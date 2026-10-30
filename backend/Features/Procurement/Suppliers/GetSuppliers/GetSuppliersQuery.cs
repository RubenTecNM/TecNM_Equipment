using Mediator;
using TecnmInventario.Api.Common.Pagination;

namespace TecnmInventario.Api.Features.Procurement.Suppliers.GetSuppliers;

public sealed record GetSuppliersQuery(int PageNumber, int PageSize)
    : IQuery<PagedResult<GetSuppliersResponse>>;
