using FluentValidation;
using Microsoft.EntityFrameworkCore;
using TecnmInventario.Api.Common.Domain;
using TecnmInventario.Api.Data;

namespace TecnmInventario.Api.Features.Roles.CreateRole;

public class CreateRoleValidator : AbstractValidator<CreateRoleCommand>
{
    private readonly TecnmInventarioDbContext _context;

    public CreateRoleValidator(TecnmInventarioDbContext context)
    {
        _context = context;

        RuleFor(x => x.Code)
            .NotEmpty().WithMessage("El código del rol es obligatorio.")
            .MaximumLength(DbFieldLengths.Roles.Code)
            .Matches("^[A-Z][A-Z0-9_]*$")
                .WithMessage("El código debe ir en mayúsculas, usando letras, números y guion bajo (ej. RESPONSABLE_AREA).")
            .MustAsync(BeUniqueCode).WithMessage("Ese código de rol ya está en uso.");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("El nombre del rol es obligatorio.")
            .MaximumLength(DbFieldLengths.Roles.Name);

        RuleFor(x => x.Description)
            .MaximumLength(255);
    }

    private async Task<bool> BeUniqueCode(string code, CancellationToken ct)
        => !await _context.Roles.AnyAsync(r => r.Code == code && !r.IsDeleted, ct);
}
