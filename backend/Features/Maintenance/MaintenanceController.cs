using Mediator;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TecnmInventario.Api.Features.Maintenance.CancelMaintenance;
using TecnmInventario.Api.Features.Maintenance.CompleteMaintenance;
using TecnmInventario.Api.Features.Maintenance.ScheduleMaintenance;
using TecnmInventario.Api.Features.Maintenance.Shared;
using TecnmInventario.Api.Features.Maintenance.StartDirectMaintenance;
using TecnmInventario.Api.Features.Maintenance.StartMaintenance;

namespace TecnmInventario.Api.Features.Maintenance;

/// <summary>
/// Flujo programar -> iniciar -> cerrar/cancelar. Solo despacha a IMediator.
/// Todas las escrituras exigen maintenance.manage (política por permiso, regla 21).
/// Las consultas (listado y detalle, maintenance.view) llegan en M.4.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class MaintenanceController : ControllerBase
{
    private readonly IMediator _mediator;

    public MaintenanceController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpPost]
    [Authorize(Policy = MaintenancePermissions.Manage)]
    public async Task<ActionResult<ScheduleMaintenanceResponse>> Schedule(
        ScheduleMaintenanceCommand command,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(command, cancellationToken);
        // GET /api/maintenance/{id} llega en M.4; la URL ya es la definitiva.
        return Created($"/api/maintenance/{result.MaintenanceRecordId}", result);
    }

    [HttpPost("start")]
    [Authorize(Policy = MaintenancePermissions.Manage)]
    public async Task<ActionResult<StartDirectMaintenanceResponse>> StartDirect(
        StartDirectMaintenanceCommand command,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(command, cancellationToken);
        return Created($"/api/maintenance/{result.MaintenanceRecordId}", result);
    }

    public sealed record StartBody(int ExpectedVersion, long? IncidentId);

    [HttpPost("{maintenanceId:long}/start")]
    [Authorize(Policy = MaintenancePermissions.Manage)]
    public async Task<ActionResult<StartMaintenanceResponse>> Start(
        long maintenanceId,
        StartBody body,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(
            new StartMaintenanceCommand(maintenanceId, body.ExpectedVersion, body.IncidentId), cancellationToken);
        return Ok(result);
    }

    public sealed record CompleteBody(
        int ExpectedVersion,
        string? Description,
        string? PartsUsed,
        decimal? Cost,
        DateTime? NextMaintenanceAt);

    [HttpPost("{maintenanceId:long}/complete")]
    [Authorize(Policy = MaintenancePermissions.Manage)]
    public async Task<ActionResult<CompleteMaintenanceResponse>> Complete(
        long maintenanceId,
        CompleteBody body,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(
            new CompleteMaintenanceCommand(
                maintenanceId, body.ExpectedVersion, body.Description, body.PartsUsed, body.Cost, body.NextMaintenanceAt),
            cancellationToken);
        return Ok(result);
    }

    public sealed record CancelBody(int ExpectedVersion, string Reason);

    [HttpPost("{maintenanceId:long}/cancel")]
    [Authorize(Policy = MaintenancePermissions.Manage)]
    public async Task<ActionResult<CancelMaintenanceResponse>> Cancel(
        long maintenanceId,
        CancelBody body,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(
            new CancelMaintenanceCommand(maintenanceId, body.ExpectedVersion, body.Reason), cancellationToken);
        return Ok(result);
    }
}
