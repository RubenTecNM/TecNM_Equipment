using System.Text;
using FluentValidation;
using TecnmInventario.Api.Common.Domain;

namespace TecnmInventario.Api.Features.Auth.Login;

public class LoginValidator : AbstractValidator<LoginCommand>
{
    /// <summary>
    /// Tope anti-abuso (S0a). NO es 72: bcrypt trunca a 72 bytes igual al
    /// crear y al verificar, así que quien se registró con una contraseña más
    /// larga debe poder entrar. El límite de 72 bytes vive solo en Register.
    /// </summary>
    public const int MaxPasswordBytes = 1024;

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
