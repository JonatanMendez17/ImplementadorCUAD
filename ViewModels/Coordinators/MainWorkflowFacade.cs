using Implementador.Models;
using Implementador.Application.Workflows;
using Implementador.Infrastructure;

namespace Implementador.ViewModels.Coordinators;

internal sealed class MainWorkflowFacade
{
    private readonly MainWorkflowService _workflowService;

    public MainWorkflowFacade(MainWorkflowService workflowService)
    {
        _workflowService = workflowService;
    }

    public Task<MainWorkflowService.ValidationOutcome> ValidateAsync(
        ImplementationFileSelection selection,
        Entidad? entidadSeleccionada,
        Empleador? empleadorSeleccionado,
        IAppLogger log,
        IProgress<int>? progress = null)
    {
        return _workflowService.ValidateAsync(
            selection,
            entidadSeleccionada,
            empleadorSeleccionado,
            log,
            progress);
    }

    public Task<int> CopyToDatabaseAsync(
        ImplementationValidationResult validationResult,
        ImplementationFileSelection selection,
        IAppLogger log,
        Action<int> reportProgress)
    {
        return _workflowService.CopyToDatabaseAsync(validationResult, selection, log, reportProgress);
    }

    public (int Padron, int ConsumoCab, int ConsumoDet) ClearEntityForEmpleador(
        Entidad entidadSeleccionada,
        Empleador empleadorSeleccionado,
        IAppLogger log)
    {
        return _workflowService.ClearEntityForEmpleador(entidadSeleccionada, empleadorSeleccionado, log);
    }
}

