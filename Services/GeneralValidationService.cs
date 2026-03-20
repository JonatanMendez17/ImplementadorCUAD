using ImplementadorCUAD.Models;
using ImplementadorCUAD.Infrastructure;

namespace ImplementadorCUAD.Services
{
    public class GeneralValidationService(IAppDbContextFactory dbContextFactory)
    {
        private readonly IAppDbContextFactory _dbContextFactory = dbContextFactory;

        public bool ValidateEntidadConsistency(ImplementationValidationResult validationResult, Action<string> log, out string entidadComun)
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
                log("No se encontro el value de 'Entidad' en los archivos cargados.");
                return false;
            }

            if (entidades.Count > 1)
            {
                log($"La entidad no coincide entre archivos. Valores detectados: {string.Join(", ", entidades)}.");
                return false;
            }

            entidadComun = entidades.First();
            return true;
        }

        public bool ValidateNoExistingDataForEntidad(string entidad, Empleador? empleador, string? targetConnectionString, Action<string> log)
        {
            if (string.IsNullOrWhiteSpace(targetConnectionString))
            {
                log($"No se encontró base de data para empleador '{empleador?.Nombre ?? "seleccionado"}'.");
                return false;
            }
            using var db = _dbContextFactory.Create(targetConnectionString);
            var existe = db.ExistsImportedDataForEntidad(entidad);
            if (existe)
            {
                var nombreEmpleador = empleador?.Nombre ?? "(sin empleador seleccionado)";
                log($"Ya existe informacion cargada para la entidad '{entidad}' en el contexto del empleador '{nombreEmpleador}'.");
                return false;
            }

            return true;
        }

        private static void AddEntidad(string fileName, IEnumerable<Dictionary<string, string>> rows, HashSet<string> entidades, Action<string> log)
        {
            var rowNumber = 1;
            foreach (var row in rows)
            {
                rowNumber++;
                if (!row.TryGetValue("Entidad", out var entidad) || string.IsNullOrWhiteSpace(entidad))
                {
                    log($"{fileName} row {rowNumber}: columna 'Entidad' vacia o inexistente.");
                    continue;
                }

                entidades.Add(entidad.Trim());
            }
        }

    }
}
