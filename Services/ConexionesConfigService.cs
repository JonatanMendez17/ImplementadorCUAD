using System.Xml.Linq;
using ImplementadorCUAD.Models;
using Microsoft.Data.SqlClient;

namespace ImplementadorCUAD.Services
{
    /// Lee la sección Conexiones de Configuracion.xml: conexión CUAD y lista de empleadores con su connection string.
    public class ConexionesConfigService
    {
        private readonly string _rutaXml = "Configuracion.xml";

        /// Obtiene el connection string de la base CUAD.
        /// Soporta tres esquemas:
        /// 1) <Cuad connectionString="...">  (esquema original)
        /// 2) <ConexionBase connectionString="..."/><Cuad baseDatos="CUAD"/>  (esquema parametrizado antiguo)
        /// 3) <ConexionBase connectionString="..."/><Empleador nombre="CUAD" baseDatos="CUAD"/>  (CUAD como empleador)
        public string? GetCuadConnectionString()
        {
            try
            {
                var document = XDocument.Load(_rutaXml);
                var conexiones = document.Root?.Element("Conexiones");
                if (conexiones == null)
                    return null;

                var cuad = conexiones.Element("Cuad");

                // 1) Esquema original: connectionString directo en <Cuad>
                if (cuad != null)
                {
                    var direct = cuad.Attribute("connectionString")?.Value?.Trim();
                    if (!string.IsNullOrWhiteSpace(direct))
                        return direct;

                    // 2) Esquema parametrizado: usar ConexionBase + baseDatos en <Cuad>
                    var baseDatosCuad = cuad.Attribute("baseDatos")?.Value?.Trim();
                    var conexionBaseCuad = conexiones.Element("ConexionBase")?
                        .Attribute("connectionString")?
                        .Value?
                        .Trim();

                    if (!string.IsNullOrWhiteSpace(baseDatosCuad) && !string.IsNullOrWhiteSpace(conexionBaseCuad))
                    {
                        try
                        {
                            var builderCuad = new SqlConnectionStringBuilder(conexionBaseCuad)
                            {
                                InitialCatalog = baseDatosCuad
                            };
                            return builderCuad.ConnectionString;
                        }
                        catch
                        {
                            // ignorar y seguir con el esquema 3
                        }
                    }
                }

                // 3) Esquema CUAD como empleador: buscar <Empleador nombre="CUAD">
                var conexionBaseEmpleador = conexiones.Element("ConexionBase")?
                    .Attribute("connectionString")?
                    .Value?
                    .Trim();
                var empleadorCuad = conexiones
                    .Elements("Empleador")
                    .FirstOrDefault(e =>
                        string.Equals(e.Attribute("nombre")?.Value?.Trim(), "CUAD", StringComparison.OrdinalIgnoreCase));

                if (empleadorCuad != null)
                {
                    var empConnAttr = empleadorCuad.Attribute("connectionString")?.Value?.Trim();
                    if (!string.IsNullOrWhiteSpace(empConnAttr))
                        return empConnAttr;

                    var baseDatosAttr = empleadorCuad.Attribute("baseDatos")?.Value?.Trim();
                    if (!string.IsNullOrWhiteSpace(baseDatosAttr) && !string.IsNullOrWhiteSpace(conexionBaseEmpleador))
                    {
                        try
                        {
                            var builderEmp = new SqlConnectionStringBuilder(conexionBaseEmpleador)
                            {
                                InitialCatalog = baseDatosAttr
                            };
                            return builderEmp.ConnectionString;
                        }
                        catch
                        {
                            return null;
                        }
                    }
                }

                return null;
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
                    // CUAD se reserva solo para lectura y no debe aparecer en el combo de empleadores.
                    if (string.IsNullOrWhiteSpace(nombre) ||
                        string.Equals(nombre, "CUAD", StringComparison.OrdinalIgnoreCase))
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

        /// Persiste la cadena de conexión de CUAD en Configuracion.xml.
        /// En configuraciones nuevas se guarda en el nodo <Cuad>.
        /// En configuraciones antiguas se mantiene compatibilidad actualizando también un posible empleador "CUAD".
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

            // Esquema nuevo: nodo <Cuad>
            var cuad = conexiones.Element("Cuad");
            if (cuad == null)
            {
                cuad = new XElement("Cuad");
                cuad.SetAttributeValue("nombre", "CUAD");
                conexiones.AddFirst(cuad);
            }

            // Aseguramos que mantenga nombre="CUAD" y escribimos la cadena completa
            cuad.SetAttributeValue("nombre", "CUAD");
            cuad.SetAttributeValue("connectionString", connectionString);

            // Compatibilidad: si existiera un Empleador "CUAD" viejo, lo eliminamos para que no genere confusión.
            var empleadorCuad = conexiones
                .Elements("Empleador")
                .FirstOrDefault(e =>
                    string.Equals(e.Attribute("nombre")?.Value?.Trim(), "CUAD", StringComparison.OrdinalIgnoreCase));
            if (empleadorCuad != null)
            {
                empleadorCuad.Remove();
            }

            document.Save(_rutaXml);
        }
    }
}
