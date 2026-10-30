using FluentValidation;
using Microsoft.EntityFrameworkCore;
using TecnmInventario.Api.Data;

namespace TecnmInventario.Api.Features.Incidents.CreateIncident;

public class CreateIncidentValidator : AbstractValidator<CreateIncidentCommand>
{
    private readonly TecnmInventarioDbContext _context;

    public CreateIncidentValidator(TecnmInventarioDbContext context)
    {
        _context = context;

        RuleFor(x => x.AssetId)
            .MustAsync(AssetExists).WithMessage("El activo especificado no existe.");

        RuleFor(x => x.IncidentTypeId)
            .MustAsync(TypeExists).WithMessage("El tipo de incidente especificado no existe.");

        RuleFor(x => x.IncidentPriorityId)
            .MustAsync(PriorityExists).WithMessage("La prioridad especificada no existe.");

        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("El título es obligatorio.")
            .MaximumLength(200);

        RuleFor(x => x.Description)
            .MaximumLength(4000)
            .When(x => x.Description is not null);
    }

    private async Task<bool> AssetExists(long id, CancellationToken ct)
        => await _context.Assets.AnyAsync(a => a.AssetId == id && !a.IsDeleted, ct);

    private async Task<bool> TypeExists(long id, CancellationToken ct)
        => await _context.IncidentTypes.AnyAsync(t => t.IncidentTypeId == id && !t.IsDeleted, ct);

    private async Task<bool> PriorityExists(long id, CancellationToken ct)
        => await _context.IncidentPriorities.AnyAsync(p => p.IncidentPriorityId == id && !p.IsDeleted, ct);
}
