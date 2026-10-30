using Mediator;
using Microsoft.EntityFrameworkCore;
using TecnmInventario.Api.Common.Pagination;
using TecnmInventario.Api.Data;

namespace TecnmInventario.Api.Features.Procurement.Acquisitions.GetAcquisitions;

public sealed class GetAcquisitionsHandler
    : IQueryHandler<GetAcquisitionsQuery, PagedResult<GetAcquisitionsResponse>>
{
    private readonly TecnmInventarioDbContext _context;

    public GetAcquisitionsHandler(TecnmInventarioDbContext context)
    {
        _context = context;
    }

    public async ValueTask<PagedResult<GetAcquisitionsResponse>> Handle(
        GetAcquisitionsQuery query, CancellationToken cancellationToken)
    {
        var baseQuery = _context.Acquisitions.OrderByDescending(a => a.AcquiredAt);

        var totalCount = await baseQuery.CountAsync(cancellationToken);

        var items = await baseQuery
            .Skip((query.PageNumber - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(a => new GetAcquisitionsResponse(
                a.AcquisitionId,
                a.SupplierId,
                a.Supplier != null ? a.Supplier.Name : null,
                a.PurchaseOrder,
                a.InvoiceNumber,
                a.AcquiredAt,
                a.TotalAmount,
                a.CurrencyCode,
                a.AcquisitionAssets.Count,
                a.Version))
            .ToListAsync(cancellationToken);

        return new PagedResult<GetAcquisitionsResponse>(items, query.PageNumber, query.PageSize, totalCount);
    }
}
