namespace TecnmInventario.Api.Features.Assets.GetAssets;

public sealed record GetAssetsResponse(
    long AssetId,
    string InventoryCode,
    string? SerialNumber,
    string? Hostname,
    int Version);
