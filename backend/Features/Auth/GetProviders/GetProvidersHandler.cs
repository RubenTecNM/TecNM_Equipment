using Mediator;
using Microsoft.Extensions.Options;
using TecnmInventario.Api.Common.Security;

namespace TecnmInventario.Api.Features.Auth.GetProviders;

public sealed class GetProvidersHandler : IQueryHandler<GetProvidersQuery, GetProvidersResponse>
{
    private readonly AuthOptions _options;

    public GetProvidersHandler(IOptions<AuthOptions> options)
    {
        _options = options.Value;
    }

    public ValueTask<GetProvidersResponse> Handle(GetProvidersQuery query, CancellationToken cancellationToken)
    {
        var providers = new List<string>();
        if (_options.Entra.IsConfigured) providers.Add(AuthCookies.MicrosoftScheme);
        if (_options.Google.IsConfigured) providers.Add(AuthCookies.GoogleScheme);

        return ValueTask.FromResult(new GetProvidersResponse(providers));
    }
}
