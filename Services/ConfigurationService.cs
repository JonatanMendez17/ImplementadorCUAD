using System.Xml.Linq;
using ImplementadorCUAD.Models;

namespace ImplementadorCUAD.Services
{
    /// Servicio encargado de leer la configuración de columnas
    public class ConfigurationService
    {
        private readonly string _rutaXml = ConnectionsConfigService.RutaConfiguracionXml;

        /// Obtiene la lista de columnas configuradas para un archivo lógico.
        public List<ColumnConfiguration> GetColumns(string fileName)
        {
            var document = XDocument.Load(_rutaXml);

            var columnas = document
                .Descendants("Archivo")
                .Where(a => a.Attribute("nombre")?.Value == fileName)
                .Descendants("Columna")
                .Select(c =>
                {
                    var nombre = c.Attribute("nombre")?.Value?.Trim() ?? string.Empty;
                    var clave = c.Attribute("clave")?.Value?.Trim();
                    var requeridaRaw = c.Attribute("requerida")?.Value?.Trim();

                    var alias = c
                        .Elements("Alias")
                        .Select(a => a.Value.Trim())
                        .Where(a => !string.IsNullOrWhiteSpace(a))
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToList();

                    if (!alias.Any() && !string.IsNullOrWhiteSpace(nombre))
                    {
                        alias.Add(nombre);
                    }

                    return new ColumnConfiguration
                    {
                        Clave = string.IsNullOrWhiteSpace(clave) ? nombre : clave,
                        Nombre = nombre,
                        Requerida = !bool.TryParse(requeridaRaw, out var requerida) || requerida,
                        Alias = alias,
                        TipoDato = c.Attribute("tipo")?.Value ?? string.Empty,
                        LargoMaximo = int.Parse(c.Attribute("largoMaximo")?.Value ?? "0")
                    };
                })
                .ToList();

            return columnas;
        }
    }
}
