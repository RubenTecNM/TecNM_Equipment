namespace TecnmInventario.Api.Features.Maintenance.StartDirectMaintenance;

public sealed record StartDirectMaintenanceResponse(
    long MaintenanceRecordId,
    string Status,
    long AssetId,
    string AssetStatusCode,
    DateTime StartedAt,
    int Version);
