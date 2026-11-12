using Mediator;

namespace TecnmInventario.Api.Features.Maintenance.CompleteMaintenance;

/// <summary>Cierra un mantenimiento EN_CURSO. Transacción propia (regla 19).</summary>
public sealed record CompleteMaintenanceCommand(
    long MaintenanceRecordId,
    int ExpectedVersion,
    string? Description,
    string? PartsUsed,
    decimal? Cost,
    DateTime? NextMaintenanceAt) : ICommand<CompleteMaintenanceResponse>;
