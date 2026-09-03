using RawPrintBridge;
using RawPrintBridge.Logging;

// 使用 CreateDefaultBuilder（而非 CreateApplicationBuilder）：
// Microsoft.Extensions.Hosting 8.x 的 net462 目标完整支持它，
// 使 net8.0 与 net48 共用同一份外壳代码。
// ContentRoot 固定为 exe 目录：Windows 服务启动时工作目录是 system32，
// 若不指定会导致读不到同目录的 appsettings.json
Host.CreateDefaultBuilder(args)
    .UseContentRoot(AppContext.BaseDirectory)
    .UseWindowsService(o => o.ServiceName = "RawPrintBridge")
    .ConfigureServices((ctx, services) =>
    {
        services.Configure<PrintBridgeOptions>(ctx.Configuration.GetSection("PrintBridge"));
        services.AddHostedService<PrintForwardWorker>();
    })
    .ConfigureAppConfiguration((ctx, cfg) =>
    {
        cfg.SetBasePath(AppContext.BaseDirectory);
        cfg.AddJsonFile("appsettings.json", optional: false, reloadOnChange: false);
    })
    .ConfigureLogging(logging =>
    {
        logging.ClearProviders();
        logging.AddConsole();
        logging.AddProvider(new FileLoggerProvider(Path.Combine(AppContext.BaseDirectory, "logs")));
    })
    .Build()
    .Run();
