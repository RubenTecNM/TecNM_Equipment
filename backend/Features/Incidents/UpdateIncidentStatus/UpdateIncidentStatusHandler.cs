using Mediator;
using Microsoft.EntityFrameworkCore;
using TecnmInventario.Api.Common.Audit;
using TecnmInventario.Api.Common.Domain;
using TecnmInventario.Api.Common.Errors;
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

        var incident = await _context.Incidents
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

        var assetStatusCode = incident.Asset.AssetStatus.Code;
        var startsRepair = command.NewStatusCode == IncidentStatusCodes.EnReparacion;

        if (startsRepair && assetStatusCode == AssetStatusCodes.DadoDeBaja)
        {
            throw new BusinessRuleViolationException(
                "No se puede iniciar la reparación de un activo dado de baja.");
        }

        var newStatusId = await _context.IncidentStatuses
            .Where(s => s.Code == command.NewStatusCode && !s.IsDeleted)
            .Select(s => s.IncidentStatusId)
            .FirstAsync(cancellationToken);

        _context.Entry(incident).Property(i => i.Version).OriginalValue = command.ExpectedVersion;
        incident.IncidentStatusId = newStatusId;
        incident.UpdatedBy = userId;

        // Transacción explícita: incidente + (opcional) cambio de estado del activo
        // + filas de audit_log se confirman juntos o ninguno.
        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

        await _context.SaveChangesAsync(cancellationToken);
        await _context.Entry(incident).ReloadAsync(cancellationToken);

        // Iniciar la reparación saca al activo de circulación (EN_MANTENIMIENTO);
        // si ya está ahí no se toca. Las demás transiciones no afectan al activo.
        var finalAssetStatus = assetStatusCode;
        if (startsRepair && assetStatusCode != AssetStatusCodes.EnMantenimiento)
        {
            var change = await _assetStatusChanger.ChangeAsync(
                incident.Asset,
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
