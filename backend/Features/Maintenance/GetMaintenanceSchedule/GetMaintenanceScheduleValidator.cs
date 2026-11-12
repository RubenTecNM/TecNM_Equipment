using FluentValidation;

namespace TecnmInventario.Api.Features.Maintenance.GetMaintenanceSchedule;

public class GetMaintenanceScheduleValidator : AbstractValidator<GetMaintenanceScheduleQuery>
{
    public const int MinDays = 1;
    public const int MaxDays = 365;

    public GetMaintenanceScheduleValidator()
    {
        RuleFor(x => x.Days)
            .InclusiveBetween(MinDays, MaxDays)
            .WithMessage($"La ventana debe estar entre {MinDays} y {MaxDays} días.")
            .When(x => x.Days is not null);
    }
}
