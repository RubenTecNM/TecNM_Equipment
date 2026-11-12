using FluentValidation;

namespace TecnmInventario.Api.Features.Maintenance.CancelMaintenance;

public class CancelMaintenanceValidator : AbstractValidator<CancelMaintenanceCommand>
{
    public const int MaxReasonLength = 500;

    public CancelMaintenanceValidator()
    {
        RuleFor(x => x.ExpectedVersion)
            .GreaterThan(0).WithMessage("Debe indicar la versión del mantenimiento que está viendo (expectedVersion).");

        RuleFor(x => x.Reason)
            .Must(r => !string.IsNullOrWhiteSpace(r)).WithMessage("El motivo de la cancelación es obligatorio.")
            .Must(r => r is null || r.Trim().Length <= MaxReasonLength)
                .WithMessage($"El motivo no puede superar {MaxReasonLength} caracteres.");
    }
}
