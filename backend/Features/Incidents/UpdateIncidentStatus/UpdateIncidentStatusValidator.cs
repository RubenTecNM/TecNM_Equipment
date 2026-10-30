using FluentValidation;
using TecnmInventario.Api.Common.Domain;
using TecnmInventario.Api.Features.Incidents.Shared;

namespace TecnmInventario.Api.Features.Incidents.UpdateIncidentStatus;

public class UpdateIncidentStatusValidator : AbstractValidator<UpdateIncidentStatusCommand>
{
    public UpdateIncidentStatusValidator()
    {
        RuleFor(x => x.ExpectedVersion)
            .GreaterThan(0).WithMessage("Debe indicar la versión del incidente que está viendo (expectedVersion).");

        RuleFor(x => x.NewStatusCode)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("El nuevo estado es obligatorio.")
            .Must(code => code != IncidentStatusCodes.Resuelto)
            .WithMessage("Para resolver un incidente use el endpoint de resolución (requiere la solución aplicada).")
            .Must(code => IncidentStatusTransitions.ManualTargets.Contains(code))
            .WithMessage("El estado indicado no es válido. Permitidos: EN_DIAGNOSTICO, EN_REPARACION, CERRADO.");

        RuleFor(x => x.Reason)
            .MaximumLength(500)
            .When(x => x.Reason is not null);
    }
}
