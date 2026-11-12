using Mediator;
using TecnmInventario.Api.Common.Audit;
using TecnmInventario.Api.Common.Errors;
using TecnmInventario.Api.Common.Persistence;
using TecnmInventario.Api.Common.Security;
using TecnmInventario.Api.Data;
using TecnmInventario.Api.Features.Assets.Shared;
using TecnmInventario.Api.Features.Maintenance.Shared;

namespace TecnmInventario.Api.Features.Maintenance.CompleteMaintenance;

public sealed class CompleteMaintenanceHandler
    : ICommandHandler<CompleteMaintenanceCommand, CompleteMaintenanceResponse>
{
    private readonly TecnmInventarioDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IAuditLogService _auditLog;
    private readonly IAssetStatusChanger _assetStatusChanger;

    public CompleteMaintenanceHandler(
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

    public async ValueTask<CompleteMaintenanceResponse> Handle(
        CompleteMaintenanceCommand command, CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId ?? throw new UnauthorizedAccessException();

        // Solo se lee el id del activo, SIN lock (AsNoTracking).
        var assetId = await MaintenanceGuards.ReadAssetIdAsync(
            _context, command.MaintenanceRecordId, cancellationToken);

        // Transacción explícita: cierre + liberación del activo + auditoría, atómicos.
        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

        // ORDEN DE LOCKS: 1) el activo, SIEMPRE primero  2) el registro (al actualizarlo).
        var lockedAsset = await AssetRowLock.LockAsync(_context, assetId, cancellationToken);
        var asset = await MaintenanceGuards.LoadAssetAsync(_context, assetId, cancellationToken);
        var record = await MaintenanceGuards.LoadRecordAsync(_context, command.MaintenanceRecordId, cancellationToken);

        MaintenanceGuards.EnsureAssetNotDeleted(asset, "completar");
        MaintenanceGuards.EnsureVersion(record, command.ExpectedVersion);

        if (!MaintenanceStatusTransitions.IsAllowed(record.Status, MaintenanceStatusCodes.Completado))
        {
            throw new BusinessRuleViolationException(
                $"Solo se puede completar un mantenimiento en curso (estado actual: {record.Status}).");
        }

        var previousStatus = record.Status;
        record.Status = MaintenanceStatusCodes.Completado;
        record.PerformedAt = DateTime.UtcNow;
        if (!string.IsNullOrWhiteSpace(command.Description))
        {
            record.Description = command.Description.Trim();
        }

        if (!string.IsNullOrWhiteSpace(command.PartsUsed))
        {
            record.PartsUsed = command.PartsUsed.Trim();
        }

        if (command.Cost is { } cost)
        {
            record.Cost = cost;
        }

        if (command.NextMaintenanceAt is { } next)
        {
            record.NextMaintenanceAt = MaintenanceDates.ToUtc(next);
        }

        record.UpdatedBy = userId;

        var release = await MaintenanceAssetRelease.ReleaseAsync(
            _context, _assetStatusChanger, asset, lockedAsset.AssetStatusCode, record,
            $"Mantenimiento #{record.MaintenanceRecordId} completado", cancellationToken);

        await _auditLog.LogAsync(
            tableName: "maintenance_records",
            recordId: record.MaintenanceRecordId,
            action: "UPDATE",
            oldData: new { Status = previousStatus },
            newData: new
            {
                Status = MaintenanceStatusCodes.Completado,
                record.PerformedAt,
                record.Cost,
                record.NextMaintenanceAt,
                record.PartsUsed,
                record.Description,
                AssetStatusAfter = release.AssetStatusCode,
                release.Released,
                release.OpenIncidents
            },
            cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        return new CompleteMaintenanceResponse(
            record.MaintenanceRecordId,
            MaintenanceStatusCodes.Completado,
            asset.AssetId,
            release.AssetStatusCode,
            release.Released,
            release.OpenIncidents,
            record.PerformedAt!.Value,
            record.Version);
    }
}
