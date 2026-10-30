namespace TecnmInventario.Api.Features.Assets.CreateAsset;

public sealed record CreateAssetResponse(
    long AssetId,
    string InventoryCode,
    string AssetTypeCode,
    string StatusCode);
