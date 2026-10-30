namespace TecnmInventario.Api.Features.Procurement.Acquisitions.GetAcquisitions;

public sealed record GetAcquisitionsResponse(
    long AcquisitionId,
    long? SupplierId,
    string? SupplierName,
    string? PurchaseOrder,
    string? InvoiceNumber,
    DateOnly? AcquiredAt,
    decimal? TotalAmount,
    string CurrencyCode,
    int AssetCount,
    int Version);
