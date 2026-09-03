using System.ComponentModel;
using System.Runtime.InteropServices;

namespace RawPrintBridge.Winspool;

/// <summary>
/// 通过 Windows 打印池（winspool）将原始字节流直接写入目标打印机。
/// 前提：发送方输出的指令语言（PCL/ESC-POS/ZPL/PostScript 等）与打印机匹配，
/// 本类只做透明转发，不做任何解析或渲染。
/// </summary>
internal static class RawPrinterHelper
{
    private const int ChunkSize = 64 * 1024;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct DOC_INFO_1W
    {
        public string? DocName;
        public string? OutputFile;
        public string? DataType;
    }

    [DllImport("winspool.Drv", EntryPoint = "OpenPrinterW", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool OpenPrinter(string pPrinterName, out IntPtr phPrinter, IntPtr pDefault);

    [DllImport("winspool.Drv", SetLastError = true)]
    private static extern bool ClosePrinter(IntPtr hPrinter);

    [DllImport("winspool.Drv", EntryPoint = "StartDocPrinterW", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool StartDocPrinter(IntPtr hPrinter, int level, ref DOC_INFO_1W pDocInfo);

    [DllImport("winspool.Drv", SetLastError = true)]
    private static extern bool EndDocPrinter(IntPtr hPrinter);

    [DllImport("winspool.Drv", SetLastError = true)]
    private static extern bool StartPagePrinter(IntPtr hPrinter);

    [DllImport("winspool.Drv", SetLastError = true)]
    private static extern bool EndPagePrinter(IntPtr hPrinter);

    [DllImport("winspool.Drv", SetLastError = true)]
    private static extern bool WritePrinter(IntPtr hPrinter, IntPtr pBytes, int dwCount, out int dwWritten);

    /// <summary>校验打印机名称在系统中是否存在</summary>
    public static bool PrinterExists(string printerName)
    {
        if (!OpenPrinter(printerName, out IntPtr hPrinter, IntPtr.Zero))
            return false;
        ClosePrinter(hPrinter);
        return true;
    }

    /// <summary>将原始字节流作为 RAW 数据送入打印池</summary>
    public static void SendBytesToPrinter(string printerName, byte[] data, string docName)
    {
        if (!OpenPrinter(printerName, out IntPtr hPrinter, IntPtr.Zero))
            throw new Win32Exception(Marshal.GetLastWin32Error(), $"OpenPrinter 失败: {printerName}");

        try
        {
            var docInfo = new DOC_INFO_1W { DocName = docName, OutputFile = null, DataType = "RAW" };
            if (!StartDocPrinter(hPrinter, 1, ref docInfo))
                throw new Win32Exception(Marshal.GetLastWin32Error(), "StartDocPrinter 失败");
            try
            {
                if (!StartPagePrinter(hPrinter))
                    throw new Win32Exception(Marshal.GetLastWin32Error(), "StartPagePrinter 失败");
                try
                {
                    IntPtr pBuf = Marshal.AllocHGlobal(ChunkSize);
                    try
                    {
                        int offset = 0;
                        while (offset < data.Length)
                        {
                            int len = Math.Min(ChunkSize, data.Length - offset);
                            Marshal.Copy(data, offset, pBuf, len);
                            if (!WritePrinter(hPrinter, pBuf, len, out int written) || written != len)
                                throw new Win32Exception(Marshal.GetLastWin32Error(), "WritePrinter 失败");
                            offset += len;
                        }
                    }
                    finally { Marshal.FreeHGlobal(pBuf); }
                }
                finally { EndPagePrinter(hPrinter); }
            }
            finally { EndDocPrinter(hPrinter); }
        }
        finally { ClosePrinter(hPrinter); }
    }
}
