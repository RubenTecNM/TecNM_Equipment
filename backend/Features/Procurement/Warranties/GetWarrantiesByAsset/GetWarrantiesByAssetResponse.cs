namespace TecnmInventario.Api.Features.Procurement.Warranties.GetWarrantiesByAsset;

public sealed record GetWarrantiesByAssetResponse(
    long WarrantyId,
    long? SupplierId,
    string? WarrantyNumber,
    DateOnly? StartsOn,
    DateOnly? EndsOn,
    string? Coverage,
    int Version);
