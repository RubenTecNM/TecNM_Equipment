using FluentValidation;

namespace TecnmInventario.Api.Features.Procurement.Acquisitions.DeleteAcquisition;

public class DeleteAcquisitionValidator : AbstractValidator<DeleteAcquisitionCommand>
{
    public DeleteAcquisitionValidator()
    {
        RuleFor(x => x.ExpectedVersion)
            .GreaterThan(0)
            .WithMessage("Debe indicar la versión de la adquisición que está eliminando.");
    }
}
