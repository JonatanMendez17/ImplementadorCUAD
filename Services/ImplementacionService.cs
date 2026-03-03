using ImplementadorCUAD.Data;
using ImplementadorCUAD.Models;

namespace ImplementadorCUAD.Services
{
    public class ImplementacionService
    {
        private readonly ImplementacionMapperService _mapperService;

        public ImplementacionService(ImplementacionMapperService mapperService)
        {
            _mapperService = mapperService;
        }

        public Task CopyToDatabaseAsync(
            ImplementacionValidationResult validationResult,
            ImplementacionFileSelection selection,
            Action<string> log,
            Action<int> reportProgress)
        {
            using var db = new AppDbContext();

            var insertadosPadron = 0;
            var insertadosConsumosDetalle = 0;
            var insertadosConsumos = 0;

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
                log("No hay archivos de implementación compatibles para procesar en esta ejecucion.");
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
                    insertadosPadron = padronSocios.Count;
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
                    db.InsertImportarConsumosDet(consumosDetalle);
                    insertadosConsumosDetalle = consumosDetalle.Count;
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
                    db.InsertImportarConsumoCab(consumosImportados);
                    insertadosConsumos = consumosImportados.Count;
                }
                else
                {
                    log("No hay registros validos para insertar en tabla Consumo.");
                }

                AvanzarProgreso();
            }

            if (insertadosPadron > 0 || insertadosConsumosDetalle > 0 || insertadosConsumos > 0)
            {
                log($"Resumen implementación: Padron_socios={insertadosPadron}, Importar_Consumos_Detalle={insertadosConsumosDetalle}, Consumo={insertadosConsumos}.");
            }
            else
            {
                log("Resumen implementación: no se insertaron registros en la base.");
            }

            return Task.CompletedTask;
        }
    }
}
