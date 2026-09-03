using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Options;
using RawPrintBridge.Winspool;

namespace RawPrintBridge;

/// <summary>
/// 后台服务：监听 RAW 打印端口（默认 9100），每个 TCP 连接视为一个打印任务，
/// 连接结束（对端关闭或读空闲超时）后把收到的字节流整体转发给目标打印机。
/// </summary>
public class PrintForwardWorker : BackgroundService
{
    private readonly ILogger<PrintForwardWorker> _logger;
    private readonly PrintBridgeOptions _options;

    public PrintForwardWorker(ILogger<PrintForwardWorker> logger, IOptions<PrintBridgeOptions> options)
    {
        _logger = logger;
        _options = options.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "RawPrintBridge 启动. 监听 {Bind}:{Port}, 目标打印机: {Printer}",
            _options.BindAddress, _options.Port, _options.TargetPrinterName);

        ValidatePrinter();

        var listener = new TcpListener(IPAddress.Parse(_options.BindAddress.Trim()), _options.Port);
        listener.Server.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
        listener.Start();
        _logger.LogInformation("开始监听 (RAW 协议，端口 {Port})", _options.Port);

        while (!stoppingToken.IsCancellationRequested)
        {
            TcpClient client;
            try
            {
                client = await listener.AcceptTcpClientAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Accept 连接失败，1 秒后重试");
                try { await Task.Delay(1000, stoppingToken); } catch (OperationCanceledException) { break; }
                continue;
            }

            _ = Task.Run(() => HandleClientAsync(client), CancellationToken.None);
        }

        listener.Stop();
        _logger.LogInformation("RawPrintBridge 已停止");
    }

    private void ValidatePrinter()
    {
        if (!_options.ValidatePrinterOnStartup) return;

        if (string.IsNullOrWhiteSpace(_options.TargetPrinterName))
        {
            _logger.LogError("配置项 PrintBridge:TargetPrinterName 为空，服务保持运行但所有任务都会失败。请修改 appsettings.json 后重启服务。");
            return;
        }

        if (!RawPrinterHelper.PrinterExists(_options.TargetPrinterName))
            _logger.LogWarning("找不到打印机: {Printer}。请确认名称与 Windows 中显示的完全一致（含共享路径时用 \\\\机器\\打印名）。",
                _options.TargetPrinterName);
        else
            _logger.LogInformation("目标打印机校验通过: {Printer}", _options.TargetPrinterName);
    }

    private async Task HandleClientAsync(TcpClient client)
    {
        string remote = "unknown";
        try
        {
            using (client)
            {
                remote = client.Client.RemoteEndPoint?.ToString() ?? "unknown";
                client.NoDelay = true;
                NetworkStream stream = client.GetStream();

                long maxBytes = (long)_options.MaxJobSizeMB * 1024 * 1024;
                using var job = new MemoryStream();
                var buffer = new byte[64 * 1024];
                bool truncatedByTimeout = false;

                while (true)
                {
                    using var timeoutCts = new CancellationTokenSource(
                        TimeSpan.FromSeconds(_options.IdleTimeoutSeconds));
                    int n;
                    try
                    {
                        n = await stream.ReadAsync(buffer, timeoutCts.Token);
                    }
                    catch (OperationCanceledException)
                    {
                        // 读空闲超时：视为本次任务结束（部分发送方不主动关闭连接）
                        truncatedByTimeout = true;
                        break;
                    }
                    if (n == 0) break; // 对端正常关闭

                    if (job.Length + n > maxBytes)
                    {
                        _logger.LogError("来自 {Remote} 的任务超过 MaxJobSizeMB({Max}MB)，已丢弃", remote, _options.MaxJobSizeMB);
                        return;
                    }
                    job.Write(buffer, 0, n);
                }

                if (truncatedByTimeout)
                    _logger.LogWarning("来自 {Remote} 的连接读空闲超时，按任务结束处理", remote);

                await ForwardJobAsync(remote, job.ToArray());
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "处理来自 {Remote} 的连接时发生异常", remote);
        }
    }

    private Task ForwardJobAsync(string remote, byte[] data)
    {
        if (data.Length == 0)
        {
            _logger.LogWarning("来自 {Remote} 的任务为空，已跳过", remote);
            return Task.CompletedTask;
        }

        string docName = $"{_options.DocNamePrefix}-{DateTime.Now:yyyyMMdd-HHmmss-fff}";
        try
        {
            RawPrinterHelper.SendBytesToPrinter(_options.TargetPrinterName, data, docName);
            _logger.LogInformation("任务 {Doc} 来自 {Remote}: {Bytes} 字节已转发到打印机 {Printer}",
                docName, remote, data.Length, _options.TargetPrinterName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "任务 {Doc} 来自 {Remote}: 转发 {Bytes} 字节失败",
                docName, remote, data.Length);
        }
        return Task.CompletedTask;
    }
}
