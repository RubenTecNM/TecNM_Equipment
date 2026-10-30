using Mediator;
using Microsoft.EntityFrameworkCore;
using TecnmInventario.Api.Common.Pagination;
using TecnmInventario.Api.Data;

namespace TecnmInventario.Api.Features.Procurement.Suppliers.GetSuppliers;

public sealed class GetSuppliersHandler
    : IQueryHandler<GetSuppliersQuery, PagedResult<GetSuppliersResponse>>
{
    private readonly TecnmInventarioDbContext _context;

    public GetSuppliersHandler(TecnmInventarioDbContext context)
    {
        _context = context;
    }

    public async ValueTask<PagedResult<GetSuppliersResponse>> Handle(
        GetSuppliersQuery query, CancellationToken cancellationToken)
    {
        // El clamping de PageSize (tope 100, mínimo 1) ya lo aplica el
        // PagedRequest usado por el controller; aquí solo se consume.
        var baseQuery = _context.Suppliers.OrderBy(s => s.Name);

        var totalCount = await baseQuery.CountAsync(cancellationToken);

        var items = await baseQuery
            .Skip((query.PageNumber - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(s => new GetSuppliersResponse(
                s.SupplierId, s.Name, s.TaxId, s.ContactName, s.Email, s.Phone, s.Version))
            .ToListAsync(cancellationToken);

        return new PagedResult<GetSuppliersResponse>(items, query.PageNumber, query.PageSize, totalCount);
    }
}
