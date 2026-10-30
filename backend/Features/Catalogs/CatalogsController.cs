using Mediator;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TecnmInventario.Api.Features.Catalogs.GetCatalogs;
using TecnmInventario.Api.Features.Catalogs.GetManufacturers;
using TecnmInventario.Api.Features.Catalogs.GetModelsByManufacturer;

namespace TecnmInventario.Api.Features.Catalogs;

// Catálogos de solo lectura: basta con estar autenticado (sin permiso específico),
// porque cualquier formulario de la aplicación necesita llenar sus desplegables.
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class CatalogsController : ControllerBase
{
    private readonly IMediator _mediator;

    public CatalogsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    public async Task<ActionResult<GetCatalogsResponse>> GetCatalogs(CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetCatalogsQuery(), cancellationToken);
        return Ok(result);
    }

    [HttpGet("manufacturers")]
    public async Task<ActionResult<IReadOnlyList<GetManufacturersResponse>>> GetManufacturers(
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetManufacturersQuery(), cancellationToken);
        return Ok(result);
    }

    [HttpGet("manufacturers/{manufacturerId:long}/models")]
    public async Task<ActionResult<IReadOnlyList<GetModelsByManufacturerResponse>>> GetModelsByManufacturer(
        long manufacturerId,
        [FromQuery] long? assetTypeId,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(
            new GetModelsByManufacturerQuery(manufacturerId, assetTypeId), cancellationToken);
        return Ok(result);
    }
}
