using Microsoft.Extensions.Logging;

namespace DFCMAD.Core.Logging;

public sealed class RollingFileLoggerProvider : ILoggerProvider
{
    private readonly FileLogWriter _writer;

    public RollingFileLoggerProvider(FileLogWriter writer)
    {
        _writer = writer;
    }

    public ILogger CreateLogger(string categoryName) => new RollingFileLogger(categoryName, _writer);

    public void Dispose()
    {
    }

    private sealed class RollingFileLogger : ILogger
    {
        private readonly string _categoryName;
        private readonly FileLogWriter _writer;

        public RollingFileLogger(string categoryName, FileLogWriter writer)
        {
            _categoryName = categoryName;
            _writer = writer;
        }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Information;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
            {
                return;
            }

            var message = formatter(state, exception);
            var line = $"{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss.fff zzz} [{logLevel}] {_categoryName}: {message}";
            if (exception is not null)
            {
                line += $"{Environment.NewLine}{exception}";
            }

            try
            {
                _writer.Write(line);
            }
            catch
            {
                // Logging must never bring down the tray app.
            }
        }
    }
}
