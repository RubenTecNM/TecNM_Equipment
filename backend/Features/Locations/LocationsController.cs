using Mediator;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TecnmInventario.Api.Features.Locations.CreateLocation;
using TecnmInventario.Api.Features.Locations.DeleteLocation;
using TecnmInventario.Api.Features.Locations.GetLocationById;
using TecnmInventario.Api.Features.Locations.GetLocations;
using TecnmInventario.Api.Features.Locations.UpdateLocation;

namespace TecnmInventario.Api.Features.Locations;

[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = "locations.manage")]
public class LocationsController : ControllerBase
{
    private readonly IMediator _mediator;

    public LocationsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<GetLocationsResponse>>> GetLocations(
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetLocationsQuery(), cancellationToken);
        return Ok(result);
    }

    [HttpGet("{locationId:long}")]
    public async Task<ActionResult<GetLocationByIdResponse>> GetLocationById(
        long locationId,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetLocationByIdQuery(locationId), cancellationToken);
        return Ok(result);
    }

    [HttpPost]
    public async Task<ActionResult<CreateLocationResponse>> CreateLocation(
        CreateLocationCommand command,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(command, cancellationToken);
        return CreatedAtAction(nameof(GetLocationById), new { locationId = result.LocationId }, result);
    }

    public sealed record UpdateLocationBody(string Name, long LocationTypeId, long? ParentLocationId);

    [HttpPut("{locationId:long}")]
    public async Task<ActionResult<UpdateLocationResponse>> UpdateLocation(
        long locationId,
        [FromQuery] int expectedVersion,
        UpdateLocationBody body,
        CancellationToken cancellationToken)
    {
        var command = new UpdateLocationCommand(
            locationId, expectedVersion, body.Name, body.LocationTypeId, body.ParentLocationId);

        var result = await _mediator.Send(command, cancellationToken);
        return Ok(result);
    }

    [HttpDelete("{locationId:long}")]
    public async Task<ActionResult<DeleteLocationResponse>> DeleteLocation(
        long locationId,
        [FromQuery] int expectedVersion,
        CancellationToken cancellationToken)
    {
        var command = new DeleteLocationCommand(locationId, expectedVersion);
        var result = await _mediator.Send(command, cancellationToken);
        return Ok(result);
    }
}
