using ImplementadorCUAD.Models;
using Microsoft.Extensions.Logging;

namespace ImplementadorCUAD.Services
{
    public sealed record UiLogRecord(DateTime TimestampUtc, LogSeverity Severity, string Message);

    public static class UiLogStream
    {
        public static event Action<UiLogRecord>? LogReceived;

        public static void Publish(UiLogRecord record)
        {
            LogReceived?.Invoke(record);
        }
    }

    public sealed class UiLoggerProvider : ILoggerProvider
    {
        public ILogger CreateLogger(string categoryName)
        {
            return new UiLogger();
        }

        public void Dispose()
        {
        }

        private sealed class UiLogger : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull
            {
                return null;
            }

            public bool IsEnabled(LogLevel logLevel)
            {
                return logLevel is LogLevel.Information or LogLevel.Warning or LogLevel.Error;
            }

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            {
                if (!IsEnabled(logLevel))
                {
                    return;
                }

                var message = formatter(state, exception);
                if (string.IsNullOrWhiteSpace(message))
                {
                    return;
                }

                if (exception != null)
                {
                    message = $"{message} | Exception: {exception.Message}";
                }

                UiLogStream.Publish(new UiLogRecord(
                    DateTime.UtcNow,
                    MapSeverity(logLevel),
                    message));
            }

            private static LogSeverity MapSeverity(LogLevel level)
            {
                return level switch
                {
                    LogLevel.Warning => LogSeverity.Warning,
                    LogLevel.Error => LogSeverity.Error,
                    _ => LogSeverity.Information
                };
            }
        }
    }
}
