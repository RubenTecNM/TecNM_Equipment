namespace TecnmInventario.Api.Common.Security;

/// <summary>
/// Sesión por cookies y proveedores federados (sección "Auth").
/// Los ClientSecret van en user-secrets, nunca en appsettings.
/// Un proveedor sin ClientId no se registra ni aparece en el login.
/// </summary>
public class AuthOptions
{
    public const string SectionName = "Auth";

    /// <summary>Origen de la SPA, destino de la redirección tras el login federado.</summary>
    public string SpaBaseUrl { get; set; } = string.Empty;

    public int RefreshTokenDays { get; set; } = 7;

    /// <summary>
    /// Segundos durante los que reusar un refresh recién rotado se trata como
    /// carrera entre pestañas y no como robo (que revoca toda la familia).
    /// </summary>
    public int RefreshReuseGraceSeconds { get; set; } = 5;

    /// <summary>Flag Secure de las cookies; solo se desactiva en Development (HTTP).</summary>
    public bool CookieSecure { get; set; } = true;

    /// <summary>
    /// Dominios de correo (exactos, sin "@") con los que una identidad de
    /// Microsoft/Entra puede iniciar sesión o vincularse. Vacío = ninguno.
    /// Los valores reales van en user-secrets o variables de entorno.
    /// </summary>
    public string[] AllowedEmailDomains { get; set; } = [];

    public EntraOptions Entra { get; set; } = new();

    public GoogleOptions Google { get; set; } = new();

    public sealed class EntraOptions
    {
        public string TenantId { get; set; } = string.Empty;
        public string ClientId { get; set; } = string.Empty;
        public string ClientSecret { get; set; } = string.Empty;

        public bool IsConfigured =>
            !string.IsNullOrWhiteSpace(TenantId) && !string.IsNullOrWhiteSpace(ClientId);
    }

    public sealed class GoogleOptions
    {
        public string ClientId { get; set; } = string.Empty;
        public string ClientSecret { get; set; } = string.Empty;

        /// <summary>Dominio de Google Workspace permitido (claim "hd"); vacío = cualquiera.</summary>
        public string HostedDomain { get; set; } = string.Empty;

        public bool IsConfigured => !string.IsNullOrWhiteSpace(ClientId);
    }
}
