using System.Xml.Linq;
using ImplementadorCUAD.Models;
using Microsoft.Data.SqlClient;

namespace ImplementadorCUAD.Services
{
    /// Lee y actualiza la sección Conexiones de Configuracion.xml: conexión CUAD y lista de empleadores con su connection string.
    public class ConexionesConfigService
    {
        public const string RutaConfiguracionXml = "Configuracion.xml";
        private readonly string _rutaXml = RutaConfiguracionXml;

        /// Obtiene el connection string de la base CUAD. Devuelve null si no existe la sección.
        public string? GetCuadConnectionString()
        {
            try
            {
                var document = XDocument.Load(_rutaXml);
                var conexiones = document.Root?.Element("Conexiones");
                var cuad = conexiones?.Element("Cuad");
                return cuad?.Attribute("connectionString")?.Value?.Trim();
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

                var conexionBase = conexiones.Element("ConexionBase")?.Attribute("connectionString")?.Value?.Trim();
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
                    else if (!string.IsNullOrWhiteSpace(baseDatosAttr) && !string.IsNullOrWhiteSpace(conexionBase))
                    {
                        try
                        {
                            var builder = new SqlConnectionStringBuilder(conexionBase)
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

        /// Actualiza la cadena de conexión de CUAD en Configuracion.xml,
        /// agregando o modificando el nodo <Conexiones><Cuad connectionString="..." /></Conexiones>.
        public void SetCuadConnectionString(string connectionString)
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

            var cuad = conexiones.Element("Cuad");
            if (cuad == null)
            {
                cuad = new XElement("Cuad");
                conexiones.AddFirst(cuad);
            }

            cuad.SetAttributeValue("connectionString", connectionString);

            document.Save(_rutaXml);
        }
    }
}
