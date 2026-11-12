namespace TecnmInventario.Api.Features.Maintenance.CompleteMaintenance;

public sealed record CompleteMaintenanceResponse(
    long MaintenanceRecordId,
    string Status,
    long AssetId,
    string AssetStatusCode,
    bool Released,
    int OpenIncidents,
    DateTime PerformedAt,
    int Version);
