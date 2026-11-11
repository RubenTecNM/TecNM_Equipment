using Mediator;

namespace TecnmInventario.Api.Features.Maintenance.StartDirectMaintenance;

/// <summary>
/// Inicia un mantenimiento sin haberlo programado: el registro nace EN_CURSO.
/// Gestiona su propia transacción (regla 19): NO es ITransactionalCommand.
/// </summary>
public sealed record StartDirectMaintenanceCommand(
    long AssetId,
    long MaintenanceTypeId,
    long? PerformedBy,
    string? Description,
    long? IncidentId) : ICommand<StartDirectMaintenanceResponse>;
