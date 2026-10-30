namespace TecnmInventario.Api.Features.Catalogs.GetModelsByManufacturer;

public sealed record GetModelsByManufacturerResponse(long ModelId, string ModelName, long? AssetTypeId);
