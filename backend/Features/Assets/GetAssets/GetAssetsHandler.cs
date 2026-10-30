using Mediator;
using Microsoft.EntityFrameworkCore;
using TecnmInventario.Api.Common.Pagination;
using TecnmInventario.Api.Data;

namespace TecnmInventario.Api.Features.Assets.GetAssets;

public sealed class GetAssetsHandler : IQueryHandler<GetAssetsQuery, PagedResult<GetAssetsResponse>>
{
    private readonly TecnmInventarioDbContext _context;

    public GetAssetsHandler(TecnmInventarioDbContext context)
    {
        _context = context;
    }

    public async ValueTask<PagedResult<GetAssetsResponse>> Handle(
        GetAssetsQuery query, CancellationToken cancellationToken)
    {
        var baseQuery = _context.Assets.Where(a => !a.IsDeleted).OrderBy(a => a.AssetId);

        var totalCount = await baseQuery.CountAsync(cancellationToken);

        var items = await baseQuery
            .Skip((query.PageNumber - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(a => new GetAssetsResponse(a.AssetId, a.InventoryCode, a.SerialNumber, a.Hostname, a.Version))
            .ToListAsync(cancellationToken);

        return new PagedResult<GetAssetsResponse>(items, query.PageNumber, query.PageSize, totalCount);
    }
}
