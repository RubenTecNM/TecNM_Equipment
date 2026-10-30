using Mediator;

namespace TecnmInventario.Api.Features.Procurement.Suppliers.CreateSupplier;

public sealed record CreateSupplierCommand(
    string Name,
    string? TaxId,
    string? ContactName,
    string? Email,
    string? Phone,
    string? Address,
    string? Notes) : ICommand<CreateSupplierResponse>, Common.Behaviors.ITransactionalCommand;
