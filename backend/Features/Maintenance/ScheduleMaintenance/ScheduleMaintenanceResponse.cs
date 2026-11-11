namespace TecnmInventario.Api.Features.Maintenance.ScheduleMaintenance;

public sealed record ScheduleMaintenanceResponse(
    long MaintenanceRecordId,
    string Status,
    long AssetId,
    string AssetStatusCode,
    DateTime ScheduledAt,
    int Version);
