using Mediator;
using Microsoft.EntityFrameworkCore;
using TecnmInventario.Api.Common.Audit;
using TecnmInventario.Api.Common.Domain;
using TecnmInventario.Api.Common.Errors;
using TecnmInventario.Api.Common.Persistence;
using TecnmInventario.Api.Common.Security;
using TecnmInventario.Api.Features.Assets.Shared;
using TecnmInventario.Api.Features.Incidents.Shared;
using TecnmInventario.Api.Data;

namespace TecnmInventario.Api.Features.Incidents.UpdateIncidentStatus;

public sealed class UpdateIncidentStatusHandler
    : ICommandHandler<UpdateIncidentStatusCommand, UpdateIncidentStatusResponse>
{
    private readonly TecnmInventarioDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IAuditLogService _auditLog;
    private readonly IAssetStatusChanger _assetStatusChanger;

    public UpdateIncidentStatusHandler(
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

    public async ValueTask<UpdateIncidentStatusResponse> Handle(
        UpdateIncidentStatusCommand command, CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId ?? throw new UnauthorizedAccessException();

        // D4/D3: Incident.Asset es una navegación requerida; sin ignorar el
        // filtro "SoftDelete" de Asset, el incidente entero desaparecería de
        // esta consulta si su activo está borrado lógicamente. El Where
        // explícito sobre la raíz compensa que IgnoreQueryFilters también
        // quita el filtro propio de Incident (D2).
        var incident = await _context.Incidents
            .IgnoreQueryFilters(["SoftDelete"])
            .Where(i => !i.IsDeleted)
            .Include(i => i.IncidentStatus)
            .Include(i => i.Asset).ThenInclude(a => a.AssetStatus)
            .FirstOrDefaultAsync(i => i.IncidentId == command.IncidentId, cancellationToken)
            ?? throw new KeyNotFoundException("El incidente solicitado no existe.");

        var currentCode = incident.IncidentStatus.Code;

        if (!IncidentStatusTransitions.IsAllowed(currentCode, command.NewStatusCode))
        {
            throw new BusinessRuleViolationException(
                $"Transición de estado de incidente no permitida: {currentCode} -> {command.NewStatusCode}.");
        }

        var startsRepair = command.NewStatusCode == IncidentStatusCodes.EnReparacion;

        var newStatusId = await _context.IncidentStatuses
            .Where(s => s.Code == command.NewStatusCode && !s.IsDeleted)
            .Select(s => s.IncidentStatusId)
            .FirstAsync(cancellationToken);

        // Transacción explícita: incidente + (opcional) cambio de estado del activo
        // + filas de audit_log se confirman juntos o ninguno.
        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

        // El activo es SIEMPRE el primer lock, antes de tocar el incidente.
        var lockedAsset = await AssetRowLock.LockAsync(_context, incident.AssetId, cancellationToken);
        var asset = incident.Asset;
        await _context.Entry(asset).ReloadAsync(cancellationToken);
        await _context.Entry(incident).ReloadAsync(cancellationToken);

        // D4: el guard va DESPUÉS del lock y del reload, nunca antes, para
        // no crear una carrera entre la comprobación y el bloqueo de la fila.
        if (asset.IsDeleted)
        {
            throw new BusinessRuleViolationException(
                $"No se puede cambiar el estado del incidente #{incident.IncidentId}: " +
                $"el activo #{incident.AssetId} está eliminado lógicamente.");
        }

        // Concurrencia optimista contra la versión fresca, tras el lock.
        if (incident.Version != command.ExpectedVersion)
        {
            throw new DbUpdateConcurrencyException(
                "El incidente fue modificado por otro usuario mientras tanto.");
        }

        if (startsRepair && lockedAsset.AssetStatusCode == AssetStatusCodes.DadoDeBaja)
        {
            throw new BusinessRuleViolationException(
                "No se puede iniciar la reparación de un activo dado de baja.");
        }

        incident.IncidentStatusId = newStatusId;
        incident.UpdatedBy = userId;

        await _context.SaveChangesAsync(cancellationToken);
        await _context.Entry(incident).ReloadAsync(cancellationToken);

        // Iniciar la reparación saca al activo de circulación (EN_MANTENIMIENTO);
        // si ya está ahí no se toca. Las demás transiciones no afectan al activo.
        // El estado se evalúa con la fila ya bloqueada, no con el cargado antes.
        var finalAssetStatus = lockedAsset.AssetStatusCode;
        if (startsRepair && lockedAsset.AssetStatusCode != AssetStatusCodes.EnMantenimiento)
        {
            var change = await _assetStatusChanger.ChangeAsync(
                asset,
                AssetStatusCodes.EnMantenimiento,
                $"Incidente #{incident.IncidentId} en reparación",
                cancellationToken);
            finalAssetStatus = change.NewStatusCode;
        }

        await _auditLog.LogAsync(
            tableName: "incidents",
            recordId: incident.IncidentId,
            action: "UPDATE",
            oldData: new { Status = currentCode },
            newData: new { Status = command.NewStatusCode, command.Reason, AssetStatusAfter = finalAssetStatus },
            cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        return new UpdateIncidentStatusResponse(
            incident.IncidentId, command.NewStatusCode, finalAssetStatus, incident.Version);
    }
}
