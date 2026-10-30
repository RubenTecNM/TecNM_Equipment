using Mediator;

namespace TecnmInventario.Api.Features.Catalogs.GetModelsByManufacturer;

public sealed record GetModelsByManufacturerQuery(long ManufacturerId, long? AssetTypeId)
    : IQuery<IReadOnlyList<GetModelsByManufacturerResponse>>;
