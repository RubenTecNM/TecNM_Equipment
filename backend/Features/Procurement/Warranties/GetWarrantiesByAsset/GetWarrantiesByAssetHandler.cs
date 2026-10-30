using Mediator;
using Microsoft.EntityFrameworkCore;
using TecnmInventario.Api.Data;

namespace TecnmInventario.Api.Features.Procurement.Warranties.GetWarrantiesByAsset;

public sealed class GetWarrantiesByAssetHandler
    : IQueryHandler<GetWarrantiesByAssetQuery, IReadOnlyList<GetWarrantiesByAssetResponse>>
{
    private readonly TecnmInventarioDbContext _context;

    public GetWarrantiesByAssetHandler(TecnmInventarioDbContext context)
    {
        _context = context;
    }

    public async ValueTask<IReadOnlyList<GetWarrantiesByAssetResponse>> Handle(
        GetWarrantiesByAssetQuery query, CancellationToken cancellationToken)
    {
        return await _context.AssetWarranties
            .Where(w => w.AssetId == query.AssetId)
            .OrderByDescending(w => w.StartsOn)
            .Select(w => new GetWarrantiesByAssetResponse(
                w.WarrantyId, w.SupplierId, w.WarrantyNumber, w.StartsOn, w.EndsOn, w.Coverage, w.Version))
            .ToListAsync(cancellationToken);
    }
}
