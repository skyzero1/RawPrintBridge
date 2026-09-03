using RawPrintBridge;
using RawPrintBridge.Logging;

// ContentRoot 固定为 exe 目录：Windows 服务启动时工作目录是 system32，
// 若不指定会导致读不到同目录的 appsettings.json
var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
{
    ContentRootPath = AppContext.BaseDirectory,
    Args = args
});

builder.Services.AddWindowsService(o => o.ServiceName = "RawPrintBridge");
builder.Services.Configure<PrintBridgeOptions>(builder.Configuration.GetSection("PrintBridge"));

builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddProvider(new FileLoggerProvider(Path.Combine(AppContext.BaseDirectory, "logs")));

builder.Services.AddHostedService<PrintForwardWorker>();

builder.Build().Run();
