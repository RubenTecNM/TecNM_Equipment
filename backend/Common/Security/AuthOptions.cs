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

    /// <summary>Flag Secure de las cookies; solo se desactiva en Development (HTTP).</summary>
    public bool CookieSecure { get; set; } = true;

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
