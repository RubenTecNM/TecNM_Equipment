using Mediator;

namespace TecnmInventario.Api.Features.Procurement.Warranties.GetWarrantiesByAsset;

public sealed record GetWarrantiesByAssetQuery(long AssetId)
    : IQuery<IReadOnlyList<GetWarrantiesByAssetResponse>>;
