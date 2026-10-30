using FluentValidation;

namespace TecnmInventario.Api.Features.Incidents.ResolveIncident;

public class ResolveIncidentValidator : AbstractValidator<ResolveIncidentCommand>
{
    public ResolveIncidentValidator()
    {
        RuleFor(x => x.ExpectedVersion)
            .GreaterThan(0).WithMessage("Debe indicar la versión del incidente que está viendo (expectedVersion).");

        RuleFor(x => x.Solution)
            .NotEmpty().WithMessage("La solución aplicada es obligatoria para resolver un incidente.")
            .MaximumLength(4000);

        RuleFor(x => x.Diagnosis)
            .MaximumLength(4000)
            .When(x => x.Diagnosis is not null);
    }
}
