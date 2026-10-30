using Mediator;
using Microsoft.EntityFrameworkCore;
using TecnmInventario.Api.Common.Deletion;
using TecnmInventario.Api.Data;

namespace TecnmInventario.Api.Features.Procurement.Acquisitions.DeleteAcquisition;

public sealed class DeleteAcquisitionHandler : ICommandHandler<DeleteAcquisitionCommand, DeleteAcquisitionResponse>
{
    private readonly TecnmInventarioDbContext _context;
    private readonly ISoftDeleteAuditor _softDeleteAuditor;

    public DeleteAcquisitionHandler(TecnmInventarioDbContext context, ISoftDeleteAuditor softDeleteAuditor)
    {
        _context = context;
        _softDeleteAuditor = softDeleteAuditor;
    }

    public async ValueTask<DeleteAcquisitionResponse> Handle(
        DeleteAcquisitionCommand command, CancellationToken cancellationToken)
    {
        var acquisition = await _context.Acquisitions
            .Include(a => a.AcquisitionAssets)
            .FirstOrDefaultAsync(a => a.AcquisitionId == command.AcquisitionId, cancellationToken);

        if (acquisition is null)
        {
            throw new KeyNotFoundException("La adquisición solicitada no existe.");
        }

        // Snapshot incluye los AcquisitionAssets: acquisition_assets no tiene
        // columnas de auditoría propias (es una tabla puente pura), así que
        // esta es la única forma de conservar en audit_log qué activos y
        // costos formaban parte de la compra que se dio de baja.
        var beforeSnapshot = new
        {
            acquisition.AcquisitionId,
            acquisition.SupplierId,
            acquisition.PurchaseOrder,
            acquisition.InvoiceNumber,
            acquisition.TotalAmount,
            acquisition.CurrencyCode,
            acquisition.CreatedAt,
            acquisition.CreatedBy,
            acquisition.Version,
            Assets = acquisition.AcquisitionAssets.Select(aa => new { aa.AssetId, aa.UnitCost }).ToList()
        };

        await _softDeleteAuditor.DeleteAsync(
            acquisition,
            recordId: acquisition.AcquisitionId,
            expectedVersion: command.ExpectedVersion,
            tableName: "acquisitions",
            snapshotBeforeDelete: beforeSnapshot,
            cancellationToken);

        return new DeleteAcquisitionResponse(acquisition.AcquisitionId, true);
    }
}
