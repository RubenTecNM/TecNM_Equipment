using FluentValidation;

namespace TecnmInventario.Api.Features.Assets.Assignments.EndAssetAssignment;

public class EndAssetAssignmentValidator : AbstractValidator<EndAssetAssignmentCommand>
{
    public EndAssetAssignmentValidator()
    {
        RuleFor(x => x.ExpectedVersion)
            .GreaterThan(0).WithMessage("Debe indicar la versión del activo que está viendo (expectedVersion).");
    }
}
