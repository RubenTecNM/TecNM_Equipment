namespace TecnmInventario.Api.Features.Procurement.Suppliers.GetSuppliers;

public sealed record GetSuppliersResponse(
    long SupplierId,
    string Name,
    string? TaxId,
    string? ContactName,
    string? Email,
    string? Phone,
    int Version);
