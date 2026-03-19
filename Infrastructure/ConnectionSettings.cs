using ImplementadorCUAD.Services;

namespace ImplementadorCUAD.Infrastructure
{
    public static class ConnectionSettings
    {
        private static string? _cachedCuadConnectionString;

        /// Connection string de la base CUAD.
        /// Prioridad: Configuracion.xml > variable de entorno IMPLEMENTADORCUAD_CONNECTIONSTRING.
        /// El valor se cachea tras la primera lectura; llamar InvalidateCache() si se modifica el XML en runtime.
        public static string CuadConnectionString
        {
            get
            {
                if (_cachedCuadConnectionString != null)
                    return _cachedCuadConnectionString;

                var fromXml = new ConexionesConfigService().GetCuadConnectionString();
                if (!string.IsNullOrWhiteSpace(fromXml))
                {
                    _cachedCuadConnectionString = fromXml;
                    return _cachedCuadConnectionString;
                }

                var fromEnv = Environment.GetEnvironmentVariable("IMPLEMENTADORCUAD_CONNECTIONSTRING");
                if (!string.IsNullOrWhiteSpace(fromEnv))
                {
                    _cachedCuadConnectionString = fromEnv;
                    return _cachedCuadConnectionString;
                }

                return string.Empty;
            }
        }

        public static void InvalidateCache() => _cachedCuadConnectionString = null;
    }
}
