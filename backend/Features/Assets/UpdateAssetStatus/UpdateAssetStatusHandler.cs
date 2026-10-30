using Mediator;
using Microsoft.EntityFrameworkCore;
using TecnmInventario.Api.Common.Audit;
using TecnmInventario.Api.Common.Domain;
using TecnmInventario.Api.Common.Errors;
using TecnmInventario.Api.Common.Security;
using TecnmInventario.Api.Data;

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

        var previousStatusCode = asset.AssetStatus.Code;

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

        _context.Entry(asset).Property(a => a.Version).OriginalValue = command.ExpectedVersion;

        asset.AssetStatusId = newStatus.AssetStatusId;
        asset.UpdatedBy = _currentUser.UserId;

        // Transacción explícita: el cambio de estado y su registro en
        // audit_log deben ser atómicos (todo o nada). Sin esto, si el
        // INSERT en audit_log falla por cualquier razón, el UPDATE del
        // activo ya quedaría confirmado en la BD sin dejar rastro de
        // auditoría -- justo lo que esto existe para evitar.
        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

        await _context.SaveChangesAsync(cancellationToken);
        await _context.Entry(asset).ReloadAsync(cancellationToken);

        // action debe ser uno de los valores que acepta el CHECK constraint
        // de audit_log (INSERT/UPDATE/DELETE); el detalle real del cambio
        // (a qué estado, por qué) va en new_data, no en el nombre de la acción.
        await _auditLog.LogAsync(
            tableName: "assets",
            recordId: asset.AssetId,
            action: "UPDATE",
            oldData: new { Status = previousStatusCode },
            newData: new { Status = command.NewStatusCode, command.Reason },
            cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        return new UpdateAssetStatusResponse(asset.AssetId, command.NewStatusCode, asset.Version);
    }
}
