using Microsoft.EntityFrameworkCore;
using TecnmInventario.Api.Common.Audit;
using TecnmInventario.Api.Common.Domain;
using TecnmInventario.Api.Common.Errors;
using TecnmInventario.Api.Common.Persistence;
using TecnmInventario.Api.Common.Security;
using TecnmInventario.Api.Core.Entities;
using TecnmInventario.Api.Data;
using TecnmInventario.Api.Features.Assets.Shared;

namespace TecnmInventario.Api.Features.Maintenance.Shared;

/// <param name="MaintenanceRecordId">null = inicio directo (el registro nace EN_CURSO).</param>
/// <param name="ExpectedVersion">Solo con <paramref name="MaintenanceRecordId"/>.</param>
public sealed record MaintenanceStartRequest(
    long AssetId,
    long? MaintenanceRecordId,
    int ExpectedVersion,
    long? MaintenanceTypeId,
    long? PerformedBy,
    string? Description,
    long? IncidentId);

public sealed record MaintenanceStartResult(
    long MaintenanceRecordId, long AssetId, string AssetStatusCode, DateTime StartedAt, int Version);

/// <summary>
/// Servicio interno que comparten los dos caminos de "iniciar mantenimiento"
/// (desde un PROGRAMADO y directo). Gestiona SU PROPIA transacción (regla 19: el
/// activo se bloquea primero, se recarga y solo entonces se decide y se muta).
///
/// ORDEN DE LOCKS: 1) activo (AssetRowLock)  2) registro (al ACTUALIZARLO)
/// 3) lo demás. Nunca el registro antes que el activo.
/// </summary>
internal static class MaintenanceStartFlow
{
    public static async Task<MaintenanceStartResult> ExecuteAsync(
        TecnmInventarioDbContext context,
        ICurrentUserService currentUser,
        IAuditLogService auditLog,
        IAssetStatusChanger assetStatusChanger,
        MaintenanceStartRequest request,
        CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId ?? throw new UnauthorizedAccessException();
        var isDirect = request.MaintenanceRecordId is null;

        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);

        // 1) El activo es SIEMPRE el primer lock.
        var lockedAsset = await AssetRowLock.LockAsync(context, request.AssetId, cancellationToken);
        var asset = await MaintenanceGuards.LoadAssetAsync(context, request.AssetId, cancellationToken);

        MaintenanceGuards.EnsureAssetNotDeleted(asset, "iniciar");
        if (lockedAsset.AssetStatusCode == AssetStatusCodes.DadoDeBaja)
        {
            throw new BusinessRuleViolationException(
                "No se puede iniciar un mantenimiento sobre un activo dado de baja.");
        }

        // 2) Con registro: versión fresca (409) y transición válida del registro.
        MaintenanceRecord? record = null;
        if (!isDirect)
        {
            record = await MaintenanceGuards.LoadRecordAsync(context, request.MaintenanceRecordId!.Value, cancellationToken);
            MaintenanceGuards.EnsureVersion(record, request.ExpectedVersion);

            if (!MaintenanceStatusTransitions.IsAllowed(record.Status, MaintenanceStatusCodes.EnCurso))
            {
                throw new BusinessRuleViolationException(
                    $"Solo se puede iniciar un mantenimiento programado (estado actual: {record.Status}).");
            }
        }

        // 3) Un solo mantenimiento EN_CURSO por activo. El índice único
        //    ux_maintenance_one_in_progress es el respaldo (409 vía middleware).
        var otherInProgress = await context.MaintenanceRecords.AnyAsync(
            m => m.AssetId == request.AssetId
                 && m.Status == MaintenanceStatusCodes.EnCurso
                 && !m.IsDeleted
                 && m.MaintenanceRecordId != (request.MaintenanceRecordId ?? 0),
            cancellationToken);
        if (otherInProgress)
        {
            throw new BusinessRuleViolationException("El activo ya tiene un mantenimiento en curso.");
        }

        // 4) Incidente opcional: del mismo activo y abierto.
        if (request.IncidentId is { } incidentId)
        {
            var incident = await context.Incidents
                .Where(i => i.IncidentId == incidentId)
                .Select(i => new { i.AssetId, StatusCode = i.IncidentStatus.Code })
                .FirstOrDefaultAsync(cancellationToken)
                ?? throw new BusinessRuleViolationException("El incidente especificado no existe.");

            if (incident.AssetId != request.AssetId)
            {
                throw new BusinessRuleViolationException("El incidente indicado no pertenece a este activo.");
            }

            if (!IncidentStatusCodes.Open.Contains(incident.StatusCode))
            {
                throw new BusinessRuleViolationException("El incidente indicado no está abierto.");
            }
        }

        var now = DateTime.UtcNow;
        if (isDirect)
        {
            record = new MaintenanceRecord
            {
                AssetId = request.AssetId,
                MaintenanceTypeId = request.MaintenanceTypeId!.Value,
                Status = MaintenanceStatusCodes.EnCurso,
                StartedAt = now,
                StatusBeforeId = lockedAsset.AssetStatusId,
                PerformedBy = request.PerformedBy ?? userId,
                Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
                IncidentId = request.IncidentId,
                CreatedBy = userId
            };
            context.MaintenanceRecords.Add(record);
        }
        else
        {
            record!.Status = MaintenanceStatusCodes.EnCurso;
            record.StartedAt = now;
            record.StatusBeforeId = lockedAsset.AssetStatusId;
            record.PerformedBy ??= userId;
            if (request.IncidentId is not null)
            {
                record.IncidentId = request.IncidentId;
            }

            record.UpdatedBy = userId;
        }

        await context.SaveChangesAsync(cancellationToken);
        await context.Entry(record).ReloadAsync(cancellationToken);

        // Solo un activo que NO está ya EN_MANTENIMIENTO cambia de estado (desde
        // DISPONIBLE, ASIGNADO o CON_FALLA). El estado se evalúa con la fila bloqueada.
        var finalAssetStatus = lockedAsset.AssetStatusCode;
        if (lockedAsset.AssetStatusCode != AssetStatusCodes.EnMantenimiento)
        {
            var change = await assetStatusChanger.ChangeAsync(
                asset,
                AssetStatusCodes.EnMantenimiento,
                $"Mantenimiento #{record.MaintenanceRecordId} iniciado",
                cancellationToken);
            finalAssetStatus = change.NewStatusCode;
        }

        if (isDirect)
        {
            await auditLog.LogAsync(
                tableName: "maintenance_records",
                recordId: record.MaintenanceRecordId,
                action: "INSERT",
                oldData: null,
                newData: new
                {
                    record.MaintenanceRecordId,
                    record.AssetId,
                    record.MaintenanceTypeId,
                    Status = MaintenanceStatusCodes.EnCurso,
                    record.StartedAt,
                    record.PerformedBy,
                    record.IncidentId,
                    StatusBefore = lockedAsset.AssetStatusCode,
                    AssetStatusAfter = finalAssetStatus
                },
                cancellationToken);
        }
        else
        {
            await auditLog.LogAsync(
                tableName: "maintenance_records",
                recordId: record.MaintenanceRecordId,
                action: "UPDATE",
                oldData: new { Status = MaintenanceStatusCodes.Programado },
                newData: new
                {
                    Status = MaintenanceStatusCodes.EnCurso,
                    record.StartedAt,
                    record.PerformedBy,
                    record.IncidentId,
                    StatusBefore = lockedAsset.AssetStatusCode,
                    AssetStatusAfter = finalAssetStatus
                },
                cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);

        return new MaintenanceStartResult(
            record.MaintenanceRecordId, request.AssetId, finalAssetStatus, record.StartedAt!.Value, record.Version);
    }
}
