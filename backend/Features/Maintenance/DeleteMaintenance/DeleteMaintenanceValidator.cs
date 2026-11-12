using FluentValidation;

namespace TecnmInventario.Api.Features.Maintenance.DeleteMaintenance;

public class DeleteMaintenanceValidator : AbstractValidator<DeleteMaintenanceCommand>
{
    public DeleteMaintenanceValidator()
    {
        RuleFor(x => x.ExpectedVersion)
            .GreaterThan(0).WithMessage("Debe indicar la versión del mantenimiento que está viendo (expectedVersion).");
    }
}
