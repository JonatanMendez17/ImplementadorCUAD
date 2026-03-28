using Implementador.Models;
using Implementador.Infrastructure;
using Microsoft.Extensions.Logging;

namespace Implementador.Application.Validation
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
                log.Error("No quedaron registros validos en los archivos cargados luego de la validacion. Verifique los errores reportados anteriormente en el log.");
                return false;
            }

            if (entidades.Count > 1)
            {
                log.Error($"La entidad no coincide entre archivos. Valores detectados: {string.Join(", ", entidades)}.");
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
                return false;
            }
            using var db = _dbContextFactory.Create(targetConnectionString);
            var existe = db.ExistsImportedDataForEntidad(entidad);
            if (existe)
            {
                var nombreEmpleador = empleador?.Nombre ?? "(sin empleador seleccionado)";
                log.Warn($"Ya existe informacion cargada para la entidad '{entidad}' en el contexto del empleador '{nombreEmpleador}'.");
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
                    log.Warn($"{fileName} fila {rowNumber}: columna 'Entidad' vacia o inexistente.");
                    continue;
                }

                entidades.Add(entidad.Trim());
            }
        }

    }
}


