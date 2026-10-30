using Mediator;

namespace TecnmInventario.Api.Features.Procurement.Suppliers.DeleteSupplier;

public sealed record DeleteSupplierCommand(long SupplierId, int ExpectedVersion)
    : ICommand<DeleteSupplierResponse>;
