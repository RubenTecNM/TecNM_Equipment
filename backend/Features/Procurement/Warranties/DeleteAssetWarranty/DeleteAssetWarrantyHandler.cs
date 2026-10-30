using Mediator;
using Microsoft.EntityFrameworkCore;
using TecnmInventario.Api.Common.Deletion;
using TecnmInventario.Api.Data;

namespace TecnmInventario.Api.Features.Procurement.Warranties.DeleteAssetWarranty;

public sealed class DeleteAssetWarrantyHandler
    : ICommandHandler<DeleteAssetWarrantyCommand, DeleteAssetWarrantyResponse>
{
    private readonly TecnmInventarioDbContext _context;
    private readonly ISoftDeleteAuditor _softDeleteAuditor;

    public DeleteAssetWarrantyHandler(TecnmInventarioDbContext context, ISoftDeleteAuditor softDeleteAuditor)
    {
        _context = context;
        _softDeleteAuditor = softDeleteAuditor;
    }

    public async ValueTask<DeleteAssetWarrantyResponse> Handle(
        DeleteAssetWarrantyCommand command, CancellationToken cancellationToken)
    {
        var warranty = await _context.AssetWarranties
            .FirstOrDefaultAsync(w => w.WarrantyId == command.WarrantyId, cancellationToken);

        if (warranty is null)
        {
            throw new KeyNotFoundException("La garantía solicitada no existe.");
        }

        var beforeSnapshot = new
        {
            warranty.WarrantyId,
            warranty.AssetId,
            warranty.SupplierId,
            warranty.WarrantyNumber,
            warranty.StartsOn,
            warranty.EndsOn,
            warranty.CreatedAt,
            warranty.CreatedBy,
            warranty.Version
        };

        await _softDeleteAuditor.DeleteAsync(
            warranty,
            recordId: warranty.WarrantyId,
            expectedVersion: command.ExpectedVersion,
            tableName: "asset_warranties",
            snapshotBeforeDelete: beforeSnapshot,
            cancellationToken);

        return new DeleteAssetWarrantyResponse(warranty.WarrantyId, true);
    }
}
