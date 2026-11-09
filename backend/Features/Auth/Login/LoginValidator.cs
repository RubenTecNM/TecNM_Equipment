using System.Text;
using FluentValidation;
using TecnmInventario.Api.Common.Domain;

namespace TecnmInventario.Api.Features.Auth.Login;

public class LoginValidator : AbstractValidator<LoginCommand>
{
    /// <summary>bcrypt solo usa los primeros 72 bytes de la contraseña.</summary>
    public const int MaxPasswordBytes = 72;

    public LoginValidator()
    {
        // P5: el identificador más largo posible es un email (254).
        RuleFor(x => x.UsernameOrEmail)
            .NotEmpty().WithMessage("El usuario o correo es obligatorio.")
            .MaximumLength(DbFieldLengths.Users.Email)
                .WithMessage($"El usuario o correo no puede superar {DbFieldLengths.Users.Email} caracteres.");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("La contraseña es obligatoria.")
            .Must(p => p is null || Encoding.UTF8.GetByteCount(p) <= MaxPasswordBytes)
                .WithMessage($"La contraseña no puede superar {MaxPasswordBytes} bytes.");
    }
}
