using Mediator;

namespace TecnmInventario.Api.Features.Locations.GetLocations;

public sealed record GetLocationsQuery : IQuery<IReadOnlyList<GetLocationsResponse>>;
