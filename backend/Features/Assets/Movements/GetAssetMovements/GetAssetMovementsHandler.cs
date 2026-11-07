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
        // A6: el historial se devuelve aunque el activo esté borrado lógicamente;
        // el filtro global de Asset se omite intencionalmente aquí.
        var assetExists = await _context.Assets
            .IgnoreQueryFilters(["SoftDelete"])
            .AnyAsync(a => a.AssetId == query.AssetId, cancellationToken);

        if (!assetExists)
        {
            throw new KeyNotFoundException("El activo solicitado no existe.");
        }

        var baseQuery = _context.AssetMovements
            .Where(m => m.AssetId == query.AssetId)
            .OrderByDescending(m => m.MovedAt)
            .ThenByDescending(m => m.AssetMovementId);

        var totalCount = await baseQuery.CountAsync(cancellationToken);

        // D3: el historial de movimientos es inmutable; las ubicaciones borradas
        // lógicamente deben seguir apareciendo con su nombre original.
        // IgnoreQueryFilters aplica a toda la query, incluidas las navegaciones.
        var items = await baseQuery
            .IgnoreQueryFilters(["SoftDelete"])
            .Where(m => m.AssetId == query.AssetId)
            .OrderByDescending(m => m.MovedAt)
            .ThenByDescending(m => m.AssetMovementId)
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
