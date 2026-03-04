using ImplementadorCUAD.Data;
using ImplementadorCUAD.Infrastructure;
using ImplementadorCUAD.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ImplementadorCUAD.Services
{
    public class ImplementacionService
    {
        private readonly ImplementacionMapperService _mapperService;
        private readonly IAppDbContextFactory _dbContextFactory;

        public ImplementacionService(ImplementacionMapperService mapperService, IAppDbContextFactory dbContextFactory)
        {
            _mapperService = mapperService;
            _dbContextFactory = dbContextFactory;
        }

        public async Task CopyToDatabaseAsync(
            ImplementacionValidationResult validationResult,
            ImplementacionFileSelection selection,
            Action<string> log,
            Action<int> reportProgress)
        {
            using var db = _dbContextFactory.Create();

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
                log("No hay registros validos para implementar en esta ejecucion.");
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
                log("No hay registros validos de padron para insertar en Padron_socios.");
            }

            if (consumosDetalle.Any())
            {
                insertadosConsumosDetalle = await db.InsertImportarConsumosDetAsync(consumosDetalle, progress).ConfigureAwait(false);
            }
            else if ((selection.ArchivosConsumosDetalle?.Count ?? 0) > 0)
            {
                log("No hay consumos detalle validos para insertar en Importar_Consumos_Detalle.");
            }

            if (consumosImportados.Any())
            {
                insertadosConsumos = await db.InsertImportarConsumoCabAsync(consumosImportados, progress).ConfigureAwait(false);
            }
            else if (!string.IsNullOrWhiteSpace(selection.ArchivoConsumos))
            {
                log("No hay registros validos para insertar en tabla Consumo.");
            }

            if (insertadosPadron > 0 || insertadosConsumosDetalle > 0 || insertadosConsumos > 0)
            {
                log($"Resumen implementaci?n: Padron_socios={insertadosPadron}, Importar_Consumos_Detalle={insertadosConsumosDetalle}, Consumo={insertadosConsumos}.");
            }
            else
            {
                log("Resumen implementaci?n: no se insertaron registros en la base.");
            }
        }
    }
}
