namespace TecnmInventario.Api.Common.Security;

/// <summary>
/// Nombres y esquemas compartidos por la sesión basada en cookies.
/// </summary>
public static class AuthCookies
{
    /// <summary>JWT de acceso. HttpOnly, Path=/api.</summary>
    public const string AccessToken = "tecnm_at";

    /// <summary>Refresh token opaco. HttpOnly, SameSite=Strict, Path=/api/auth.</summary>
    public const string RefreshToken = "tecnm_rt";

    /// <summary>Token CSRF (double-submit). Legible por JS para el header X-CSRF-Token.</summary>
    public const string Csrf = "tecnm_csrf";

    public const string CsrfHeader = "X-CSRF-Token";

    /// <summary>Esquema cookie temporal donde OIDC deja la identidad externa.</summary>
    public const string ExternalScheme = "External";

    public const string MicrosoftScheme = "microsoft";

    public const string GoogleScheme = "google";
}
