namespace TecnmInventario.Api.Features.Auth.ExternalLogin;

/// <summary>
/// La ruta de regreso viaja por el proveedor externo: solo se aceptan rutas
/// locales de la SPA ("/activos"), nunca URLs absolutas ni "//host" (open redirect).
/// </summary>
public static class ExternalLoginReturnUrl
{
    public const string Default = "/";

    public static bool IsLocal(string url) =>
        url.StartsWith('/') &&
        !url.StartsWith("//", StringComparison.Ordinal) &&
        !url.StartsWith("/\\", StringComparison.Ordinal);

    public static string Sanitize(string? url) =>
        !string.IsNullOrEmpty(url) && IsLocal(url) ? url : Default;
}
