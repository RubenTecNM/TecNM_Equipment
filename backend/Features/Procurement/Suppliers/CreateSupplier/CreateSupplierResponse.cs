namespace TecnmInventario.Api.Features.Procurement.Suppliers.CreateSupplier;

public sealed record CreateSupplierResponse(
    long SupplierId,
    string Name,
    int Version);
