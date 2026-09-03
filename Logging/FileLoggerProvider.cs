using System.Text;

namespace RawPrintBridge.Logging;

/// <summary>轻量文件日志：每天一个日志文件，写入 exe 目录下的 logs 文件夹</summary>
public sealed class FileLoggerProvider : ILoggerProvider
{
    private readonly string _logDir;
    private readonly object _lock = new();
    private StreamWriter? _writer;
    private string? _currentFile;

    public FileLoggerProvider(string logDir) => _logDir = logDir;

    public ILogger CreateLogger(string categoryName) => new FileLogger(this, categoryName);

    internal void Write(string category, LogLevel level, string message, Exception? ex)
    {
        var sb = new StringBuilder();
        sb.Append(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"))
          .Append(" [").Append(level).Append("] ")
          .Append(category).Append(": ").Append(message);
        if (ex != null) sb.AppendLine().Append(ex);

        lock (_lock)
        {
            try
            {
                string file = Path.Combine(_logDir, $"rawprintbridge-{DateTime.Now:yyyyMMdd}.log");
                if (_writer == null || !string.Equals(_currentFile, file, StringComparison.OrdinalIgnoreCase))
                {
                    _writer?.Dispose();
                    Directory.CreateDirectory(_logDir);
                    _writer = new StreamWriter(file, append: true, Encoding.UTF8) { AutoFlush = true };
                    _currentFile = file;
                }
                _writer.WriteLine(sb.ToString());
            }
            catch
            {
                // 日志写入失败不应影响主流程
            }
        }
    }

    public void Dispose()
    {
        lock (_lock)
        {
            _writer?.Dispose();
            _writer = null;
        }
    }

    private sealed class FileLogger(FileLoggerProvider owner, string category) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Information;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel)) return;
            owner.Write(category, logLevel, formatter(state, exception), exception);
        }
    }
}
