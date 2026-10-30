using Mediator;
using Microsoft.EntityFrameworkCore;
using TecnmInventario.Api.Common.Pagination;
using TecnmInventario.Api.Data;

namespace TecnmInventario.Api.Features.Assets.Movements.GetAssetMovements;

public sealed class GetAssetMovementsHandler
    : IQueryHandler<GetAssetMovementsQuery, PagedResult<GetAssetMovementsResponse>>
{
    private readonly TecnmInventarioDbContext _context;

    public GetAssetMovementsHandler(TecnmInventarioDbContext context)
    {
        _context = context;
    }

    public async ValueTask<PagedResult<GetAssetMovementsResponse>> Handle(
        GetAssetMovementsQuery query, CancellationToken cancellationToken)
    {
        var assetExists = await _context.Assets
            .AnyAsync(a => a.AssetId == query.AssetId && !a.IsDeleted, cancellationToken);

        if (!assetExists)
        {
            throw new KeyNotFoundException("El activo solicitado no existe.");
        }

        var baseQuery = _context.AssetMovements
            .Where(m => m.AssetId == query.AssetId)
            .OrderByDescending(m => m.MovedAt)
            .ThenByDescending(m => m.AssetMovementId);

        var totalCount = await baseQuery.CountAsync(cancellationToken);

        var items = await baseQuery
            .Include(m => m.FromLocation)
            .Include(m => m.ToLocation)
            .Include(m => m.MovementReason)
            .Include(m => m.MovedByNavigation)
            .Skip((query.PageNumber - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(m => new GetAssetMovementsResponse(
                m.AssetMovementId,
                m.FromLocationId,
                m.FromLocation != null ? m.FromLocation.Name : null,
                m.ToLocationId,
                m.ToLocation.Name,
                m.MovementReasonId,
                m.MovementReason.Code,
                m.MovementReason.Name,
                m.MovedBy,
                m.MovedByNavigation.Username,
                m.MovedAt,
                m.Notes))
            .ToListAsync(cancellationToken);

        return new PagedResult<GetAssetMovementsResponse>(
            items, query.PageNumber, query.PageSize, totalCount);
    }
}
