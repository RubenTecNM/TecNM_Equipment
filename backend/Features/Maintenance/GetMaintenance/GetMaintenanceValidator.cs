using FluentValidation;
using TecnmInventario.Api.Features.Maintenance.Shared;

namespace TecnmInventario.Api.Features.Maintenance.GetMaintenance;

public class GetMaintenanceValidator : AbstractValidator<GetMaintenanceQuery>
{
    public GetMaintenanceValidator()
    {
        RuleForEach(x => x.Status)
            .Must(s => s is not null && MaintenanceStatusCodes.All.Contains(s.Trim().ToUpperInvariant()))
            .WithMessage("El estado de mantenimiento indicado no es válido. Valores: PROGRAMADO, EN_CURSO, COMPLETADO, CANCELADO.")
            .When(x => x.Status is not null);

        RuleFor(x => x.Search)
            .MaximumLength(200)
            .When(x => x.Search is not null);
    }
}
