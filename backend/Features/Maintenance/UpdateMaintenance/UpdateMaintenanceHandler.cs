using Mediator;
using Microsoft.EntityFrameworkCore;
using TecnmInventario.Api.Common.Audit;
using TecnmInventario.Api.Common.Authorization;
using TecnmInventario.Api.Common.Errors;
using TecnmInventario.Api.Common.Security;
using TecnmInventario.Api.Data;
using TecnmInventario.Api.Features.Maintenance.Shared;

namespace TecnmInventario.Api.Features.Maintenance.UpdateMaintenance;

/// <summary>
/// Edición simple (regla 13): ITransactionalCommand + auditoría UPDATE.
///
/// Por qué NO toma AssetRowLock y no viola la regla 19: esa regla exige el lock del
/// activo a los handlers que cambian el ESTADO del activo en función de otras filas
/// (incidentes, asignaciones, otros mantenimientos). Este handler no cambia el activo
/// ni decide nada a partir de otras filas: solo modifica campos descriptivos del
/// propio registro. La concurrencia con Start/Complete/Cancel la cubre el token de
/// versión (Version es IsConcurrencyToken): cualquier cambio del registro sube la
/// versión por trigger, así que el UPDATE de quien llega segundo afecta 0 filas ->
/// DbUpdateConcurrencyException -> 409. Además no hay riesgo de interbloqueo: este
/// handler bloquea una sola fila (el registro) y nunca espera por el activo, mientras
/// Start/Complete/Cancel bloquean activo -> registro; no puede formarse un ciclo.
/// </summary>
public sealed class UpdateMaintenanceHandler : ICommandHandler<UpdateMaintenanceCommand, UpdateMaintenanceResponse>
{
    private readonly TecnmInventarioDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IAuditLogService _auditLog;
    private readonly IRolePermissionService _rolePermissions;

    public UpdateMaintenanceHandler(
        TecnmInventarioDbContext context,
        ICurrentUserService currentUser,
        IAuditLogService auditLog,
        IRolePermissionService rolePermissions)
    {
        _context = context;
        _currentUser = currentUser;
        _auditLog = auditLog;
        _rolePermissions = rolePermissions;
    }

    public async ValueTask<UpdateMaintenanceResponse> Handle(
        UpdateMaintenanceCommand command, CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId ?? throw new UnauthorizedAccessException();

        var record = await MaintenanceGuards.LoadRecordAsync(_context, command.MaintenanceRecordId, cancellationToken);

        // Concurrencia optimista: la versión que vio el cliente debe coincidir con la
        // actual aunque los datos enviados sean idénticos (regla 15).
        MaintenanceGuards.EnsureVersion(record, command.ExpectedVersion);

        // Regla 20: operar sobre algo cuyo activo está borrado es 400, nunca 404/500.
        var asset = await MaintenanceGuards.LoadAssetAsync(_context, record.AssetId, cancellationToken);
        MaintenanceGuards.EnsureAssetNotDeleted(asset, "editar");

        if (record.Status is not (MaintenanceStatusCodes.Programado or MaintenanceStatusCodes.EnCurso))
        {
            throw new BusinessRuleViolationException(
                "Un mantenimiento completado o cancelado no se puede editar.");
        }

        var isScheduled = record.Status == MaintenanceStatusCodes.Programado;

        // 1) Campos solo editables en PROGRAMADO: un valor DISTINTO en EN_CURSO es 400.
        DateTime? newScheduledAt = null;
        if (command.ScheduledAt is { } requestedSchedule)
        {
            var utc = MaintenanceDates.ToUtc(requestedSchedule);
            if (record.ScheduledAt != utc)
            {
                RejectIfNotScheduled(isScheduled, "scheduledAt");
                newScheduledAt = utc;
            }
        }

        long? newTypeId = null;
        if (command.MaintenanceTypeId is { } requestedType && requestedType != record.MaintenanceTypeId)
        {
            RejectIfNotScheduled(isScheduled, "maintenanceTypeId");

            // MaintenanceType es un catálogo exento del filtro: !IsDeleted explícito.
            var typeExists = await _context.MaintenanceTypes.AnyAsync(
                t => t.MaintenanceTypeId == requestedType && !t.IsDeleted, cancellationToken);
            if (!typeExists)
            {
                throw new BusinessRuleViolationException("El tipo de mantenimiento especificado no existe.");
            }

            newTypeId = requestedType;
        }

        // 2) Campos editables en ambos estados. Mismas reglas que Schedule, solo si cambian.
        var performedByChanged = command.PerformedBy != record.PerformedBy;
        if (performedByChanged
            && command.PerformedBy is { } technicianId
            && !await MaintenanceTechnicianRule.IsValidAsync(_context, _rolePermissions, technicianId, cancellationToken))
        {
            throw new BusinessRuleViolationException(MaintenanceTechnicianRule.InvalidMessage);
        }

        var description = string.IsNullOrWhiteSpace(command.Description) ? null : command.Description.Trim();
        var descriptionChanged = description != record.Description;

        // Regla 15: sin cambios reales no hay UPDATE, ni versión nueva, ni auditoría.
        if (newScheduledAt is null && newTypeId is null && !performedByChanged && !descriptionChanged)
        {
            return new UpdateMaintenanceResponse(record.MaintenanceRecordId, record.Status, record.Version);
        }

        // Snapshots explícitos (sin secretos), "antes" ANTES de mutar.
        var before = new
        {
            record.ScheduledAt,
            record.MaintenanceTypeId,
            record.PerformedBy,
            record.Description,
            record.Version
        };

        if (newScheduledAt is not null)
        {
            record.ScheduledAt = newScheduledAt;
        }

        if (newTypeId is not null)
        {
            record.MaintenanceTypeId = newTypeId.Value;
        }

        record.PerformedBy = command.PerformedBy;
        record.Description = description;
        record.UpdatedBy = userId;

        await _context.SaveChangesAsync(cancellationToken);
        await _context.Entry(record).ReloadAsync(cancellationToken);

        await _auditLog.LogAsync(
            tableName: "maintenance_records",
            recordId: record.MaintenanceRecordId,
            action: "UPDATE",
            oldData: before,
            newData: new
            {
                record.ScheduledAt,
                record.MaintenanceTypeId,
                record.PerformedBy,
                record.Description,
                record.Version
            },
            cancellationToken);

        return new UpdateMaintenanceResponse(record.MaintenanceRecordId, record.Status, record.Version);
    }

    private static void RejectIfNotScheduled(bool isScheduled, string field)
    {
        if (!isScheduled)
        {
            throw new BusinessRuleViolationException(
                $"El campo {field} no se puede modificar en un mantenimiento en curso.");
        }
    }
}
