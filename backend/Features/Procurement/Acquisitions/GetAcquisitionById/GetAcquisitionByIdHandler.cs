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
        // D3: la adquisición es historial y no debe perder líneas de compra
        // porque el activo esté borrado lógicamente (AcquisitionAsset.Asset
        // es una navegación requerida). IgnoreQueryFilters quita el filtro
        // "SoftDelete" de TODA la consulta (D2, sin excepción): también deja
        // de aplicarse al propio Acquisition y a Supplier, por eso el Where
        // explícito sobre la raíz. Efecto visible: Supplier se sigue
        // mostrando aunque esté borrado lógicamente (antes salía null).
        var acquisition = await _context.Acquisitions
            .IgnoreQueryFilters(["SoftDelete"])
            .Where(a => !a.IsDeleted)
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
