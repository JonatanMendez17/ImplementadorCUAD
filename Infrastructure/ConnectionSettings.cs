using System.Configuration;
using ImplementadorCUAD.Services;

namespace ImplementadorCUAD.Infrastructure
{
    public static class ConnectionSettings
    {
        private const string DefaultConnectionString =
            "Server=(localdb)\\MSSQLLocalDB;Database=ImplementadorCUAD_DB;Trusted_Connection=True;TrustServerCertificate=True;";

        private static string? _cuadConnectionString;

        static ConnectionSettings()
        {
            var fromEnv = Environment.GetEnvironmentVariable("IMPLEMENTADORCUAD_CONNECTIONSTRING");
            if (!string.IsNullOrWhiteSpace(fromEnv))
            {
                ConnectionString = fromEnv;
                return;
            }

            var fromConfig = ConfigurationManager.ConnectionStrings["ImplementadorCUADDb"]?.ConnectionString;
            if (!string.IsNullOrWhiteSpace(fromConfig))
            {
                ConnectionString = fromConfig;
                return;
            }

            ConnectionString = DefaultConnectionString;
        }

        public static string ConnectionString { get; set; }

        /// <summary>
        /// Connection string de la base CUAD (solo lectura). Si no hay sección Conexiones en config, devuelve ConnectionString (modo una sola base).
        /// </summary>
        public static string CuadConnectionString
        {
            get
            {
                if (_cuadConnectionString != null)
                    return _cuadConnectionString;
                var fromConexiones = new ConexionesConfigService().GetCuadConnectionString();
                _cuadConnectionString = !string.IsNullOrWhiteSpace(fromConexiones) ? fromConexiones : ConnectionString;
                return _cuadConnectionString;
            }
        }

        /// <summary>
        /// Fuerza a que se vuelva a leer el connection string de CUAD desde Configuracion.xml.
        /// </summary>
        public static void Reload()
        {
            _cuadConnectionString = null;
        }
    }
}
