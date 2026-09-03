namespace RawPrintBridge;

/// <summary>映射 appsettings.json 中 PrintBridge 节点的配置项</summary>
public class PrintBridgeOptions
{
    /// <summary>监听地址，0.0.0.0 表示接受所有网卡</summary>
    public string BindAddress { get; set; } = "0.0.0.0";

    /// <summary>监听端口，RAW 打印协议默认 9100</summary>
    public int Port { get; set; } = 9100;

    /// <summary>目标打印机名称，必须与 Windows「打印机和扫描仪」中显示的名称完全一致</summary>
    public string TargetPrinterName { get; set; } = "";

    /// <summary>读空闲超时秒数。连接上超过该时间没有新数据则认为本次打印任务结束</summary>
    public int IdleTimeoutSeconds { get; set; } = 30;

    /// <summary>单个打印任务的最大体积（MB），超出则丢弃，防止内存被撑爆</summary>
    public int MaxJobSizeMB { get; set; } = 50;

    /// <summary>Windows 打印队列中显示的任务名前缀</summary>
    public string DocNamePrefix { get; set; } = "RawPrintBridge";

    /// <summary>启动时是否校验目标打印机是否存在</summary>
    public bool ValidatePrinterOnStartup { get; set; } = true;
}
