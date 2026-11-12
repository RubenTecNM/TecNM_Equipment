using Mediator;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TecnmInventario.Api.Features.Maintenance.CancelMaintenance;
using TecnmInventario.Api.Common.Pagination;
using TecnmInventario.Api.Features.Maintenance.CompleteMaintenance;
using TecnmInventario.Api.Features.Maintenance.DeleteMaintenance;
using TecnmInventario.Api.Features.Maintenance.GetMaintenance;
using TecnmInventario.Api.Features.Maintenance.GetMaintenanceById;
using TecnmInventario.Api.Features.Maintenance.GetMaintenanceSchedule;
using TecnmInventario.Api.Features.Maintenance.GetMaintenanceTechnicians;
using TecnmInventario.Api.Features.Maintenance.ScheduleMaintenance;
using TecnmInventario.Api.Features.Maintenance.Shared;
using TecnmInventario.Api.Features.Maintenance.StartDirectMaintenance;
using TecnmInventario.Api.Features.Maintenance.StartMaintenance;
using TecnmInventario.Api.Features.Maintenance.UpdateMaintenance;

namespace TecnmInventario.Api.Features.Maintenance;

/// <summary>
/// Flujo programar -> iniciar -> cerrar/cancelar. Solo despacha a IMediator.
/// Las escrituras exigen maintenance.manage; borrar exige maintenance.delete (política por permiso, regla 21).
/// Las consultas exigen maintenance.view (la de técnicos, maintenance.manage).
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

    public sealed record MaintenanceFilters(
        long? AssetId,
        string[]? Status,
        long? MaintenanceTypeId,
        long? PerformedBy,
        DateTime? From,
        DateTime? To,
        string? Search,
        bool? OnlyOpen);

    [HttpGet]
    [Authorize(Policy = MaintenancePermissions.View)]
    public async Task<ActionResult<PagedResult<MaintenanceListItem>>> GetMaintenance(
        [FromQuery] PagedRequest pagination,
        [FromQuery] MaintenanceFilters filters,
        CancellationToken cancellationToken)
    {
        var query = new GetMaintenanceQuery(
            pagination.PageNumber, pagination.PageSize, filters.AssetId, filters.Status,
            filters.MaintenanceTypeId, filters.PerformedBy, filters.From, filters.To,
            filters.Search, filters.OnlyOpen);

        return Ok(await _mediator.Send(query, cancellationToken));
    }

    // Las rutas literales (schedule, technicians) no chocan con {maintenanceId:long}.
    [HttpGet("schedule")]
    [Authorize(Policy = MaintenancePermissions.View)]
    public async Task<ActionResult<GetMaintenanceScheduleResponse>> GetSchedule(
        [FromQuery] int? days,
        CancellationToken cancellationToken)
        => Ok(await _mediator.Send(new GetMaintenanceScheduleQuery(days), cancellationToken));

    [HttpGet("technicians")]
    [Authorize(Policy = MaintenancePermissions.Manage)]
    public async Task<ActionResult<IReadOnlyList<MaintenanceTechnicianResponse>>> GetTechnicians(
        CancellationToken cancellationToken)
        => Ok(await _mediator.Send(new GetMaintenanceTechniciansQuery(), cancellationToken));

    [HttpGet("{maintenanceId:long}")]
    [Authorize(Policy = MaintenancePermissions.View)]
    public async Task<ActionResult<GetMaintenanceByIdResponse>> GetMaintenanceById(
        long maintenanceId,
        CancellationToken cancellationToken)
        => Ok(await _mediator.Send(new GetMaintenanceByIdQuery(maintenanceId), cancellationToken));

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

    public sealed record UpdateBody(
        int ExpectedVersion,
        DateTime? ScheduledAt,
        long? MaintenanceTypeId,
        long? PerformedBy,
        string? Description);

    [HttpPut("{maintenanceId:long}")]
    [Authorize(Policy = MaintenancePermissions.Manage)]
    public async Task<ActionResult<UpdateMaintenanceResponse>> Update(
        long maintenanceId,
        UpdateBody body,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(
            new UpdateMaintenanceCommand(
                maintenanceId, body.ExpectedVersion, body.ScheduledAt, body.MaintenanceTypeId,
                body.PerformedBy, body.Description),
            cancellationToken);
        return Ok(result);
    }

    [HttpDelete("{maintenanceId:long}")]
    [Authorize(Policy = MaintenancePermissions.Delete)]
    public async Task<ActionResult<DeleteMaintenanceResponse>> Delete(
        long maintenanceId,
        [FromQuery] int expectedVersion,
        CancellationToken cancellationToken)
        => Ok(await _mediator.Send(new DeleteMaintenanceCommand(maintenanceId, expectedVersion), cancellationToken));
}
