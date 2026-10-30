using Mediator;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TecnmInventario.Api.Features.Procurement.Warranties.CreateAssetWarranty;
using TecnmInventario.Api.Features.Procurement.Warranties.DeleteAssetWarranty;

namespace TecnmInventario.Api.Features.Procurement.Warranties;

[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = "procurement.manage")]
public class WarrantiesController : ControllerBase
{
    private readonly IMediator _mediator;

    public WarrantiesController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpPost]
    public async Task<ActionResult<CreateAssetWarrantyResponse>> CreateAssetWarranty(
        CreateAssetWarrantyCommand command,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(command, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, result);
    }

    [HttpDelete("{warrantyId:long}")]
    public async Task<ActionResult<DeleteAssetWarrantyResponse>> DeleteAssetWarranty(
        long warrantyId,
        [FromQuery] int expectedVersion,
        CancellationToken cancellationToken)
    {
        var command = new DeleteAssetWarrantyCommand(warrantyId, expectedVersion);
        var result = await _mediator.Send(command, cancellationToken);
        return Ok(result);
    }
}
