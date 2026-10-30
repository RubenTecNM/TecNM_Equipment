using Mediator;

namespace TecnmInventario.Api.Features.Auth.ExternalLogin;

/// <summary>
/// Prepara el challenge OIDC: valida el proveedor y la ruta de regreso de la SPA.
/// El controller convierte la respuesta en Challenge().
/// </summary>
public sealed record StartExternalLoginQuery(string Provider, string? ReturnUrl)
    : IQuery<StartExternalLoginResponse>;
