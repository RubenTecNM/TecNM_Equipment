using FluentValidation;
using TecnmInventario.Api.Common.Security;

namespace TecnmInventario.Api.Features.Auth.ExternalLogin;

public class StartExternalLoginValidator : AbstractValidator<StartExternalLoginQuery>
{
    public StartExternalLoginValidator()
    {
        RuleFor(x => x.Provider)
            .Must(p => p is AuthCookies.MicrosoftScheme or AuthCookies.GoogleScheme)
            .WithMessage("Proveedor no soportado.");

        RuleFor(x => x.ReturnUrl)
            .Must(url => ExternalLoginReturnUrl.IsLocal(url!))
            .When(x => !string.IsNullOrEmpty(x.ReturnUrl))
            .WithMessage("La ruta de regreso debe ser local.");
    }
}
