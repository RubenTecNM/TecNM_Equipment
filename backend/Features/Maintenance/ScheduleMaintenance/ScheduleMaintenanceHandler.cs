using Mediator;
using Microsoft.EntityFrameworkCore;
using TecnmInventario.Api.Common.Audit;
using TecnmInventario.Api.Common.Security;
using TecnmInventario.Api.Core.Entities;
using TecnmInventario.Api.Data;
using TecnmInventario.Api.Features.Maintenance.Shared;

namespace TecnmInventario.Api.Features.Maintenance.ScheduleMaintenance;

/// <summary>
/// Programar NO cambia el activo, así que es una escritura simple (regla 13):
/// ITransactionalCommand + auditoría INSERT; la transacción la abre TransactionBehavior.
/// </summary>
public sealed class ScheduleMaintenanceHandler
    : ICommandHandler<ScheduleMaintenanceCommand, ScheduleMaintenanceResponse>
{
    private readonly TecnmInventarioDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IAuditLogService _auditLog;

    public ScheduleMaintenanceHandler(
        TecnmInventarioDbContext context, ICurrentUserService currentUser, IAuditLogService auditLog)
    {
        _context = context;
        _currentUser = currentUser;
        _auditLog = auditLog;
    }

    public async ValueTask<ScheduleMaintenanceResponse> Handle(
        ScheduleMaintenanceCommand command, CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId ?? throw new UnauthorizedAccessException();

        var record = new MaintenanceRecord
        {
            AssetId = command.AssetId,
            MaintenanceTypeId = command.MaintenanceTypeId,
            // SIEMPRE explícito: no se depende del DEFAULT de la columna.
            Status = MaintenanceStatusCodes.Programado,
            ScheduledAt = MaintenanceDates.ToUtc(command.ScheduledAt!.Value),
            PerformedBy = command.PerformedBy,
            Description = string.IsNullOrWhiteSpace(command.Description) ? null : command.Description.Trim(),
            CreatedBy = userId
        };

        _context.MaintenanceRecords.Add(record);
        await _context.SaveChangesAsync(cancellationToken);

        var assetStatusCode = await _context.Assets
            .Where(a => a.AssetId == record.AssetId)
            .Select(a => a.AssetStatus.Code)
            .FirstAsync(cancellationToken);

        await _auditLog.LogAsync(
            tableName: "maintenance_records",
            recordId: record.MaintenanceRecordId,
            action: "INSERT",
            oldData: null,
            newData: new
            {
                record.MaintenanceRecordId,
                record.AssetId,
                record.MaintenanceTypeId,
                record.Status,
                record.ScheduledAt,
                record.PerformedBy,
                record.Description,
                record.Version
            },
            cancellationToken);

        return new ScheduleMaintenanceResponse(
            record.MaintenanceRecordId,
            record.Status,
            record.AssetId,
            assetStatusCode,
            record.ScheduledAt!.Value,
            record.Version);
    }
}
