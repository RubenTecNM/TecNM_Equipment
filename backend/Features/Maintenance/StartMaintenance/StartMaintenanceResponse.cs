namespace TecnmInventario.Api.Features.Maintenance.StartMaintenance;

public sealed record StartMaintenanceResponse(
    long MaintenanceRecordId,
    string Status,
    long AssetId,
    string AssetStatusCode,
    DateTime StartedAt,
    int Version);
