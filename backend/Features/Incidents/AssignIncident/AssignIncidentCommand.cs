using Mediator;

namespace TecnmInventario.Api.Features.Incidents.AssignIncident;

public sealed record AssignIncidentCommand(
    long IncidentId,
    int ExpectedVersion,
    long AssignedToUserId) : ICommand<AssignIncidentResponse>;
