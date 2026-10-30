namespace TecnmInventario.Api.Features.Auth.ExternalLogin;

/// <param name="Scheme">Esquema OIDC registrado (microsoft | google).</param>
/// <param name="CallbackUri">Ruta local a la que vuelve el middleware OIDC.</param>
public sealed record StartExternalLoginResponse(string Scheme, string CallbackUri);
