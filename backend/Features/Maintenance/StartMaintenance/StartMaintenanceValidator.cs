using FluentValidation;

namespace TecnmInventario.Api.Features.Maintenance.StartMaintenance;

public class StartMaintenanceValidator : AbstractValidator<StartMaintenanceCommand>
{
    public StartMaintenanceValidator()
    {
        RuleFor(x => x.ExpectedVersion)
            .GreaterThan(0).WithMessage("Debe indicar la versión del mantenimiento que está viendo (expectedVersion).");

        RuleFor(x => x.IncidentId)
            .GreaterThan(0).WithMessage("El incidente indicado no es válido.")
            .When(x => x.IncidentId is not null);
    }
}
