using ImplementadorCUAD.Models;
using ImplementadorCUAD.Infrastructure;
using Microsoft.Extensions.Logging;

namespace ImplementadorCUAD.Services
{
    public class GeneralValidationService(IAppDbContextFactory dbContextFactory, ILogger<GeneralValidationService>? logger = null)
    {
        private readonly IAppDbContextFactory _dbContextFactory = dbContextFactory;
        private readonly ILogger<GeneralValidationService>? _logger = logger;

        public bool ValidateEntidadConsistency(ImplementationValidationResult validationResult, IAppLogger log, out string entidadComun)
        {
            entidadComun = string.Empty;

            var entidades = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            AddEntidad("Categorias Socios", validationResult.DatosCategoriasValidadas, entidades, log);
            AddEntidad("Padron Socios", validationResult.DatosPadronValidados, entidades, log);
            AddEntidad("Consumos", validationResult.DatosConsumosValidados, entidades, log);
            AddEntidad("Consumos Detalle", validationResult.DatosConsumosDetalleValidados, entidades, log);
            AddEntidad("Catalogo Servicios", validationResult.DatosCatalogoServiciosValidados, entidades, log);
            AddEntidad("Consumos Servicios", validationResult.DatosServiciosValidados, entidades, log);

            if (entidades.Count == 0)
            {
                log.Error("No se encontro el value de 'Entidad' en los archivos cargados.");
                _logger?.LogWarning("No se encontro el value de Entidad en los archivos cargados.");
                return false;
            }

            if (entidades.Count > 1)
            {
                log.Error($"La entidad no coincide entre archivos. Valores detectados: {string.Join(", ", entidades)}.");
                _logger?.LogWarning("La entidad no coincide entre archivos. Valores detectados: {Entidades}", string.Join(", ", entidades));
                return false;
            }

            entidadComun = entidades.First();
            return true;
        }

        public bool ValidateNoExistingDataForEntidad(string entidad, Empleador? empleador, string? targetConnectionString, IAppLogger log)
        {
            if (string.IsNullOrWhiteSpace(targetConnectionString))
            {
                log.Error($"No se encontró base de data para empleador '{empleador?.Nombre ?? "seleccionado"}'.");
                _logger?.LogWarning("No se encontro base de data para empleador {Empleador}.", empleador?.Nombre ?? "seleccionado");
                return false;
            }
            using var db = _dbContextFactory.Create(targetConnectionString);
            var existe = db.ExistsImportedDataForEntidad(entidad);
            if (existe)
            {
                var nombreEmpleador = empleador?.Nombre ?? "(sin empleador seleccionado)";
                log.Warn($"Ya existe informacion cargada para la entidad '{entidad}' en el contexto del empleador '{nombreEmpleador}'.");
                _logger?.LogWarning("Ya existe informacion cargada para entidad {Entidad} en empleador {Empleador}.", entidad, nombreEmpleador);
                return false;
            }

            return true;
        }

        private static void AddEntidad(string fileName, IEnumerable<Dictionary<string, string>> rows, HashSet<string> entidades, IAppLogger log)
        {
            var rowNumber = 1;
            foreach (var row in rows)
            {
                rowNumber++;
                if (!row.TryGetValue("Entidad", out var entidad) || string.IsNullOrWhiteSpace(entidad))
                {
                    log.Warn($"{fileName} row {rowNumber}: columna 'Entidad' vacia o inexistente.");
                    continue;
                }

                entidades.Add(entidad.Trim());
            }
        }

    }
}
