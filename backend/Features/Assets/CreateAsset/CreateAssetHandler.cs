using Mediator;
using Microsoft.EntityFrameworkCore;
using TecnmInventario.Api.Common.Audit;
using TecnmInventario.Api.Common.Domain;
using TecnmInventario.Api.Common.Security;
using TecnmInventario.Api.Data;
using TecnmInventario.Api.Core.Entities;

namespace TecnmInventario.Api.Features.Assets.CreateAsset;

public sealed class CreateAssetHandler : ICommandHandler<CreateAssetCommand, CreateAssetResponse>
{
    private readonly TecnmInventarioDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IAuditLogService _auditLog;

    public CreateAssetHandler(
        TecnmInventarioDbContext context,
        ICurrentUserService currentUser,
        IAuditLogService auditLog)
    {
        _context = context;
        _currentUser = currentUser;
        _auditLog = auditLog;
    }

    public async ValueTask<CreateAssetResponse> Handle(CreateAssetCommand command, CancellationToken cancellationToken)
    {
        var assetType = await _context.AssetTypes
            .FirstAsync(t => t.AssetTypeId == command.AssetTypeId && !t.IsDeleted, cancellationToken);

        var initialStatus = await _context.AssetStatuses
            .FirstAsync(s => s.Code == AssetStatusCodes.Disponible && !s.IsDeleted, cancellationToken);

        var now = DateTime.UtcNow;

        var asset = new Asset
        {
            InventoryCode = command.InventoryCode,
            AssetTypeId = assetType.AssetTypeId,
            AssetStatusId = initialStatus.AssetStatusId,
            ManufacturerId = command.ManufacturerId,
            ModelId = command.ModelId,
            CurrentLocationId = command.CurrentLocationId,
            SerialNumber = command.SerialNumber,
            Hostname = command.Hostname,
            Notes = command.Notes,
            CreatedAt = now,
            UpdatedAt = now,
            CreatedBy = _currentUser.UserId,
            IsDeleted = false,
            Version = 1
        };

        _context.Assets.Add(asset);
        await _context.SaveChangesAsync(cancellationToken);

        // El INSERT se audita después del SaveChanges para tener el id
        // generado por la base de datos.
        await _auditLog.LogAsync(
            tableName: "assets",
            recordId: asset.AssetId,
            action: "INSERT",
            oldData: null,
            newData: new
            {
                asset.InventoryCode,
                asset.AssetTypeId,
                asset.AssetStatusId,
                asset.ManufacturerId,
                asset.ModelId,
                asset.CurrentLocationId,
                asset.SerialNumber,
                asset.Hostname,
                asset.Notes,
                asset.Version
            },
            cancellationToken);

        return new CreateAssetResponse(asset.AssetId, asset.InventoryCode, assetType.Code, initialStatus.Code);
    }
}
