using Mediator;

namespace TecnmInventario.Api.Features.Maintenance.CancelMaintenance;

/// <summary>Cancela un mantenimiento PROGRAMADO o EN_CURSO. Transacción propia (regla 19).</summary>
public sealed record CancelMaintenanceCommand(
    long MaintenanceRecordId,
    int ExpectedVersion,
    string Reason) : ICommand<CancelMaintenanceResponse>;
