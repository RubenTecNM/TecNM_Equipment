using System.Text;

namespace TecnmInventario.Api.Common.Security;

/// <summary>
/// Validaciones de seguridad que deben fallar al arrancar, no en la primera
/// petición: un JWT firmado con una clave débil o cookies sin Secure en
/// producción son configuraciones inseguras que conviene detectar de inmediato.
/// </summary>
public static class SecurityStartupValidator
{
    /// <summary>Mínimo recomendado por RFC 7518 para HMAC-SHA256: 256 bits.</summary>
    public const int MinimumJwtSecretBytes = 32;

    /// <summary>
    /// Fragmentos que delatan un valor de ejemplo o una plantilla copiada sin
    /// sustituir. La comparación es sin distinguir mayúsculas.
    /// </summary>
    public static readonly IReadOnlyList<string> PlaceholderFragments = new[]
    {
        "<",
        "clave-larga",
        "changeme",
        "cambiar",
        "your-secret",
        "tu-secreto",
        "placeholder",
        "secreto-de-ejemplo",
        "example-secret"
    };

    public static void Validate(string? jwtSecret, bool isDevelopment, bool cookieSecure, AuthOptions? authOptions = null)
    {
        ValidateJwtSecret(jwtSecret);
        ValidateCookieSecure(isDevelopment, cookieSecure);
        ValidateExternalProviders(isDevelopment, authOptions ?? new AuthOptions());
    }

    public static void ValidateJwtSecret(string? jwtSecret)
    {
        if (string.IsNullOrWhiteSpace(jwtSecret))
        {
            throw new InvalidOperationException(
                "Falta configurar Jwt:Secret. " +
                "Defínelo con user-secrets: dotnet user-secrets set \"Jwt:Secret\" \"<valor-aleatorio>\" " +
                "o con la variable de entorno Jwt__Secret. Genera uno con: openssl rand -base64 48");
        }

        var normalized = jwtSecret.Trim();

        foreach (var fragment in PlaceholderFragments)
        {
            if (normalized.Contains(fragment, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "Jwt:Secret parece un valor de ejemplo o una plantilla sin sustituir " +
                    $"(contiene '{fragment}'). Genera uno real con: openssl rand -base64 48");
            }
        }

        if (normalized.Length < MinimumJwtSecretBytes ||
            Encoding.UTF8.GetByteCount(normalized) < MinimumJwtSecretBytes)
        {
            throw new InvalidOperationException(
                $"Jwt:Secret es demasiado corto: se requieren al menos {MinimumJwtSecretBytes} bytes UTF-8 " +
                "(256 bits). Genera uno nuevo con: openssl rand -base64 48");
        }
    }

    /// <summary>
    /// Tenants de Entra que aceptan cuentas de cualquier organización (o
    /// personales): con ellos la Authority no restringe quién inicia sesión.
    /// </summary>
    public static readonly IReadOnlyList<string> MultiTenantEntraIds = new[]
    {
        "common",
        "organizations",
        "consumers"
    };

    /// <summary>
    /// Fuera de Development, un proveedor federado configurado (con ClientId)
    /// debe estar restringido a la institución. Sin ClientId no se exige nada.
    /// </summary>
    public static void ValidateExternalProviders(bool isDevelopment, AuthOptions authOptions)
    {
        if (isDevelopment)
        {
            return;
        }

        if (authOptions.Entra.IsConfigured)
        {
            var tenant = authOptions.Entra.TenantId.Trim();
            if (MultiTenantEntraIds.Any(t => string.Equals(t, tenant, StringComparison.OrdinalIgnoreCase)))
            {
                throw new InvalidOperationException(
                    $"Auth:Entra:TenantId no puede ser '{tenant}' fuera de Development: " +
                    "aceptaría cuentas de otras organizaciones. Usa el Id del tenant institucional.");
            }

            if (!authOptions.AllowedEmailDomains.Any(d => !string.IsNullOrWhiteSpace(d)))
            {
                throw new InvalidOperationException(
                    "Auth:AllowedEmailDomains está vacío y Microsoft/Entra está configurado: " +
                    "define los dominios de correo institucionales (Auth__AllowedEmailDomains__0=...).");
            }
        }

        if (authOptions.Google.IsConfigured && string.IsNullOrWhiteSpace(authOptions.Google.HostedDomain))
        {
            throw new InvalidOperationException(
                "Auth:Google:HostedDomain está vacío y Google está configurado: " +
                "aceptaría cualquier cuenta de Google. Define el dominio de Google Workspace.");
        }
    }

    public static void ValidateCookieSecure(bool isDevelopment, bool cookieSecure)
    {
        if (!isDevelopment && !cookieSecure)
        {
            throw new InvalidOperationException(
                "Auth:CookieSecure debe ser true fuera de Development: las cookies de sesión " +
                "no pueden viajar por HTTP. Configúralo con Auth__CookieSecure=true (variable de " +
                "entorno) o en appsettings.Production.json, y sirve la API detrás de HTTPS.");
        }
    }
}
