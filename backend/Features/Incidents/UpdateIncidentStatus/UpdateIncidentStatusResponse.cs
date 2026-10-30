namespace TecnmInventario.Api.Features.Incidents.UpdateIncidentStatus;

public sealed record UpdateIncidentStatusResponse(
    long IncidentId,
    string IncidentStatusCode,
    string AssetStatusCode,
    int Version);
