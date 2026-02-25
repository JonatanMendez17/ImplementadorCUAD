using MigradorCUAD.Data;
using MigradorCUAD.Models;

namespace MigradorCUAD.Services
{
    public class GeneralValidationService
    {
        public bool ValidateEntidadConsistency(
            MigrationValidationResult validationResult,
            Action<string> log,
            out string entidadComun)
        {
            entidadComun = string.Empty;

            var entidades = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            AddEntidades("Categorias", validationResult.DatosCategoriasValidadas, entidades, log);
            AddEntidades("Padron", validationResult.DatosPadronValidados, entidades, log);
            AddEntidades("Consumos", validationResult.DatosConsumosValidados, entidades, log);
            AddEntidades("ConsumosDetalle", validationResult.DatosConsumosDetalleValidados, entidades, log);
            AddEntidades("Servicios", validationResult.DatosServiciosValidados, entidades, log);
            AddEntidades("CatalogoServicios", validationResult.DatosCatalogoServiciosValidados, entidades, log);

            if (entidades.Count == 0)
            {
                log("❌ No se encontró el valor de 'Entidad' en los archivos cargados.");
                return false;
            }

            if (entidades.Count > 1)
            {
                log($"❌ La entidad no coincide entre archivos. Valores detectados: {string.Join(", ", entidades)}");
                return false;
            }

            entidadComun = entidades.First();
            log($"✅ Validación de entidad entre archivos: {entidadComun}");
            return true;
        }

        public bool ValidateNoExistingDataForEntidad(string entidad, Empleador? empleador, Action<string> log)
        {
            using var db = new AppDbContext();
            var existe = db.ExistsImportedDataForEntidad(entidad);
            if (existe)
            {
                var nombreEmpleador = empleador?.Nombre ?? "(sin empleador seleccionado)";
                log($"❌ Ya existe información cargada para la entidad '{entidad}' en el contexto del empleador '{nombreEmpleador}'.");
                return false;
            }

            log($"✅ No existe información previa para la entidad '{entidad}'.");
            return true;
        }

        private static void AddEntidades(
            string nombreArchivo,
            IEnumerable<Dictionary<string, string>> filas,
            HashSet<string> entidades,
            Action<string> log)
        {
            var numeroFila = 1;
            foreach (var fila in filas)
            {
                numeroFila++;
                if (!fila.TryGetValue("Entidad", out var entidad) || string.IsNullOrWhiteSpace(entidad))
                {
                    log($"❌ {nombreArchivo} fila {numeroFila}: columna 'Entidad' vacía o inexistente.");
                    continue;
                }

                entidades.Add(entidad.Trim());
            }
        }
    }
}
