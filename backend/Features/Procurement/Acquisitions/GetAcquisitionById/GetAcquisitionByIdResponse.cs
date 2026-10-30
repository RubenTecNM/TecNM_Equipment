namespace TecnmInventario.Api.Features.Procurement.Acquisitions.GetAcquisitionById;

public sealed record AcquisitionAssetLine(long AssetId, string InventoryCode, decimal? UnitCost);

public sealed record GetAcquisitionByIdResponse(
    long AcquisitionId,
    long? SupplierId,
    string? SupplierName,
    string? PurchaseOrder,
    string? InvoiceNumber,
    DateOnly? AcquiredAt,
    decimal? Subtotal,
    decimal? TaxAmount,
    decimal? TotalAmount,
    string CurrencyCode,
    DateOnly? WarrantyStart,
    DateOnly? WarrantyEnd,
    string? Notes,
    int Version,
    IReadOnlyList<AcquisitionAssetLine> Assets);
