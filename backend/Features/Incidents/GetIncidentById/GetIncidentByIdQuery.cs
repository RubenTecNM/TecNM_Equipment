using Mediator;

namespace TecnmInventario.Api.Features.Incidents.GetIncidentById;

public sealed record GetIncidentByIdQuery(long IncidentId) : IQuery<GetIncidentByIdResponse>;
