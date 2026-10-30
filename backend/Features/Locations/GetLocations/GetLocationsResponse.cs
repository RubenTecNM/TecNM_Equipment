namespace TecnmInventario.Api.Features.Locations.GetLocations;

public sealed record GetLocationsResponse(
    long LocationId,
    string Code,
    string Name,
    string LocationTypeCode,
    long? ParentLocationId,
    int Version);
