using ImplementadorCUAD.Infrastructure;
using ImplementadorCUAD.Models;

namespace ImplementadorCUAD.Services
{
    internal sealed class MainWorkflowService
    {
        private readonly FileImportService _fileImportService;
        private readonly GeneralValidationService _generalValidationService;
        private readonly ImplementationService _implementationService;
        private readonly IAppDbContextFactory _dbContextFactory;

        public MainWorkflowService(
            FileImportService fileImportService,
            GeneralValidationService generalValidationService,
            ImplementationService implementationService,
            IAppDbContextFactory dbContextFactory)
        {
            _fileImportService = fileImportService;
            _generalValidationService = generalValidationService;
            _implementationService = implementationService;
            _dbContextFactory = dbContextFactory;
        }

        public async Task<ValidationOutcome> ValidateAsync(
            ImplementationFileSelection selection,
            Entidad? entidadSeleccionada,
            Empleador? empleadorSeleccionado,
            IAppLogger log,
            IProgress<int>? progress = null)
        {
            var validationResult = await Task.Run(
                () => _fileImportService.ValidateAndLoadFiles(selection, log, progress)).ConfigureAwait(false);

            if (!validationResult.HasLoadedData)
            {
                return new ValidationOutcome(validationResult, false);
            }

            var entidadConsistente = _generalValidationService.ValidateEntidadConsistency(
                validationResult,
                log,
                out var entidadComun);

            if (!entidadConsistente)
                return new ValidationOutcome(validationResult, false);

            if (!MatchesSelectedEntidad(entidadComun, entidadSeleccionada))
            {
                log.Error(
                    $"La entidad detectada en archivos ('{entidadComun}') no coincide con la entidad seleccionada.");
                return new ValidationOutcome(validationResult, false);
            }

            if (empleadorSeleccionado != null
                && empleadorSeleccionado.EmrId > 0
                && string.IsNullOrWhiteSpace(empleadorSeleccionado.ConnectionString))
            {
                log.Warn($"No se encontró base de data para empleador '{empleadorSeleccionado.Nombre ?? "seleccionado"}'.");
                return new ValidationOutcome(validationResult, false);
            }

            var sinDatosPrevios = _generalValidationService.ValidateNoExistingDataForEntidad(
                entidadComun,
                empleadorSeleccionado,
                empleadorSeleccionado?.ConnectionString,
                log);

            return new ValidationOutcome(validationResult, sinDatosPrevios);
        }

        public Task CopyToDatabaseAsync(
            ImplementationValidationResult validationResult,
            ImplementationFileSelection selection,
            IAppLogger log,
            Action<int> reportProgress)
        {
            return _implementationService.CopyToDatabaseAsync(validationResult, selection, log, reportProgress);
        }

        public (int Padron, int ConsumoCab, int ConsumoDet) ClearEntityForEmpleador(
            Entidad entidadSeleccionada,
            Empleador empleadorSeleccionado,
            IAppLogger log)
        {
            if (string.IsNullOrWhiteSpace(empleadorSeleccionado.ConnectionString))
                throw new ArgumentException("ConnectionString del empleador no puede estar vacía.", nameof(empleadorSeleccionado));

            using var db = _dbContextFactory.Create(empleadorSeleccionado.ConnectionString);
            var eliminados = db.DeleteImportedDataForEntidad(
                entidadSeleccionada.Nombre ?? string.Empty,
                entidadSeleccionada.EntId);
            return eliminados;
        }

        private static bool MatchesSelectedEntidad(string entidadComun, Entidad? entidadSeleccionada)
        {
            if (entidadSeleccionada == null || entidadSeleccionada.EntId <= 0 || string.IsNullOrWhiteSpace(entidadComun))
                return false;

            var entidadNormalizada = entidadComun.Trim();

            if (!string.IsNullOrWhiteSpace(entidadSeleccionada.Nombre)
                && string.Equals(entidadNormalizada, entidadSeleccionada.Nombre.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (int.TryParse(entidadNormalizada, out var entidadId))
            {
                return entidadId == entidadSeleccionada.EntId;
            }

            return string.Equals(entidadNormalizada, entidadSeleccionada.EntId.ToString(), StringComparison.OrdinalIgnoreCase);
        }

        public sealed record ValidationOutcome(ImplementationValidationResult ValidationResult, bool ValidationCompleted);
    }
}

