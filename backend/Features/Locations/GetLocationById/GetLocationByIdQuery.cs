using Mediator;

namespace TecnmInventario.Api.Features.Locations.GetLocationById;

public sealed record GetLocationByIdQuery(long LocationId) : IQuery<GetLocationByIdResponse>;
