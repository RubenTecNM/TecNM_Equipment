using Mediator;

namespace TecnmInventario.Api.Features.Incidents.CreateIncident;

public sealed record CreateIncidentCommand(
    long AssetId,
    long IncidentTypeId,
    long IncidentPriorityId,
    string Title,
    string? Description) : ICommand<CreateIncidentResponse>;
