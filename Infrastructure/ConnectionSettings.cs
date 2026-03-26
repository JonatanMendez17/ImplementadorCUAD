using ImplementadorCUAD.Services;
using Microsoft.Extensions.Logging;

namespace ImplementadorCUAD.Infrastructure
{
    public static class ConnectionSettings
    {
        private static string? _cachedBaseConnectionString;
        private static ILoggerFactory? _loggerFactory;

        public static void SetLoggerFactory(ILoggerFactory loggerFactory)
        {
            _loggerFactory = loggerFactory;
        }

        /// Connection string de la base (`ConexionBase` en `Configuration.xml`).
        /// El value se cachea tras la primera lectura; llamar InvalidateCache() si se modifica el XML en runtime.
        public static string BaseConnectionString
        {
            get
            {
                if (_cachedBaseConnectionString != null)
                    return _cachedBaseConnectionString;

                var logger = _loggerFactory?.CreateLogger<ConnectionsConfigService>();
                var fromXml = new ConnectionsConfigService(logger).GetConexionBaseConnectionString();
                if (!string.IsNullOrWhiteSpace(fromXml))
                {
                    _cachedBaseConnectionString = fromXml;
                    return _cachedBaseConnectionString;
                }

                return string.Empty;
            }
        }

        public static void InvalidateCache() => _cachedBaseConnectionString = null;
    }
}
