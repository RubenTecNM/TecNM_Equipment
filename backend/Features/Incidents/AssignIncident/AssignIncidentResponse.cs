namespace TecnmInventario.Api.Features.Incidents.AssignIncident;

public sealed record AssignIncidentResponse(
    long IncidentId,
    long AssignedToId,
    string AssignedToUsername,
    int Version);
