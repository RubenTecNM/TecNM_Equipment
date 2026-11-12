namespace TecnmInventario.Api.Features.Maintenance.UpdateMaintenance;

public sealed record UpdateMaintenanceResponse(long MaintenanceRecordId, string Status, int Version);
