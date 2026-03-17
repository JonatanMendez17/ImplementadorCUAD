using ImplementadorCUAD.Services;

namespace ImplementadorCUAD.Infrastructure
{
    public static class ConnectionSettings
    {
        private const string DefaultConnectionString =
            "Server=(localdb)\\MSSQLLocalDB;Database=ImplementadorCUAD_DB;Trusted_Connection=True;TrustServerCertificate=True;";

        static ConnectionSettings()
        {
            var fromEnv = Environment.GetEnvironmentVariable("IMPLEMENTADORCUAD_CONNECTIONSTRING");
            if (!string.IsNullOrWhiteSpace(fromEnv))
            {
                ConnectionString = fromEnv;
                return;
            }

            ConnectionString = DefaultConnectionString;
        }

        /// Cadena de conexión "general" obtenida de variables de entorno, app.config o valor por defecto.
        public static string ConnectionString { get; set; }

        /// Connection string de la base CUAD (solo lectura).
        /// Intenta leerla desde Configuracion.xml (sección <Conexiones><Cuad .../>). Si no hay valor,
        /// utiliza la ConnectionString general como último recurso (modo una sola base).
        public static string CuadConnectionString
        {
            get
            {
                var fromConfigXml = new ConexionesConfigService().GetCuadConnectionString();
                return !string.IsNullOrWhiteSpace(fromConfigXml)
                    ? fromConfigXml
                    : ConnectionString;
            }
        }
    }
}
