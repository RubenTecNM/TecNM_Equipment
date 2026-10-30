using FluentValidation;

namespace TecnmInventario.Api.Features.Incidents.DeleteIncident;

public class DeleteIncidentValidator : AbstractValidator<DeleteIncidentCommand>
{
    public DeleteIncidentValidator()
    {
        RuleFor(x => x.ExpectedVersion)
            .GreaterThan(0).WithMessage("Debe indicar la versión del incidente que está viendo (expectedVersion).");
    }
}
