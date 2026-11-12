using Mediator;
using TecnmInventario.Api.Common.Behaviors;

namespace TecnmInventario.Api.Features.Maintenance.UpdateMaintenance;

/// <summary>
/// Edita un mantenimiento PROGRAMADO (scheduledAt, maintenanceTypeId, performedBy,
/// description) o EN_CURSO (performedBy, description). El body es el estado deseado
/// de los campos editables: performedBy y description null/omitidos los LIMPIAN;
/// scheduledAt y maintenanceTypeId omitidos significan "sin cambio" (son obligatorios
/// en BD). Un campo no editable en el estado actual con un valor distinto es 400.
/// Solo toca el registro (sin AssetRowLock): ver el comentario del handler.
/// </summary>
public sealed record UpdateMaintenanceCommand(
    long MaintenanceRecordId,
    int ExpectedVersion,
    DateTime? ScheduledAt,
    long? MaintenanceTypeId,
    long? PerformedBy,
    string? Description) : ICommand<UpdateMaintenanceResponse>, ITransactionalCommand;
