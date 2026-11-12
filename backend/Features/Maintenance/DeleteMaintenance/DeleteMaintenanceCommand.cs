using Mediator;

namespace TecnmInventario.Api.Features.Maintenance.DeleteMaintenance;

/// <summary>
/// Baja lógica de un registro creado por error (regla 20). ISoftDeleteAuditor gestiona
/// su propia transacción y la auditoría DELETE: NO es ITransactionalCommand.
/// </summary>
public sealed record DeleteMaintenanceCommand(long MaintenanceRecordId, int ExpectedVersion)
    : ICommand<DeleteMaintenanceResponse>;
