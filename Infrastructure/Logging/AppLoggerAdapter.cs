using Implementador.Infrastructure;

namespace Implementador.Infrastructure.Logging
{
    public sealed class AppLoggerAdapter(Action<string> info, Action<string> warn, Action<string> error, Action? separator = null) : IAppLogger
    {
        private readonly Action<string> _info = info;
        private readonly Action<string> _warn = warn;
        private readonly Action<string> _error = error;
        private readonly Action? _separator = separator;

        public void Info(string message) => _info(message);
        public void Warn(string message) => _warn(message);
        public void Error(string message) => _error(message);
        public void Error(Exception ex, string message) => _error($"{message}. {ex.Message}");
        public void Separator() => _separator?.Invoke();
    }
}


