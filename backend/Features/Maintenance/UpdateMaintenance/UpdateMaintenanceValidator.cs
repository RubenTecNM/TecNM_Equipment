using FluentValidation;

namespace TecnmInventario.Api.Features.Maintenance.UpdateMaintenance;

// Las comprobaciones que dependen del estado guardado (tipo existente, técnico válido)
// viven en el handler: solo se aplican cuando el valor realmente cambia, para que
// reenviar un valor sin cambios nunca falle.
public class UpdateMaintenanceValidator : AbstractValidator<UpdateMaintenanceCommand>
{
    public UpdateMaintenanceValidator()
    {
        RuleFor(x => x.ExpectedVersion)
            .GreaterThan(0).WithMessage("Debe indicar la versión del mantenimiento que está viendo (expectedVersion).");

        RuleFor(x => x.Description)
            .MaximumLength(4000)
            .When(x => x.Description is not null);
    }
}
