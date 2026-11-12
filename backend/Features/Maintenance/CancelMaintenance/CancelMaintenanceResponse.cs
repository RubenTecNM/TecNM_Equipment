namespace TecnmInventario.Api.Features.Maintenance.CancelMaintenance;

public sealed record CancelMaintenanceResponse(
    long MaintenanceRecordId,
    string Status,
    long AssetId,
    string AssetStatusCode,
    bool Released,
    int OpenIncidents,
    DateTime CancelledAt,
    int Version);
