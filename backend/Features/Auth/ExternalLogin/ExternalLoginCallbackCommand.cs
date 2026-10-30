using Mediator;

namespace TecnmInventario.Api.Features.Auth.ExternalLogin;

/// <summary>
/// Cierra el login federado: lee la identidad que OIDC dejó en el esquema
/// "External", la vincula con un app_user existente y abre la sesión.
/// Siempre termina en una redirección a la SPA (éxito o /login?error=...).
/// </summary>
public sealed record ExternalLoginCallbackCommand(string? ReturnUrl)
    : ICommand<ExternalLoginCallbackResponse>;
