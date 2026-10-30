using FluentValidation;
using Microsoft.EntityFrameworkCore;
using TecnmInventario.Api.Common.Domain;
using TecnmInventario.Api.Data;

namespace TecnmInventario.Api.Features.Auth.Register;

public class RegisterValidator : AbstractValidator<RegisterCommand>
{
    private readonly TecnmInventarioDbContext _context;

    public RegisterValidator(TecnmInventarioDbContext context)
    {
        _context = context;

        RuleFor(x => x.Username)
            .NotEmpty().WithMessage("El usuario es obligatorio.")
            .MaximumLength(DbFieldLengths.Users.Username)
            .MustAsync(BeUniqueUsername).WithMessage("Ese nombre de usuario ya está en uso.");

        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("El correo es obligatorio.")
            .MaximumLength(DbFieldLengths.Users.Email)
            .EmailAddress().WithMessage("El correo no tiene un formato válido.")
            .MustAsync(BeUniqueEmail).WithMessage("Ese correo ya está registrado.");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("La contraseña es obligatoria.")
            .MinimumLength(8).WithMessage("La contraseña debe tener al menos 8 caracteres.");

        RuleFor(x => x.FirstName)
            .NotEmpty().WithMessage("El nombre es obligatorio.")
            .MaximumLength(DbFieldLengths.Users.FirstName);

        RuleFor(x => x.LastName)
            .NotEmpty().WithMessage("El apellido es obligatorio.")
            .MaximumLength(DbFieldLengths.Users.LastName);

        RuleFor(x => x.RoleCode)
            .NotEmpty().WithMessage("El rol es obligatorio.")
            .MustAsync(RoleExists).WithMessage("El rol especificado no existe.");
    }

    private async Task<bool> BeUniqueUsername(string username, CancellationToken cancellationToken)
        => !await _context.AppUsers.AnyAsync(u => u.Username == username, cancellationToken);

    private async Task<bool> BeUniqueEmail(string email, CancellationToken cancellationToken)
        => !await _context.AppUsers.AnyAsync(u => u.Email == email, cancellationToken);

    private async Task<bool> RoleExists(string roleCode, CancellationToken cancellationToken)
        => await _context.Roles.AnyAsync(r => r.Code == roleCode && !r.IsDeleted, cancellationToken);
}
