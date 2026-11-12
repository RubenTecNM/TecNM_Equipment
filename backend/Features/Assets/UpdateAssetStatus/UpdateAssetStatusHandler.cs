using Mediator;
using Microsoft.EntityFrameworkCore;
using TecnmInventario.Api.Common.Audit;
using TecnmInventario.Api.Common.Domain;
using TecnmInventario.Api.Common.Errors;
using TecnmInventario.Api.Common.Persistence;
using TecnmInventario.Api.Common.Security;
using TecnmInventario.Api.Data;
using TecnmInventario.Api.Features.Maintenance.Shared;

namespace TecnmInventario.Api.Features.Assets.UpdateAssetStatus;

public sealed class UpdateAssetStatusHandler : ICommandHandler<UpdateAssetStatusCommand, UpdateAssetStatusResponse>
{
    private readonly TecnmInventarioDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IAuditLogService _auditLog;

    public UpdateAssetStatusHandler(
        TecnmInventarioDbContext context, ICurrentUserService currentUser, IAuditLogService auditLog)
    {
        _context = context;
        _currentUser = currentUser;
        _auditLog = auditLog;
    }

    public async ValueTask<UpdateAssetStatusResponse> Handle(
        UpdateAssetStatusCommand command, CancellationToken cancellationToken)
    {
        var asset = await _context.Assets
            .Include(a => a.AssetStatus)
            .FirstOrDefaultAsync(a => a.AssetId == command.AssetId && !a.IsDeleted, cancellationToken);

        if (asset is null)
        {
            throw new KeyNotFoundException("El activo solicitado no existe.");
        }

        var newStatus = await _context.AssetStatuses
            .FirstAsync(s => s.Code == command.NewStatusCode && !s.IsDeleted, cancellationToken);

        // Transacción explícita: el cambio de estado y su registro en
        // audit_log deben ser atómicos (todo o nada). Sin esto, si el
        // INSERT en audit_log falla por cualquier razón, el UPDATE del
        // activo ya quedaría confirmado en la BD sin dejar rastro de
        // auditoría -- justo lo que esto existe para evitar.
        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

        // El activo es SIEMPRE el primer lock. La comprobación de incidentes
        // abiertos se hace DESPUÉS del lock: antes se hacía fuera de la
        // transacción y una alta concurrente de incidente podía colarse.
        var lockedAsset = await AssetRowLock.LockAsync(_context, asset.AssetId, cancellationToken);
        await _context.Entry(asset).ReloadAsync(cancellationToken);

        // Concurrencia optimista contra la versión fresca, tras el lock.
        if (asset.Version != command.ExpectedVersion)
        {
            throw new DbUpdateConcurrencyException(
                "El activo fue modificado por otro usuario mientras tanto.");
        }

        // M.3 (I3): con un mantenimiento EN_CURSO el activo no puede salir de
        // EN_MANTENIMIENTO hacia DISPONIBLE/ASIGNADO por aquí; hay que cerrar o
        // cancelar el mantenimiento. Va DESPUÉS del lock (regla 19). Sin registro
        // EN_CURSO (EN_MANTENIMIENTO por un incidente o a mano) sigue permitido.
        var maintenanceInProgress = await _context.MaintenanceRecords.AnyAsync(
            m => m.AssetId == asset.AssetId && m.Status == MaintenanceStatusCodes.EnCurso && !m.IsDeleted,
            cancellationToken);

        if (maintenanceInProgress
            && lockedAsset.AssetStatusCode == AssetStatusCodes.EnMantenimiento
            && command.NewStatusCode is AssetStatusCodes.Disponible or AssetStatusCodes.Asignado)
        {
            throw new BusinessRuleViolationException(
                "El activo tiene un mantenimiento en curso. Ciérralo o cancélalo primero.");
        }

        // M.3 (I4): la baja exige que no haya mantenimiento EN_CURSO; los
        // PROGRAMADOS se cancelan en esta misma transacción, antes de la baja.
        if (command.NewStatusCode == AssetStatusCodes.DadoDeBaja)
        {
            if (maintenanceInProgress)
            {
                throw new BusinessRuleViolationException(
                    "El activo tiene un mantenimiento en curso. Ciérralo o cancélalo antes de darlo de baja.");
            }

            await CancelScheduledMaintenanceAsync(asset.AssetId, cancellationToken);
        }

        // Un activo con incidentes abiertos no puede volver a circular. Sin esta
        // regla se podría pasar a DISPONIBLE/ASIGNADO por este endpoint (via
        // EN_MANTENIMIENTO) con una falla todavía sin resolver. DADO_DE_BAJA queda
        // exento: un equipo robado o irreparable debe poder darse de baja igual.
        if (command.NewStatusCode is AssetStatusCodes.Disponible or AssetStatusCodes.Asignado)
        {
            var openCodes = IncidentStatusCodes.Open.ToArray();
            var hasOpenIncidents = await _context.Incidents.AnyAsync(
                i => i.AssetId == asset.AssetId && openCodes.Contains(i.IncidentStatus.Code),
                cancellationToken);

            if (hasOpenIncidents)
            {
                throw new BusinessRuleViolationException(
                    "No se puede liberar el activo: tiene incidentes abiertos. Resuélvalos primero.");
            }
        }

        asset.AssetStatusId = newStatus.AssetStatusId;
        asset.UpdatedBy = _currentUser.UserId;

        await _context.SaveChangesAsync(cancellationToken);
        await _context.Entry(asset).ReloadAsync(cancellationToken);

        // action debe ser uno de los valores que acepta el CHECK constraint
        // de audit_log (INSERT/UPDATE/DELETE); el detalle real del cambio
        // (a qué estado, por qué) va en new_data, no en el nombre de la acción.
        await _auditLog.LogAsync(
            tableName: "assets",
            recordId: asset.AssetId,
            action: "UPDATE",
            oldData: new { Status = lockedAsset.AssetStatusCode },
            newData: new { Status = command.NewStatusCode, command.Reason },
            cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        return new UpdateAssetStatusResponse(asset.AssetId, command.NewStatusCode, asset.Version);
    }

    /// <summary>
    /// Cancela todos los mantenimientos PROGRAMADOS del activo (con el activo ya
    /// bloqueado: el activo es siempre el primer lock) y audita cada uno.
    /// </summary>
    private async Task CancelScheduledMaintenanceAsync(long assetId, CancellationToken cancellationToken)
    {
        var scheduled = await _context.MaintenanceRecords
            .Where(m => m.AssetId == assetId && m.Status == MaintenanceStatusCodes.Programado && !m.IsDeleted)
            .OrderBy(m => m.MaintenanceRecordId)
            .ToListAsync(cancellationToken);

        foreach (var record in scheduled)
        {
            record.Status = MaintenanceStatusCodes.Cancelado;
            record.CancelledAt = DateTime.UtcNow;
            record.CancellationReason = "Cancelado por baja del activo.";
            record.UpdatedBy = _currentUser.UserId;
        }

        if (scheduled.Count == 0)
        {
            return;
        }

        await _context.SaveChangesAsync(cancellationToken);

        foreach (var record in scheduled)
        {
            await _auditLog.LogAsync(
                tableName: "maintenance_records",
                recordId: record.MaintenanceRecordId,
                action: "UPDATE",
                oldData: new { Status = MaintenanceStatusCodes.Programado },
                newData: new
                {
                    Status = MaintenanceStatusCodes.Cancelado,
                    record.CancelledAt,
                    record.CancellationReason
                },
                cancellationToken);
        }
    }
}
