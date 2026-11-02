using Mediator;
using Microsoft.EntityFrameworkCore;
using TecnmInventario.Api.Common.Audit;
using TecnmInventario.Api.Common.Domain;
using TecnmInventario.Api.Common.Errors;
using TecnmInventario.Api.Common.Persistence;
using TecnmInventario.Api.Common.Security;
using TecnmInventario.Api.Features.Assets.Shared;
using TecnmInventario.Api.Data;
using TecnmInventario.Api.Core.Entities;

namespace TecnmInventario.Api.Features.Incidents.CreateIncident;

public sealed class CreateIncidentHandler : ICommandHandler<CreateIncidentCommand, CreateIncidentResponse>
{
    private readonly TecnmInventarioDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IAuditLogService _auditLog;
    private readonly IAssetStatusChanger _assetStatusChanger;

    public CreateIncidentHandler(
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

    public async ValueTask<CreateIncidentResponse> Handle(
        CreateIncidentCommand command, CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId ?? throw new UnauthorizedAccessException();

        var asset = await _context.Assets
            .Include(a => a.AssetStatus)
            .FirstOrDefaultAsync(a => a.AssetId == command.AssetId && !a.IsDeleted, cancellationToken)
            ?? throw new KeyNotFoundException("El activo solicitado no existe.");

        var openStatusId = await _context.IncidentStatuses
            .Where(s => s.Code == IncidentStatusCodes.Abierto && !s.IsDeleted)
            .Select(s => s.IncidentStatusId)
            .FirstAsync(cancellationToken);

        // Transacción explícita: incidente + cambio de estado del activo +
        // filas de audit_log deben confirmarse juntos o ninguno.
        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

        // El activo es SIEMPRE el primer lock. Se bloquea antes de insertar el
        // incidente para que una resolución concurrente no cuente incidentes
        // abiertos sin ver este, ni este handler decida con un estado viejo.
        var lockedAsset = await AssetRowLock.LockAsync(_context, asset.AssetId, cancellationToken);
        await _context.Entry(asset).ReloadAsync(cancellationToken);

        if (lockedAsset.AssetStatusCode == AssetStatusCodes.DadoDeBaja)
        {
            throw new BusinessRuleViolationException(
                "No se puede reportar un incidente sobre un activo dado de baja.");
        }

        var incident = new Incident
        {
            AssetId = asset.AssetId,
            IncidentTypeId = command.IncidentTypeId,
            IncidentPriorityId = command.IncidentPriorityId,
            IncidentStatusId = openStatusId,
            ReportedBy = userId,
            Title = command.Title.Trim(),
            Description = command.Description?.Trim(),
            CreatedBy = userId
        };

        _context.Incidents.Add(incident);
        await _context.SaveChangesAsync(cancellationToken);

        // Solo un activo operativo pasa a CON_FALLA. Si ya estaba CON_FALLA o
        // EN_MANTENIMIENTO (ya fuera de servicio) no se toca su estado. El
        // estado se evalúa con la fila ya bloqueada, no con el cargado antes.
        var finalAssetStatus = lockedAsset.AssetStatusCode;
        if (finalAssetStatus is AssetStatusCodes.Disponible or AssetStatusCodes.Asignado)
        {
            var change = await _assetStatusChanger.ChangeAsync(
                asset,
                AssetStatusCodes.ConFalla,
                $"Incidente #{incident.IncidentId} reportado",
                cancellationToken);
            finalAssetStatus = change.NewStatusCode;
        }

        await _auditLog.LogAsync(
            tableName: "incidents",
            recordId: incident.IncidentId,
            action: "INSERT",
            oldData: null,
            newData: new
            {
                incident.IncidentId,
                incident.AssetId,
                incident.IncidentTypeId,
                incident.IncidentPriorityId,
                Status = IncidentStatusCodes.Abierto,
                incident.Title,
                incident.ReportedBy,
                AssetStatusAfter = finalAssetStatus
            },
            cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        return new CreateIncidentResponse(
            incident.IncidentId,
            incident.AssetId,
            IncidentStatusCodes.Abierto,
            finalAssetStatus,
            incident.ReportedAt,
            incident.Version);
    }
}
