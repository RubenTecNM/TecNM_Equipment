using Mediator;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TecnmInventario.Api.Common.Pagination;
using TecnmInventario.Api.Features.Procurement.Suppliers.CreateSupplier;
using TecnmInventario.Api.Features.Procurement.Suppliers.DeleteSupplier;
using TecnmInventario.Api.Features.Procurement.Suppliers.GetSuppliers;
using TecnmInventario.Api.Features.Procurement.Suppliers.UpdateSupplier;

namespace TecnmInventario.Api.Features.Procurement.Suppliers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = "procurement.manage")]
public class SuppliersController : ControllerBase
{
    private readonly IMediator _mediator;

    public SuppliersController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    public async Task<ActionResult<PagedResult<GetSuppliersResponse>>> GetSuppliers(
        [FromQuery] PagedRequest pagination,
        CancellationToken cancellationToken)
    {
        var query = new GetSuppliersQuery(pagination.PageNumber, pagination.PageSize);
        var result = await _mediator.Send(query, cancellationToken);
        return Ok(result);
    }

    [HttpPost]
    public async Task<ActionResult<CreateSupplierResponse>> CreateSupplier(
        CreateSupplierCommand command,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(command, cancellationToken);
        return CreatedAtAction(nameof(GetSuppliers), new { }, result);
    }

    public sealed record UpdateSupplierBody(
        string Name, string? TaxId, string? ContactName,
        string? Email, string? Phone, string? Address, string? Notes);

    [HttpPut("{supplierId:long}")]
    public async Task<ActionResult<UpdateSupplierResponse>> UpdateSupplier(
        long supplierId,
        [FromQuery] int expectedVersion,
        UpdateSupplierBody body,
        CancellationToken cancellationToken)
    {
        var command = new UpdateSupplierCommand(
            supplierId, expectedVersion, body.Name, body.TaxId,
            body.ContactName, body.Email, body.Phone, body.Address, body.Notes);

        var result = await _mediator.Send(command, cancellationToken);
        return Ok(result);
    }

    [HttpDelete("{supplierId:long}")]
    public async Task<ActionResult<DeleteSupplierResponse>> DeleteSupplier(
        long supplierId,
        [FromQuery] int expectedVersion,
        CancellationToken cancellationToken)
    {
        var command = new DeleteSupplierCommand(supplierId, expectedVersion);
        var result = await _mediator.Send(command, cancellationToken);
        return Ok(result);
    }
}
