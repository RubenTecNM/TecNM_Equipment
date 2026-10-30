using Mediator;
using Microsoft.EntityFrameworkCore;
using TecnmInventario.Api.Common.Audit;
using TecnmInventario.Api.Common.Domain;
using TecnmInventario.Api.Common.Errors;
using TecnmInventario.Api.Common.Security;
using TecnmInventario.Api.Core.Entities;
using TecnmInventario.Api.Data;

namespace TecnmInventario.Api.Features.Assets.Movements.CreateAssetMovement;

public sealed class CreateAssetMovementHandler
    : ICommandHandler<CreateAssetMovementCommand, CreateAssetMovementResponse>
{
    private readonly TecnmInventarioDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IAuditLogService _auditLog;

    public CreateAssetMovementHandler(
        TecnmInventarioDbContext context, ICurrentUserService currentUser, IAuditLogService auditLog)
    {
        _context = context;
        _currentUser = currentUser;
        _auditLog = auditLog;
    }

    public async ValueTask<CreateAssetMovementResponse> Handle(
        CreateAssetMovementCommand command, CancellationToken cancellationToken)
    {
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
        // 409 no deja filas parciales ni transacción abierta.
        if (asset.Version != command.ExpectedVersion)
        {
            throw new DbUpdateConcurrencyException(
                "El activo fue modificado por otro usuario mientras tanto.");
        }

        // Máquina de estados: solo DISPONIBLE o ASIGNADO pueden reubicarse.
        // Mover no cambia el estado del activo (no se usa AssetStatusTransitions).
        if (asset.AssetStatus.Code is not (AssetStatusCodes.Disponible or AssetStatusCodes.Asignado))
        {
            throw new BusinessRuleViolationException(
                $"No se puede mover un activo en estado {asset.AssetStatus.Code}. " +
                $"Solo {AssetStatusCodes.Disponible} o {AssetStatusCodes.Asignado} pueden reubicarse.");
        }

        if (asset.CurrentLocationId == command.ToLocationId)
        {
            throw new BusinessRuleViolationException(
                "El destino coincide con la ubicación actual del activo.");
        }

        var movedBy = _currentUser.UserId
            ?? throw new ForbiddenAccessException("Se requiere usuario autenticado.");

        var now = DateTime.UtcNow;
        var previousLocationId = asset.CurrentLocationId;

        // El movimiento es el historial: registra origen (ubicación actual
        // desnormalizada) y destino, y actualiza la ubicación del activo.
        var movement = new AssetMovement
        {
            AssetId = asset.AssetId,
            FromLocationId = previousLocationId,
            ToLocationId = command.ToLocationId,
            MovementReasonId = command.MovementReasonId,
            MovedBy = movedBy,
            MovedAt = command.MovedAt ?? now,
            Notes = command.Notes,
            CreatedAt = now,
            UpdatedAt = now,
            CreatedBy = _currentUser.UserId,
            Version = 1
        };

        asset.CurrentLocationId = command.ToLocationId;
        asset.UpdatedAt = now;
        asset.UpdatedBy = _currentUser.UserId;

        // Transacción explícita: el movimiento, el cambio de ubicación del
        // activo y el rastro en audit_log son atómicos (todo o nada).
        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

        _context.AssetMovements.Add(movement);
        await _context.SaveChangesAsync(cancellationToken);

        // action debe ser INSERT/UPDATE/DELETE (CHECK de audit_log);
        // el detalle va en new_data.
        await _auditLog.LogAsync(
            tableName: "asset_movements",
            recordId: movement.AssetMovementId,
            action: "INSERT",
            oldData: null,
            newData: new
            {
                movement.AssetId,
                FromLocationId = previousLocationId,
                movement.ToLocationId,
                movement.MovementReasonId,
                movement.MovedAt,
            },
            cancellationToken);

        await _auditLog.LogAsync(
            tableName: "assets",
            recordId: asset.AssetId,
            action: "UPDATE",
            oldData: new { LocationId = previousLocationId },
            newData: new { LocationId = command.ToLocationId },
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

        return new CreateAssetMovementResponse(
            movement.AssetMovementId,
            movement.AssetId,
            movement.FromLocationId,
            movement.ToLocationId,
            movement.MovedAt,
            assetVersion);
    }
}
