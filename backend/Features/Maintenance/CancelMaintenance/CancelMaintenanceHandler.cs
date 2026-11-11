using Mediator;
using TecnmInventario.Api.Common.Audit;
using TecnmInventario.Api.Common.Errors;
using TecnmInventario.Api.Common.Persistence;
using TecnmInventario.Api.Common.Security;
using TecnmInventario.Api.Data;
using TecnmInventario.Api.Features.Assets.Shared;
using TecnmInventario.Api.Features.Maintenance.Shared;

namespace TecnmInventario.Api.Features.Maintenance.CancelMaintenance;

public sealed class CancelMaintenanceHandler
    : ICommandHandler<CancelMaintenanceCommand, CancelMaintenanceResponse>
{
    private readonly TecnmInventarioDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IAuditLogService _auditLog;
    private readonly IAssetStatusChanger _assetStatusChanger;

    public CancelMaintenanceHandler(
        TecnmInventarioDbContext context,
        ICurrentUserService currentUser,
        IAuditLogService auditLog,
        IAssetStatusChanger assetStatusChanger)
    {
        _context = context;
        _currentUser = currentUser;
        _auditLog = auditLog;
        _assetStatusChanger = assetStatusChanger;
    }

    public async ValueTask<CancelMaintenanceResponse> Handle(
        CancelMaintenanceCommand command, CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId ?? throw new UnauthorizedAccessException();

        var assetId = await MaintenanceGuards.ReadAssetIdAsync(
            _context, command.MaintenanceRecordId, cancellationToken);

        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

        // ORDEN DE LOCKS: 1) el activo, SIEMPRE primero (también al cancelar un
        // PROGRAMADO: serializa contra un Start concurrente)  2) el registro.
        var lockedAsset = await AssetRowLock.LockAsync(_context, assetId, cancellationToken);
        var asset = await MaintenanceGuards.LoadAssetAsync(_context, assetId, cancellationToken);
        var record = await MaintenanceGuards.LoadRecordAsync(_context, command.MaintenanceRecordId, cancellationToken);

        MaintenanceGuards.EnsureAssetNotDeleted(asset, "cancelar");
        MaintenanceGuards.EnsureVersion(record, command.ExpectedVersion);

        if (!MaintenanceStatusTransitions.IsAllowed(record.Status, MaintenanceStatusCodes.Cancelado))
        {
            throw new BusinessRuleViolationException(
                $"Solo se puede cancelar un mantenimiento programado o en curso (estado actual: {record.Status}).");
        }

        var previousStatus = record.Status;
        record.Status = MaintenanceStatusCodes.Cancelado;
        record.CancelledAt = DateTime.UtcNow;
        record.CancellationReason = command.Reason.Trim();
        record.UpdatedBy = userId;

        MaintenanceReleaseResult release;
        if (previousStatus == MaintenanceStatusCodes.EnCurso)
        {
            // Cancelar uno EN_CURSO libera el activo igual que cerrarlo.
            release = await MaintenanceAssetRelease.ReleaseAsync(
                _context, _assetStatusChanger, asset, lockedAsset.AssetStatusCode, record,
                $"Mantenimiento #{record.MaintenanceRecordId} cancelado", cancellationToken);
        }
        else
        {
            // PROGRAMADO: no se toca el activo (aún no se había iniciado).
            await _context.SaveChangesAsync(cancellationToken);
            await _context.Entry(record).ReloadAsync(cancellationToken);
            release = new MaintenanceReleaseResult(false, lockedAsset.AssetStatusCode, 0);
        }

        await _auditLog.LogAsync(
            tableName: "maintenance_records",
            recordId: record.MaintenanceRecordId,
            action: "UPDATE",
            oldData: new { Status = previousStatus },
            newData: new
            {
                Status = MaintenanceStatusCodes.Cancelado,
                record.CancelledAt,
                record.CancellationReason,
                AssetStatusAfter = release.AssetStatusCode,
                release.Released,
                release.OpenIncidents
            },
            cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        return new CancelMaintenanceResponse(
            record.MaintenanceRecordId,
            MaintenanceStatusCodes.Cancelado,
            asset.AssetId,
            release.AssetStatusCode,
            release.Released,
            release.OpenIncidents,
            record.CancelledAt!.Value,
            record.Version);
    }
}
