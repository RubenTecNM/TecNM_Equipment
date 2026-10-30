using Mediator;

namespace TecnmInventario.Api.Features.Assets.GetAssetById;

public sealed record GetAssetByIdQuery(long AssetId) : IQuery<GetAssetByIdResponse>;
