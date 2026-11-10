using System.Text.RegularExpressions;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using TecnmInventario.Api.Common.Domain;
using TecnmInventario.Api.Data;

namespace TecnmInventario.Api.Features.Auth.Register;

public class RegisterValidator : AbstractValidator<RegisterCommand>
{
    /// <summary>bcrypt solo usa los primeros 72 bytes de la contraseña.</summary>
    public const int MaxPasswordBytes = 72;

    // U2: tras normalizar (minúsculas), solo ASCII sin espacios.
    private static readonly Regex UsernameFormat = new("^[a-z0-9._-]+$", RegexOptions.CultureInvariant);

    private readonly TecnmInventarioDbContext _context;

    public RegisterValidator(TecnmInventarioDbContext context)
    {
        _context = context;

        RuleFor(x => x.Username)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("El usuario es obligatorio.")
            .MaximumLength(DbFieldLengths.Users.Username)
            // N3: con '@' el login lo buscaría como correo.
            .Must(u => u is null || !u.Contains('@')).WithMessage("El usuario no puede contener '@'.")
            .Must(u => u is null || UsernameFormat.IsMatch(Normalize(u)))
                .WithMessage("El usuario solo puede contener letras sin acentos, números, punto, guion y guion bajo.")
            .MustAsync(BeUniqueUsername).WithMessage("Ese nombre de usuario ya está en uso.");

        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("El correo es obligatorio.")
            .MaximumLength(DbFieldLengths.Users.Email)
            .EmailAddress().WithMessage("El correo no tiene un formato válido.")
            // U3: igual que el username, el correo se guarda y se compara en ASCII.
            .Must(e => e is null || e.All(char.IsAscii))
                .WithMessage("El correo solo puede contener caracteres ASCII (sin acentos ni ñ).")
            .MustAsync(BeUniqueEmail).WithMessage("Ese correo ya está registrado.");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("La contraseña es obligatoria.")
            .MinimumLength(8).WithMessage("La contraseña debe tener al menos 8 caracteres.")
            // P5: bcrypt ignora lo que pase de 72 bytes (no caracteres: la ñ,
            // los acentos y los símbolos ocupan más de un byte en UTF-8).
            .Must(p => p is null || System.Text.Encoding.UTF8.GetByteCount(p) <= MaxPasswordBytes)
                .WithMessage($"La contraseña no puede superar {MaxPasswordBytes} bytes " +
                             "(las letras con acento, la ñ y los símbolos ocupan más de un byte).");

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

    /// <summary>N1: forma canónica de username y email (la que se guarda).</summary>
    public static string Normalize(string? value) => (value ?? string.Empty).Trim().ToLowerInvariant();

    // N1: unicidad sin distinguir mayúsculas (lower(col) = valor normalizado),
    // también frente a filas históricas guardadas con mayúsculas.
    private async Task<bool> BeUniqueUsername(string username, CancellationToken cancellationToken)
    {
        var normalized = Normalize(username);
        return !await _context.AppUsers.AnyAsync(u => u.Username.ToLower() == normalized, cancellationToken);
    }

    private async Task<bool> BeUniqueEmail(string email, CancellationToken cancellationToken)
    {
        var normalized = Normalize(email);
        return !await _context.AppUsers.AnyAsync(u => u.Email.ToLower() == normalized, cancellationToken);
    }

    private async Task<bool> RoleExists(string roleCode, CancellationToken cancellationToken)
        => await _context.Roles.AnyAsync(r => r.Code == roleCode && !r.IsDeleted, cancellationToken);
}
