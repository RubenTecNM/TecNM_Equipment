using FluentValidation;

namespace TecnmInventario.Api.Features.Locations.DeleteLocation;

public class DeleteLocationValidator : AbstractValidator<DeleteLocationCommand>
{
    public DeleteLocationValidator()
    {
        RuleFor(x => x.ExpectedVersion)
            .GreaterThan(0)
            .WithMessage("Debe indicar la versión de la ubicación que está eliminando.");
    }
}
