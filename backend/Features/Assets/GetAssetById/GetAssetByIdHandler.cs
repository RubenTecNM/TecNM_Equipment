using Mediator;
using Microsoft.EntityFrameworkCore;
using TecnmInventario.Api.Data;

namespace TecnmInventario.Api.Features.Assets.GetAssetById;

public sealed class GetAssetByIdHandler : IQueryHandler<GetAssetByIdQuery, GetAssetByIdResponse>
{
    private readonly TecnmInventarioDbContext _context;

    public GetAssetByIdHandler(TecnmInventarioDbContext context)
    {
        _context = context;
    }

    public async ValueTask<GetAssetByIdResponse> Handle(
        GetAssetByIdQuery query, CancellationToken cancellationToken)
    {
        // D5: el nombre de Manufacturer/Model es un dato real del activo y se
        // muestra aunque el fabricante o el modelo estén borrados lógicamente.
        // IgnoreQueryFilters quita el filtro "SoftDelete" de TODA la consulta
        // (D2), incluido el del propio Asset, por eso el Where explícito.
        var asset = await _context.Assets
            .IgnoreQueryFilters(["SoftDelete"])
            .Include(a => a.AssetType)
            .Include(a => a.AssetStatus)
            .Include(a => a.Manufacturer)
            .Include(a => a.Model)
            .FirstOrDefaultAsync(a => a.AssetId == query.AssetId && !a.IsDeleted, cancellationToken);

        if (asset is null)
        {
            throw new KeyNotFoundException("El activo solicitado no existe.");
        }

        return new GetAssetByIdResponse(
            asset.AssetId,
            asset.InventoryCode,
            asset.AssetType.Code,
            asset.AssetStatus.Code,
            asset.Manufacturer?.Name,
            asset.Model?.ModelName,
            asset.CurrentLocationId,
            asset.SerialNumber,
            asset.Hostname,
            asset.Notes,
            asset.Version);
    }
}
