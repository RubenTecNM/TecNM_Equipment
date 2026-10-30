namespace TecnmInventario.Api.Features.Incidents.CreateIncident;

public sealed record CreateIncidentResponse(
    long IncidentId,
    long AssetId,
    string IncidentStatusCode,
    string AssetStatusCode,
    DateTime ReportedAt,
    int Version);
