using Mediator;
using TecnmInventario.Api.Common.Pagination;

namespace TecnmInventario.Api.Features.Procurement.Acquisitions.GetAcquisitions;

public sealed record GetAcquisitionsQuery(int PageNumber, int PageSize)
    : IQuery<PagedResult<GetAcquisitionsResponse>>;
