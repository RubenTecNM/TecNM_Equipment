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

    public static void Validate(string? jwtSecret, bool isDevelopment, bool cookieSecure)
    {
        ValidateJwtSecret(jwtSecret);
        ValidateCookieSecure(isDevelopment, cookieSecure);
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
