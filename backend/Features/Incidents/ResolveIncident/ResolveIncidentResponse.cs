namespace TecnmInventario.Api.Features.Incidents.ResolveIncident;

public sealed record ResolveIncidentResponse(
    long IncidentId,
    string IncidentStatusCode,
    string AssetStatusCode,
    bool AssetReleased,
    int OpenIncidentsRemaining,
    DateTime? ResolvedAt,
    int Version);
