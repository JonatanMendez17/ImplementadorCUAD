using System.Xml.Linq;
using ImplementadorCUAD.Models;
using Microsoft.Data.SqlClient;

namespace ImplementadorCUAD.Services
{
    /// Lee y actualiza la sección Conexiones de `Configuration.xml`.
    /// - `ConexionBase`: base (solo lectura).
    /// - `ConexionEmpleadores`: parámetros comunes para construir la conexión destino de cada empleador.
    public class ConnectionsConfigService
    {
        public const string RutaConfiguracionXml = "Configuration.xml";
        private readonly string _rutaXml = RutaConfiguracionXml;

        /// Obtiene el connection string de la base (`ConexionBase`).
        /// Devuelve null si el nodo no existe o no puede leerse.
        public string? GetConexionBaseConnectionString()
        {
            try
            {
                var document = XDocument.Load(_rutaXml);
                var conexiones = document.Root?.Element("Conexiones");
                var conexionBase = conexiones?.Element("ConexionBase");
                return conexionBase?.Attribute("connectionString")?.Value?.Trim();
            }
            catch
            {
                return null;
            }
        }

        /// Lista de empleadores definidos en configuración, con su connection string ya resuelto.
        public IReadOnlyList<EmpleadorConfig> GetEmpleadores()
        {
            var resultado = new List<EmpleadorConfig>();
            try
            {
                var document = XDocument.Load(_rutaXml);
                var conexiones = document.Root?.Element("Conexiones");
                if (conexiones == null)
                    return resultado;

                    var conexionEmpleadores = conexiones.Element("ConexionEmpleadores")
                        ?.Attribute("connectionString")?.Value?.Trim();
                var empleadorElements = conexiones.Elements("Empleador").ToList();

                foreach (var emp in empleadorElements)
                {
                    var nombre = emp.Attribute("nombre")?.Value?.Trim() ?? string.Empty;
                    if (string.IsNullOrWhiteSpace(nombre))
                        continue;

                    var connectionStringAttr = emp.Attribute("connectionString")?.Value?.Trim();
                    var baseDatosAttr = emp.Attribute("baseDatos")?.Value?.Trim();

                    string? connectionString = null;
                    if (!string.IsNullOrWhiteSpace(connectionStringAttr))
                    {
                        connectionString = connectionStringAttr;
                    }
                    else if (!string.IsNullOrWhiteSpace(baseDatosAttr) && !string.IsNullOrWhiteSpace(conexionEmpleadores))
                    {
                        try
                        {
                            var builder = new SqlConnectionStringBuilder(conexionEmpleadores)
                            {
                                InitialCatalog = baseDatosAttr
                            };
                            connectionString = builder.ConnectionString;
                        }
                        catch
                        {
                            continue;
                        }
                    }

                    if (string.IsNullOrWhiteSpace(connectionString))
                        continue;

                    resultado.Add(new EmpleadorConfig
                    {
                        Nombre = nombre,
                        ConnectionString = connectionString,
                        BaseDatos = baseDatosAttr
                    });
                }

                return resultado;
            }
            catch
            {
                return resultado;
            }
        }

        /// Actualiza la cadena de conexión de `ConexionBase` en `Configuration.xml`,
        /// agregando o modificando el nodo <Conexiones><ConexionBase connectionString="..." /></Conexiones>.
        public void SetConexionBaseConnectionString(string connectionString)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
                throw new ArgumentException("La cadena de conexión no puede estar vacía.", nameof(connectionString));

            var document = XDocument.Load(_rutaXml);
            var root = document.Root ?? new XElement("Configuracion");
            if (document.Root == null)
            {
                document.Add(root);
            }

            var conexiones = root.Element("Conexiones");
            if (conexiones == null)
            {
                conexiones = new XElement("Conexiones");
                root.Add(conexiones);
            }

            var conexionBase = conexiones.Element("ConexionBase");
            if (conexionBase == null)
            {
                conexionBase = new XElement("ConexionBase");
                conexiones.AddFirst(conexionBase);
            }

            conexionBase.SetAttributeValue("connectionString", connectionString);

            document.Save(_rutaXml);
        }
    }
}
