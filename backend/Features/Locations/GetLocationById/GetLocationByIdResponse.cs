namespace TecnmInventario.Api.Features.Locations.GetLocationById;

public sealed record GetLocationByIdResponse(
    long LocationId,
    string Code,
    string Name,
    string LocationTypeCode,
    long? ParentLocationId,
    IReadOnlyList<string> Breadcrumb,
    int Version);
