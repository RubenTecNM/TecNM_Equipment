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
        var asset = await _context.Assets
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
