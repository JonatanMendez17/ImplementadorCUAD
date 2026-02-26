using MigradorCUAD.Data;
using MigradorCUAD.Models;

namespace MigradorCUAD.Services
{
    public class MigrationService
    {
        private readonly MigrationMapperService _mapperService;

        public MigrationService(MigrationMapperService mapperService)
        {
            _mapperService = mapperService;
        }

        public Task CopyToDatabaseAsync(
            MigrationValidationResult validationResult,
            MigrationFileSelection selection,
            Action<string> log,
            Action<int> reportProgress)
        {
            using var db = new AppDbContext();

            var totalPasos = 0;
            if (!string.IsNullOrWhiteSpace(selection.ArchivoPadron))
            {
                totalPasos++;
            }

            if (!string.IsNullOrWhiteSpace(selection.ArchivoConsumosDetalle))
            {
                totalPasos++;
            }

            if (!string.IsNullOrWhiteSpace(selection.ArchivoConsumos))
            {
                totalPasos++;
            }

            if (totalPasos == 0)
            {
                reportProgress(100);
                log("No hay archivos de migracion compatibles para procesar en esta ejecucion.");
                return Task.CompletedTask;
            }

            var pasosCompletados = 0;
            void AvanzarProgreso()
            {
                pasosCompletados++;
                var porcentaje = (int)Math.Round((double)pasosCompletados * 100 / totalPasos, MidpointRounding.AwayFromZero);
                reportProgress(Math.Min(100, porcentaje));
            }

            if (!string.IsNullOrWhiteSpace(selection.ArchivoPadron))
            {
                var padronSocios = _mapperService.MapPadronSocios(validationResult.DatosPadronValidados, log);
                if (padronSocios.Any())
                {
                    db.InsertPadronSocio(padronSocios);
                    log($"Padron de socios insertado correctamente en Padron_socios ({padronSocios.Count} registros).");
                }
                else
                {
                    log("No hay registros validos de padron para insertar en Padron_socios.");
                }

                AvanzarProgreso();
            }

            if (!string.IsNullOrWhiteSpace(selection.ArchivoConsumosDetalle))
            {
                var consumosDetalle = _mapperService.MapConsumosDetalle(validationResult.DatosConsumosDetalleValidados, log);
                if (consumosDetalle.Any())
                {
                    log($"Insertando {consumosDetalle.Count} registros en Importar_Consumos_Detalle...");
                    db.InsertImportarConsumosDet(consumosDetalle);
                    log("Consumos detalle insertados correctamente en Importar_Consumos_Detalle.");
                }
                else
                {
                    log("No hay consumos detalle validos para insertar en Importar_Consumos_Detalle.");
                }

                AvanzarProgreso();
            }

            if (!string.IsNullOrWhiteSpace(selection.ArchivoConsumos))
            {
                var consumosImportados = _mapperService.MapConsumos(validationResult.DatosConsumosValidados, log);
                if (consumosImportados.Any())
                {
                    log($"Insertando {consumosImportados.Count} registros en Consumo...");
                    db.InsertImportarConsumoCab(consumosImportados);
                    log("Consumos insertados correctamente en tabla Consumo.");
                }
                else
                {
                    log("No hay registros validos para insertar en tabla Consumo.");
                }

                AvanzarProgreso();
            }

            return Task.CompletedTask;
        }
    }
}
