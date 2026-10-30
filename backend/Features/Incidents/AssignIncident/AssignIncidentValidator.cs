using FluentValidation;

namespace TecnmInventario.Api.Features.Incidents.AssignIncident;

public class AssignIncidentValidator : AbstractValidator<AssignIncidentCommand>
{
    public AssignIncidentValidator()
    {
        RuleFor(x => x.ExpectedVersion)
            .GreaterThan(0).WithMessage("Debe indicar la versión del incidente que está viendo (expectedVersion).");

        RuleFor(x => x.AssignedToUserId)
            .GreaterThan(0).WithMessage("Debe indicar el técnico a asignar.");
    }
}
