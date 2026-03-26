using ImplementadorCUAD.Infrastructure;
using ImplementadorCUAD.Models;

namespace ImplementadorCUAD.Services
{
    public class ImplementationService(ImplementationMapperService mapperService, IAppDbContextFactory dbContextFactory)
    {
        private readonly ImplementationMapperService _mapperService = mapperService;
        private readonly IAppDbContextFactory _dbContextFactory = dbContextFactory;

        public async Task CopyToDatabaseAsync(ImplementationValidationResult validationResult, ImplementationFileSelection selection, IAppLogger log, Action<int> reportProgress)
        {
            if (string.IsNullOrWhiteSpace(selection.TargetConnectionString))
            {
                log.Error("No se encontró base de data para el empleador seleccionado.");
                reportProgress(100);
                return;
            }
            using var db = _dbContextFactory.Create(selection.TargetConnectionString);

            var insertadosPadron = 0;
            var insertadosConsumosDetalle = 0;
            var insertadosConsumos = 0;

            var padronSocios = !string.IsNullOrWhiteSpace(selection.ArchivoPadron)
                ? _mapperService.MapPadronSocios(validationResult.DatosPadronValidados, log)
                : new List<ImportarPadronSocio>();

            var consumosDetalle = (selection.ArchivosConsumosDetalle?.Count ?? 0) > 0
                ? _mapperService.MapConsumosDetalle(validationResult.DatosConsumosDetalleValidados, log)
                : new List<ImportarConsumosDet>();

            var consumosImportados = !string.IsNullOrWhiteSpace(selection.ArchivoConsumos)
                ? _mapperService.MapConsumos(validationResult.DatosConsumosValidados, log)
                : new List<ImportarConsumoCab>();

            var totalRegistros =
                padronSocios.Count +
                consumosDetalle.Count +
                consumosImportados.Count;

            if (totalRegistros == 0)
            {
                reportProgress(100);
                log.Warn("No hay registros validos para implementar en esta ejecucion.");
                return;
            }

            var insertadosGlobal = 0;
            var progress = new Progress<int>(delta =>
            {
                insertadosGlobal += delta;
                var porcentaje = (int)Math.Round(
                    (double)insertadosGlobal * 100 / totalRegistros,
                    MidpointRounding.AwayFromZero);
                reportProgress(Math.Min(100, porcentaje));
            });

            if (padronSocios.Any())
            {
                insertadosPadron = await db.InsertPadronSocioAsync(padronSocios, progress).ConfigureAwait(false);
            }
            else if (!string.IsNullOrWhiteSpace(selection.ArchivoPadron))
            {
                log.Warn("No hay registros validos de padron socios para insertar en base de data.");
            }

            if (consumosDetalle.Any())
            {
                insertadosConsumosDetalle = await db.InsertImportarConsumosDetAsync(consumosDetalle, progress).ConfigureAwait(false);
            }
            else if ((selection.ArchivosConsumosDetalle?.Count ?? 0) > 0)
            {
                log.Warn("No hay consumos detalle validos para insertar en base de data.");
            }

            if (consumosImportados.Any())
            {
                insertadosConsumos = await db.InsertImportarConsumoCabAsync(consumosImportados, progress).ConfigureAwait(false);
            }
            else if (!string.IsNullOrWhiteSpace(selection.ArchivoConsumos))
            {
                log.Warn("No hay registros validos para insertar en base de data.");
            }

            if (insertadosPadron > 0 || insertadosConsumosDetalle > 0 || insertadosConsumos > 0)
            {
                log.Info($"Resumen implementacion: Importar_Padron_socios={insertadosPadron}, Importar_Consumos_Cab={insertadosConsumosDetalle}, Importar_Consumo_Det={insertadosConsumos}.");
            }
            else
            {
                log.Warn("Resumen implementacion: no se insertaron registros en la base.");
            }
        }
    }
}
