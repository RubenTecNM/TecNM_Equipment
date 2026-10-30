namespace TecnmInventario.Api.Features.Incidents.GetIncidents;

public sealed record GetIncidentsResponse(
    long IncidentId,
    long AssetId,
    string AssetInventoryCode,
    string Title,
    string TypeCode,
    string TypeName,
    string PriorityCode,
    string PriorityName,
    string StatusCode,
    string StatusName,
    string ReportedByUsername,
    string? AssignedToUsername,
    DateTime ReportedAt,
    DateTime? ResolvedAt,
    int Version);
