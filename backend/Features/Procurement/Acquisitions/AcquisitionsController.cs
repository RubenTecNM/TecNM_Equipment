using Mediator;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TecnmInventario.Api.Common.Pagination;
using TecnmInventario.Api.Features.Procurement.Acquisitions.CreateAcquisition;
using TecnmInventario.Api.Features.Procurement.Acquisitions.DeleteAcquisition;
using TecnmInventario.Api.Features.Procurement.Acquisitions.GetAcquisitionById;
using TecnmInventario.Api.Features.Procurement.Acquisitions.GetAcquisitions;

namespace TecnmInventario.Api.Features.Procurement.Acquisitions;

[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = "procurement.manage")]
public class AcquisitionsController : ControllerBase
{
    private readonly IMediator _mediator;

    public AcquisitionsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    public async Task<ActionResult<PagedResult<GetAcquisitionsResponse>>> GetAcquisitions(
        [FromQuery] PagedRequest pagination,
        CancellationToken cancellationToken)
    {
        var query = new GetAcquisitionsQuery(pagination.PageNumber, pagination.PageSize);
        var result = await _mediator.Send(query, cancellationToken);
        return Ok(result);
    }

    [HttpGet("{acquisitionId:long}")]
    public async Task<ActionResult<GetAcquisitionByIdResponse>> GetAcquisitionById(
        long acquisitionId,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetAcquisitionByIdQuery(acquisitionId), cancellationToken);
        return Ok(result);
    }

    [HttpPost]
    public async Task<ActionResult<CreateAcquisitionResponse>> CreateAcquisition(
        CreateAcquisitionCommand command,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(command, cancellationToken);
        return CreatedAtAction(nameof(GetAcquisitionById), new { acquisitionId = result.AcquisitionId }, result);
    }

    [HttpDelete("{acquisitionId:long}")]
    public async Task<ActionResult<DeleteAcquisitionResponse>> DeleteAcquisition(
        long acquisitionId,
        [FromQuery] int expectedVersion,
        CancellationToken cancellationToken)
    {
        var command = new DeleteAcquisitionCommand(acquisitionId, expectedVersion);
        var result = await _mediator.Send(command, cancellationToken);
        return Ok(result);
    }
}
