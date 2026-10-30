using Microsoft.EntityFrameworkCore;
using TecnmInventario.Api.Common.Audit;
using TecnmInventario.Api.Common.Errors;
using TecnmInventario.Api.Common.Security;
using TecnmInventario.Api.Features.Assets.UpdateAssetStatus;
using TecnmInventario.Api.Data;
using TecnmInventario.Api.Core.Entities;

namespace TecnmInventario.Api.Features.Assets.Shared;

public sealed class AssetStatusChanger : IAssetStatusChanger
{
    private readonly TecnmInventarioDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IAuditLogService _auditLog;

    public AssetStatusChanger(
        TecnmInventarioDbContext context, ICurrentUserService currentUser, IAuditLogService auditLog)
    {
        _context = context;
        _currentUser = currentUser;
        _auditLog = auditLog;
    }

    public async Task<AssetStatusChangeResult> ChangeAsync(
        Asset asset, string newStatusCode, string reason, CancellationToken cancellationToken)
    {
        // Guard: sin transacción del llamador, el UPDATE del activo y su fila
        // de audit_log podrían confirmarse por separado (bug ya vivido).
        if (_context.Database.CurrentTransaction is null)
        {
            throw new InvalidOperationException(
                "IAssetStatusChanger requiere una transacción activa iniciada por el llamador.");
        }

        var previousCode = await _context.AssetStatuses
            .Where(s => s.AssetStatusId == asset.AssetStatusId)
            .Select(s => s.Code)
            .FirstAsync(cancellationToken);

        if (!AssetStatusTransitions.IsAllowed(previousCode, newStatusCode))
        {
            throw new BusinessRuleViolationException(
                $"Transición de estado no permitida: {previousCode} -> {newStatusCode}.");
        }

        var newStatusId = await _context.AssetStatuses
            .Where(s => s.Code == newStatusCode && !s.IsDeleted)
            .Select(s => s.AssetStatusId)
            .FirstAsync(cancellationToken);

        asset.AssetStatusId = newStatusId;
        asset.UpdatedBy = _currentUser.UserId;

        await _context.SaveChangesAsync(cancellationToken);

        // El trigger touch_version incrementó version en la BD: releer para
        // que un segundo cambio en la misma transacción no falle con un falso 409.
        await _context.Entry(asset).ReloadAsync(cancellationToken);

        await _auditLog.LogAsync(
            tableName: "assets",
            recordId: asset.AssetId,
            action: "UPDATE",
            oldData: new { Status = previousCode },
            newData: new { Status = newStatusCode, Reason = reason },
            cancellationToken);

        return new AssetStatusChangeResult(previousCode, newStatusCode, asset.Version);
    }
}
