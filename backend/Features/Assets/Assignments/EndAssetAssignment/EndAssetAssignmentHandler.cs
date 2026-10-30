using Mediator;
using Microsoft.EntityFrameworkCore;
using TecnmInventario.Api.Common.Audit;
using TecnmInventario.Api.Common.Domain;
using TecnmInventario.Api.Common.Errors;
using TecnmInventario.Api.Common.Security;
using TecnmInventario.Api.Core.Entities;
using TecnmInventario.Api.Data;
using TecnmInventario.Api.Features.Assets.Shared;

namespace TecnmInventario.Api.Features.Assets.Assignments.EndAssetAssignment;

public sealed class EndAssetAssignmentHandler
    : ICommandHandler<EndAssetAssignmentCommand, EndAssetAssignmentResponse>
{
    private readonly TecnmInventarioDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IAuditLogService _auditLog;
    private readonly IAssetStatusChanger _statusChanger;

    public EndAssetAssignmentHandler(
        TecnmInventarioDbContext context, ICurrentUserService currentUser,
        IAuditLogService auditLog, IAssetStatusChanger statusChanger)
    {
        _context = context;
        _currentUser = currentUser;
        _auditLog = auditLog;
        _statusChanger = statusChanger;
    }

    public async ValueTask<EndAssetAssignmentResponse> Handle(
        EndAssetAssignmentCommand command, CancellationToken cancellationToken)
    {
        var assignment = await _context.AssetAssignments
            .FirstOrDefaultAsync(
                a => a.AssetAssignmentId == command.AssetAssignmentId &&
                     a.AssetId == command.AssetId,
                cancellationToken);

        if (assignment is null)
        {
            throw new KeyNotFoundException("La asignación solicitada no existe.");
        }

        var asset = await _context.Assets
            .Include(a => a.AssetStatus)
            .FirstOrDefaultAsync(
                a => a.AssetId == command.AssetId && !a.IsDeleted, cancellationToken);

        if (asset is null)
        {
            throw new KeyNotFoundException("El activo solicitado no existe.");
        }

        // Concurrencia optimista: la versión que el cliente vio debe
        // coincidir con la actual ANTES de abrir la transacción; así un
        // 409 no deja filas parciales ni transacción abierta. Nota: con
        // incidentes abiertos el activo no se UPDATEa en esta operación,
        // así que solo esta comparación explícita detecta la versión vieja
        // (el chequeo de EF en el UPDATE nunca llegaría a correr). Va
        // antes que la regla "ya fue devuelta" para que un reintento con
        // versión vieja sea 409 (conflicto), no un falso 400 de negocio.
        if (asset.Version != command.ExpectedVersion)
        {
            throw new DbUpdateConcurrencyException(
                "El activo fue modificado por otro usuario mientras tanto.");
        }

        if (assignment.ReturnedAt is not null)
        {
            throw new BusinessRuleViolationException("La asignación ya fue devuelta.");
        }

        assignment.ReturnedAt = DateTime.UtcNow;
        assignment.UpdatedAt = DateTime.UtcNow;
        assignment.UpdatedBy = _currentUser.UserId;

        // Transacción explícita: la devolución, la posible liberación del
        // activo y el rastro en audit_log son atómicos (todo o nada).
        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

        await _context.SaveChangesAsync(cancellationToken);

        // Solo un activo ASIGNADO sin incidentes abiertos vuelve a DISPONIBLE.
        // En cualquier otro estado (CON_FALLA, EN_MANTENIMIENTO, DADO_DE_BAJA)
        // conserva su estado.
        if (asset.AssetStatus.Code == AssetStatusCodes.Asignado)
        {
            var openCodes = IncidentStatusCodes.Open.ToArray();
            var hasOpenIncidents = await _context.Incidents.AnyAsync(
                i => i.AssetId == asset.AssetId && openCodes.Contains(i.IncidentStatus.Code),
                cancellationToken);

            if (!hasOpenIncidents)
            {
                await _statusChanger.ChangeAsync(
                    asset, AssetStatusCodes.Disponible, "Devolución de asignación", cancellationToken);
            }
        }

        await _auditLog.LogAsync(
            tableName: "asset_assignments",
            recordId: assignment.AssetAssignmentId,
            action: "UPDATE",
            oldData: new { ReturnedAt = (DateTime?)null },
            newData: new { assignment.ReturnedAt },
            cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        // El trigger touch_version subió version en la BD: releer con
        // AsNoTracking para devolver la versión nueva (el contexto
        // trackeado conserva la de carga).
        var assetVersion = await _context.Assets
            .AsNoTracking()
            .Where(a => a.AssetId == asset.AssetId)
            .Select(a => a.Version)
            .FirstAsync(cancellationToken);

        return new EndAssetAssignmentResponse(
            assignment.AssetAssignmentId,
            assignment.ReturnedAt.Value,
            assetVersion);
    }
}
