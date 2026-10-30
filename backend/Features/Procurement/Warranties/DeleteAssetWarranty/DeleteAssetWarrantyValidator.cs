using FluentValidation;

namespace TecnmInventario.Api.Features.Procurement.Warranties.DeleteAssetWarranty;

public class DeleteAssetWarrantyValidator : AbstractValidator<DeleteAssetWarrantyCommand>
{
    public DeleteAssetWarrantyValidator()
    {
        RuleFor(x => x.ExpectedVersion)
            .GreaterThan(0)
            .WithMessage("Debe indicar la versión de la garantía que está eliminando.");
    }
}
