using Mediator;
using TecnmInventario.Api.Common.Behaviors;

namespace TecnmInventario.Api.Features.Maintenance.ScheduleMaintenance;

/// <summary>Programa un mantenimiento (queda PROGRAMADO; no cambia el activo).</summary>
public sealed record ScheduleMaintenanceCommand(
    long AssetId,
    long MaintenanceTypeId,
    DateTime? ScheduledAt,
    long? PerformedBy,
    string? Description) : ICommand<ScheduleMaintenanceResponse>, ITransactionalCommand;
