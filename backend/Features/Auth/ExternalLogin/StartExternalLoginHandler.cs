using Mediator;
using Microsoft.Extensions.Options;
using TecnmInventario.Api.Common.Errors;
using TecnmInventario.Api.Common.Security;

namespace TecnmInventario.Api.Features.Auth.ExternalLogin;

public sealed class StartExternalLoginHandler : IQueryHandler<StartExternalLoginQuery, StartExternalLoginResponse>
{
    private readonly AuthOptions _options;

    public StartExternalLoginHandler(IOptions<AuthOptions> options)
    {
        _options = options.Value;
    }

    public ValueTask<StartExternalLoginResponse> Handle(StartExternalLoginQuery query, CancellationToken cancellationToken)
    {
        var configured = query.Provider switch
        {
            AuthCookies.MicrosoftScheme => _options.Entra.IsConfigured,
            AuthCookies.GoogleScheme => _options.Google.IsConfigured,
            _ => false
        };

        if (!configured)
        {
            throw new BusinessRuleViolationException("El inicio de sesión con este proveedor no está habilitado.");
        }

        var returnUrl = ExternalLoginReturnUrl.Sanitize(query.ReturnUrl);
        var callback = $"/api/auth/external/callback?returnUrl={Uri.EscapeDataString(returnUrl)}";

        return ValueTask.FromResult(new StartExternalLoginResponse(query.Provider, callback));
    }
}
