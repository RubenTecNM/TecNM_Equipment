using FluentValidation;

namespace TecnmInventario.Api.Features.Procurement.Suppliers.DeleteSupplier;

public class DeleteSupplierValidator : AbstractValidator<DeleteSupplierCommand>
{
    public DeleteSupplierValidator()
    {
        RuleFor(x => x.ExpectedVersion)
            .GreaterThan(0)
            .WithMessage("Debe indicar la versión del proveedor que está eliminando.");
    }
}
