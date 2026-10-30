using Mediator;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TecnmInventario.Api.Common.Pagination;
using TecnmInventario.Api.Features.Incidents.CreateIncident;
using TecnmInventario.Api.Features.Incidents.GetIncidentById;
using TecnmInventario.Api.Features.Incidents.GetIncidents;
using TecnmInventario.Api.Features.Incidents.AssignIncident;
using TecnmInventario.Api.Features.Incidents.UpdateIncidentStatus;
using TecnmInventario.Api.Features.Incidents.ResolveIncident;
using TecnmInventario.Api.Features.Incidents.DeleteIncident;

namespace TecnmInventario.Api.Features.Incidents;

[ApiController]
[Route("api/[controller]")]
public class IncidentsController : ControllerBase
{
    private readonly IMediator _mediator;

    public IncidentsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    public sealed record IncidentFilters(
        long? AssetId,
        string? Status,
        string? Priority,
        long? IncidentTypeId,
        long? AssignedTo,
        bool? OnlyOpen,
        string? Search);

    [HttpGet]
    [Authorize(Policy = "incidents.view")]
    public async Task<ActionResult<PagedResult<GetIncidentsResponse>>> GetIncidents(
        [FromQuery] PagedRequest pagination,
        [FromQuery] IncidentFilters filters,
        CancellationToken cancellationToken)
    {
        var query = new GetIncidentsQuery(
            pagination.PageNumber, pagination.PageSize,
            filters.AssetId, filters.Status, filters.Priority,
            filters.IncidentTypeId, filters.AssignedTo, filters.OnlyOpen, filters.Search);

        var result = await _mediator.Send(query, cancellationToken);
        return Ok(result);
    }

    [HttpGet("{incidentId:long}")]
    [Authorize(Policy = "incidents.view")]
    public async Task<ActionResult<GetIncidentByIdResponse>> GetIncidentById(
        long incidentId,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetIncidentByIdQuery(incidentId), cancellationToken);
        return Ok(result);
    }

    [HttpPost]
    [Authorize(Policy = "incidents.create")]
    public async Task<ActionResult<CreateIncidentResponse>> CreateIncident(
        CreateIncidentCommand command,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(command, cancellationToken);
        return CreatedAtAction(nameof(GetIncidentById), new { incidentId = result.IncidentId }, result);
    }

    public sealed record AssignIncidentBody(long AssignedToUserId);

    [HttpPut("{incidentId:long}/assignment")]
    [Authorize(Policy = "incidents.resolve")]
    public async Task<ActionResult<AssignIncidentResponse>> AssignIncident(
        long incidentId,
        [FromQuery] int expectedVersion,
        AssignIncidentBody body,
        CancellationToken cancellationToken)
    {
        var command = new AssignIncidentCommand(incidentId, expectedVersion, body.AssignedToUserId);
        var result = await _mediator.Send(command, cancellationToken);
        return Ok(result);
    }

    public sealed record UpdateIncidentStatusBody(string NewStatusCode, string? Reason);

    [HttpPatch("{incidentId:long}/status")]
    [Authorize(Policy = "incidents.resolve")]
    public async Task<ActionResult<UpdateIncidentStatusResponse>> UpdateIncidentStatus(
        long incidentId,
        [FromQuery] int expectedVersion,
        UpdateIncidentStatusBody body,
        CancellationToken cancellationToken)
    {
        var command = new UpdateIncidentStatusCommand(incidentId, expectedVersion, body.NewStatusCode, body.Reason);
        var result = await _mediator.Send(command, cancellationToken);
        return Ok(result);
    }

    public sealed record ResolveIncidentBody(string Solution, string? Diagnosis);

    [HttpPost("{incidentId:long}/resolve")]
    [Authorize(Policy = "incidents.resolve")]
    public async Task<ActionResult<ResolveIncidentResponse>> ResolveIncident(
        long incidentId,
        [FromQuery] int expectedVersion,
        ResolveIncidentBody body,
        CancellationToken cancellationToken)
    {
        var command = new ResolveIncidentCommand(incidentId, expectedVersion, body.Solution, body.Diagnosis);
        var result = await _mediator.Send(command, cancellationToken);
        return Ok(result);
    }

    [HttpDelete("{incidentId:long}")]
    [Authorize(Policy = "incidents.delete")]
    public async Task<ActionResult<DeleteIncidentResponse>> DeleteIncident(
        long incidentId,
        [FromQuery] int expectedVersion,
        CancellationToken cancellationToken)
    {
        var command = new DeleteIncidentCommand(incidentId, expectedVersion);
        var result = await _mediator.Send(command, cancellationToken);
        return Ok(result);
    }
}
