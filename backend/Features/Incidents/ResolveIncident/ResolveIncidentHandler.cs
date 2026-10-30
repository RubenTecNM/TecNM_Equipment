using Mediator;
using Microsoft.EntityFrameworkCore;
using TecnmInventario.Api.Common.Audit;
using TecnmInventario.Api.Common.Domain;
using TecnmInventario.Api.Common.Errors;
using TecnmInventario.Api.Common.Security;
using TecnmInventario.Api.Features.Assets.Shared;
using TecnmInventario.Api.Data;

namespace TecnmInventario.Api.Features.Incidents.ResolveIncident;

public sealed class ResolveIncidentHandler : ICommandHandler<ResolveIncidentCommand, ResolveIncidentResponse>
{
    private readonly TecnmInventarioDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IAuditLogService _auditLog;
    private readonly IAssetStatusChanger _assetStatusChanger;

    public ResolveIncidentHandler(
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

    public async ValueTask<ResolveIncidentResponse> Handle(
        ResolveIncidentCommand command, CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId ?? throw new UnauthorizedAccessException();

        var incident = await _context.Incidents
            .Include(i => i.IncidentStatus)
            .Include(i => i.Asset).ThenInclude(a => a.AssetStatus)
            .FirstOrDefaultAsync(i => i.IncidentId == command.IncidentId, cancellationToken)
            ?? throw new KeyNotFoundException("El incidente solicitado no existe.");

        var previousStatusCode = incident.IncidentStatus.Code;
        if (!IncidentStatusCodes.Open.Contains(previousStatusCode))
        {
            throw new BusinessRuleViolationException(
                "Solo se puede resolver un incidente abierto, en diagnóstico o en reparación.");
        }

        var resolvedStatusId = await _context.IncidentStatuses
            .Where(s => s.Code == IncidentStatusCodes.Resuelto && !s.IsDeleted)
            .Select(s => s.IncidentStatusId)
            .FirstAsync(cancellationToken);

        var solution = command.Solution.Trim();
        var diagnosis = string.IsNullOrWhiteSpace(command.Diagnosis) ? null : command.Diagnosis.Trim();

        _context.Entry(incident).Property(i => i.Version).OriginalValue = command.ExpectedVersion;
        incident.IncidentStatusId = resolvedStatusId;
        incident.Solution = solution;
        if (diagnosis is not null)
        {
            incident.Diagnosis = diagnosis;
        }
        incident.ResolvedAt = DateTime.UtcNow;
        incident.UpdatedBy = userId;

        // Transacción explícita: la resolución del incidente, la liberación del
        // activo (uno o dos saltos) y todas las filas de audit_log son atómicas.
        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

        await _context.SaveChangesAsync(cancellationToken);
        await _context.Entry(incident).ReloadAsync(cancellationToken);

        var asset = incident.Asset;
        var assetStatusCode = asset.AssetStatus.Code;

        // El activo no se libera mientras quede otro incidente abierto sobre él.
        var openCodes = IncidentStatusCodes.Open.ToArray();
        var otherOpenIncidents = await _context.Incidents.CountAsync(
            i => i.AssetId == incident.AssetId
                 && i.IncidentId != incident.IncidentId
                 && openCodes.Contains(i.IncidentStatus.Code),
            cancellationToken);

        var finalAssetStatus = assetStatusCode;
        var released = false;

        if (otherOpenIncidents == 0
            && (assetStatusCode == AssetStatusCodes.ConFalla
                || assetStatusCode == AssetStatusCodes.EnMantenimiento))
        {
            var hasActiveAssignment = await _context.AssetAssignments
                .AnyAsync(a => a.AssetId == incident.AssetId && a.ReturnedAt == null, cancellationToken);

            var target = hasActiveAssignment ? AssetStatusCodes.Asignado : AssetStatusCodes.Disponible;
            var reason = $"Incidente #{incident.IncidentId} resuelto";

            // La matriz exige pasar por EN_MANTENIMIENTO antes de volver a circular.
            if (assetStatusCode == AssetStatusCodes.ConFalla)
            {
                await _assetStatusChanger.ChangeAsync(
                    asset, AssetStatusCodes.EnMantenimiento, reason + " (paso intermedio)", cancellationToken);
            }

            var change = await _assetStatusChanger.ChangeAsync(asset, target, reason, cancellationToken);
            finalAssetStatus = change.NewStatusCode;
            released = true;
        }

        await _auditLog.LogAsync(
            tableName: "incidents",
            recordId: incident.IncidentId,
            action: "UPDATE",
            oldData: new { Status = previousStatusCode },
            newData: new
            {
                Status = IncidentStatusCodes.Resuelto,
                incident.Solution,
                incident.Diagnosis,
                incident.ResolvedAt,
                AssetStatusAfter = finalAssetStatus,
                OtherOpenIncidents = otherOpenIncidents
            },
            cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        return new ResolveIncidentResponse(
            incident.IncidentId,
            IncidentStatusCodes.Resuelto,
            finalAssetStatus,
            released,
            otherOpenIncidents,
            incident.ResolvedAt,
            incident.Version);
    }
}
