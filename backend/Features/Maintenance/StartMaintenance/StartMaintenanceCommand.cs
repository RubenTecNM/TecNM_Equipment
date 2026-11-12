using Mediator;

namespace TecnmInventario.Api.Features.Maintenance.StartMaintenance;

/// <summary>
/// Inicia un mantenimiento PROGRAMADO. Gestiona su propia transacción (regla 19),
/// por eso NO implementa ITransactionalCommand.
/// </summary>
public sealed record StartMaintenanceCommand(
    long MaintenanceRecordId,
    int ExpectedVersion,
    long? IncidentId) : ICommand<StartMaintenanceResponse>;
