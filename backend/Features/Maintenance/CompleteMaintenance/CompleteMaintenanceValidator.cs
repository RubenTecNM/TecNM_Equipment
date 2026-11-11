using FluentValidation;
using TecnmInventario.Api.Features.Maintenance.Shared;

namespace TecnmInventario.Api.Features.Maintenance.CompleteMaintenance;

public class CompleteMaintenanceValidator : AbstractValidator<CompleteMaintenanceCommand>
{
    // numeric(14,2): 12 dígitos enteros y 2 decimales.
    private const decimal MaxCost = 999_999_999_999.99m;

    public CompleteMaintenanceValidator()
    {
        RuleFor(x => x.ExpectedVersion)
            .GreaterThan(0).WithMessage("Debe indicar la versión del mantenimiento que está viendo (expectedVersion).");

        RuleFor(x => x.Description)
            .MaximumLength(4000)
            .When(x => x.Description is not null);

        RuleFor(x => x.PartsUsed)
            .MaximumLength(4000)
            .When(x => x.PartsUsed is not null);

        RuleFor(x => x.Cost)
            .GreaterThanOrEqualTo(0).WithMessage("El costo no puede ser negativo.")
            .LessThanOrEqualTo(MaxCost).WithMessage("El costo excede el máximo permitido.")
            .PrecisionScale(14, 2, ignoreTrailingZeros: true)
                .WithMessage("El costo admite como máximo 2 decimales.")
            .When(x => x.Cost is not null);

        RuleFor(x => x.NextMaintenanceAt)
            .Must(d => MaintenanceDates.ToUtc(d!.Value) > DateTime.UtcNow)
            .WithMessage("La fecha del próximo mantenimiento debe ser posterior a la actual.")
            .When(x => x.NextMaintenanceAt is not null);
    }
}
