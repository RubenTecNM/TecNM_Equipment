using Mediator;
using TecnmInventario.Api.Common.Pagination;

namespace TecnmInventario.Api.Features.Assets.GetAssets;

public sealed record GetAssetsQuery(int PageNumber, int PageSize)
    : IQuery<PagedResult<GetAssetsResponse>>;
