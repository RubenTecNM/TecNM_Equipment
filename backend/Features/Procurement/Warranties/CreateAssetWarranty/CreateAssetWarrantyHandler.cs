using Mediator;
using TecnmInventario.Api.Common.Audit;
using TecnmInventario.Api.Common.Security;
using TecnmInventario.Api.Data;
using TecnmInventario.Api.Core.Entities;

namespace TecnmInventario.Api.Features.Procurement.Warranties.CreateAssetWarranty;

public sealed class CreateAssetWarrantyHandler
    : ICommandHandler<CreateAssetWarrantyCommand, CreateAssetWarrantyResponse>
{
    private readonly TecnmInventarioDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IAuditLogService _auditLog;

    public CreateAssetWarrantyHandler(
        TecnmInventarioDbContext context,
        ICurrentUserService currentUser,
        IAuditLogService auditLog)
    {
        _context = context;
        _currentUser = currentUser;
        _auditLog = auditLog;
    }

    public async ValueTask<CreateAssetWarrantyResponse> Handle(
        CreateAssetWarrantyCommand command, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;

        var warranty = new AssetWarranty
        {
            AssetId = command.AssetId,
            SupplierId = command.SupplierId,
            WarrantyNumber = string.IsNullOrWhiteSpace(command.WarrantyNumber) ? null : command.WarrantyNumber.Trim(),
            StartsOn = command.StartsOn,
            EndsOn = command.EndsOn,
            Coverage = command.Coverage,
            Terms = command.Terms,
            CreatedAt = now,
            UpdatedAt = now,
            CreatedBy = _currentUser.UserId,
            IsDeleted = false,
            Version = 1
        };

        _context.AssetWarranties.Add(warranty);
        await _context.SaveChangesAsync(cancellationToken);

        await _auditLog.LogAsync(
            tableName: "asset_warranties",
            recordId: warranty.WarrantyId,
            action: "INSERT",
            oldData: null,
            newData: new
            {
                warranty.AssetId,
                warranty.SupplierId,
                warranty.WarrantyNumber,
                warranty.StartsOn,
                warranty.EndsOn,
                warranty.Coverage,
                warranty.Terms,
                warranty.Version
            },
            cancellationToken);

        return new CreateAssetWarrantyResponse(warranty.WarrantyId, warranty.AssetId, warranty.Version);
    }
}
