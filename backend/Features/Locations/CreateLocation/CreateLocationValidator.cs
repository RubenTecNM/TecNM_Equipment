using FluentValidation;
using Microsoft.EntityFrameworkCore;
using TecnmInventario.Api.Data;

namespace TecnmInventario.Api.Features.Locations.CreateLocation;

public class CreateLocationValidator : AbstractValidator<CreateLocationCommand>
{
    private readonly TecnmInventarioDbContext _context;

    public CreateLocationValidator(TecnmInventarioDbContext context)
    {
        _context = context;

        RuleFor(x => x.Code)
            .NotEmpty().WithMessage("El código de la ubicación es obligatorio.")
            .MaximumLength(40)
            .MustAsync(BeUniqueCode).WithMessage("Ya existe una ubicación con ese código.");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("El nombre de la ubicación es obligatorio.")
            .MaximumLength(150);

        RuleFor(x => x.LocationTypeId)
            .MustAsync(LocationTypeExists).WithMessage("El tipo de ubicación especificado no existe.");

        RuleFor(x => x.ParentLocationId)
            .MustAsync(ParentExists).WithMessage("La ubicación padre especificada no existe.")
            .When(x => x.ParentLocationId.HasValue);
    }

    // IgnoreQueryFilters: el UNIQUE de code incluye ubicaciones borradas.
    private async Task<bool> BeUniqueCode(string code, CancellationToken ct)
        => !await _context.Locations.IgnoreQueryFilters().AnyAsync(l => l.Code == code, ct);

    private async Task<bool> LocationTypeExists(long id, CancellationToken ct)
        => await _context.LocationTypes.AnyAsync(t => t.LocationTypeId == id && !t.IsDeleted, ct);

    private async Task<bool> ParentExists(long? id, CancellationToken ct)
        => await _context.Locations.AnyAsync(l => l.LocationId == id, ct);
}
