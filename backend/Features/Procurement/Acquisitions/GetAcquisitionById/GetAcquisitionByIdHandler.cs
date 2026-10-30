using Mediator;
using Microsoft.EntityFrameworkCore;
using TecnmInventario.Api.Data;

namespace TecnmInventario.Api.Features.Procurement.Acquisitions.GetAcquisitionById;

public sealed class GetAcquisitionByIdHandler : IQueryHandler<GetAcquisitionByIdQuery, GetAcquisitionByIdResponse>
{
    private readonly TecnmInventarioDbContext _context;

    public GetAcquisitionByIdHandler(TecnmInventarioDbContext context)
    {
        _context = context;
    }

    public async ValueTask<GetAcquisitionByIdResponse> Handle(
        GetAcquisitionByIdQuery query, CancellationToken cancellationToken)
    {
        // Include(Supplier): Supplier tiene HasQueryFilter propio (!IsDeleted).
        // EF Core aplica el filtro del tipo incluido automáticamente incluso
        // en un Include sobre una entidad (Acquisition) que no lo tiene -- si
        // el proveedor fue borrado lógicamente, Supplier viene null aquí en
        // vez de lanzar. Se verifica con una prueba real más abajo.
        var acquisition = await _context.Acquisitions
            .Include(a => a.Supplier)
            .Include(a => a.AcquisitionAssets)
                .ThenInclude(aa => aa.Asset)
            .FirstOrDefaultAsync(a => a.AcquisitionId == query.AcquisitionId, cancellationToken);

        if (acquisition is null)
        {
            throw new KeyNotFoundException("La adquisición solicitada no existe.");
        }

        var assetLines = acquisition.AcquisitionAssets
            .Select(aa => new AcquisitionAssetLine(aa.AssetId, aa.Asset.InventoryCode, aa.UnitCost))
            .ToList();

        return new GetAcquisitionByIdResponse(
            acquisition.AcquisitionId,
            acquisition.SupplierId,
            acquisition.Supplier?.Name,
            acquisition.PurchaseOrder,
            acquisition.InvoiceNumber,
            acquisition.AcquiredAt,
            acquisition.Subtotal,
            acquisition.TaxAmount,
            acquisition.TotalAmount,
            acquisition.CurrencyCode,
            acquisition.WarrantyStart,
            acquisition.WarrantyEnd,
            acquisition.Notes,
            acquisition.Version,
            assetLines);
    }
}
