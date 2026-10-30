namespace TecnmInventario.Api.Features.Assets.GetAssetById;

public sealed record GetAssetByIdResponse(
    long AssetId,
    string InventoryCode,
    string AssetTypeCode,
    string StatusCode,
    string? ManufacturerName,
    string? ModelName,
    long? CurrentLocationId,
    string? SerialNumber,
    string? Hostname,
    string? Notes,
    int Version);
