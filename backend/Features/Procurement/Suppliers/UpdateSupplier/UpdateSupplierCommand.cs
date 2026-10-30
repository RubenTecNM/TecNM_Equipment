using Mediator;

namespace TecnmInventario.Api.Features.Procurement.Suppliers.UpdateSupplier;

public sealed record UpdateSupplierCommand(
    long SupplierId,
    int ExpectedVersion,
    string Name,
    string? TaxId,
    string? ContactName,
    string? Email,
    string? Phone,
    string? Address,
    string? Notes) : ICommand<UpdateSupplierResponse>, Common.Behaviors.ITransactionalCommand;
