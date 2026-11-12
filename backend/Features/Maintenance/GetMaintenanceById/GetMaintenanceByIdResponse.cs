using TecnmInventario.Api.Features.Maintenance.Shared;

namespace TecnmInventario.Api.Features.Maintenance.GetMaintenanceById;

public sealed record MaintenanceStatusRef(string Code, string Name);

public sealed record MaintenanceIncidentRef(long Id, string Status, string Title);

/// <summary>Detalle: todo lo del ítem del listado más los campos del cierre. Sin correos.</summary>
public sealed record GetMaintenanceByIdResponse(
    long MaintenanceRecordId,
    string Status,
    long AssetId,
    string AssetInventoryCode,
    string? AssetHostname,
    bool AssetIsDeleted,
    string MaintenanceTypeCode,
    string MaintenanceTypeName,
    MaintenanceUserRef? PerformedBy,
    DateTime? ScheduledAt,
    DateTime? StartedAt,
    DateTime? PerformedAt,
    DateTime? NextMaintenanceAt,
    long? IncidentId,
    int Version,
    string? Description,
    string? PartsUsed,
    decimal? Cost,
    MaintenanceStatusRef? StatusBefore,
    MaintenanceStatusRef? StatusAfter,
    MaintenanceIncidentRef? Incident,
    DateTime? CancelledAt,
    string? CancellationReason,
    DateTime CreatedAt,
    MaintenanceUserRef? CreatedBy);
