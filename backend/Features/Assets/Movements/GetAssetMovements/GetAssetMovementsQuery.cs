using Mediator;
using TecnmInventario.Api.Common.Pagination;

namespace TecnmInventario.Api.Features.Assets.Movements.GetAssetMovements;

public sealed record GetAssetMovementsQuery(
    long AssetId,
    int PageNumber,
    int PageSize)
    : IQuery<PagedResult<GetAssetMovementsResponse>>;
