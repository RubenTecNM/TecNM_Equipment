using System.Security.Cryptography;
using System.Text;
using TecnmInventario.Api.Common.Security;

namespace TecnmInventario.Api.Common.Middleware;

/// <summary>
/// Protección CSRF double-submit para la sesión por cookies: en métodos que
/// escriben, el header X-CSRF-Token debe coincidir con la cookie tecnm_csrf.
/// Solo aplica cuando la petición se autentica con cookies; las que traen
/// Authorization: Bearer (curl, tests) no son vulnerables a CSRF.
/// </summary>
public class CsrfMiddleware
{
    // Login abre la sesión (aún no hay cookie CSRF); las rutas externas son
    // navegaciones GET/redirecciones del proveedor.
    private static readonly string[] ExemptPrefixes =
    [
        "/api/auth/login",
        "/api/auth/external",
        "/api/auth/signin-"
    ];

    private readonly RequestDelegate _next;
    private readonly ILogger<CsrfMiddleware> _logger;

    public CsrfMiddleware(RequestDelegate next, ILogger<CsrfMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (RequiresCheck(context) && !TokensMatch(context))
        {
            _logger.LogWarning("Token CSRF ausente o inválido en {Path}", context.Request.Path);
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsJsonAsync(new
            {
                title = "Acceso prohibido",
                status = StatusCodes.Status403Forbidden,
                detail = "Token CSRF ausente o inválido. Recarga la página e intenta de nuevo."
            });
            return;
        }

        await _next(context);
    }

    private static bool RequiresCheck(HttpContext context)
    {
        var request = context.Request;

        if (HttpMethods.IsGet(request.Method) || HttpMethods.IsHead(request.Method) ||
            HttpMethods.IsOptions(request.Method))
            return false;

        if (request.Headers.Authorization.Count > 0)
            return false;

        var path = request.Path.Value ?? string.Empty;
        if (ExemptPrefixes.Any(p => path.StartsWith(p, StringComparison.OrdinalIgnoreCase)))
            return false;

        return request.Cookies.ContainsKey(AuthCookies.AccessToken) ||
               request.Cookies.ContainsKey(AuthCookies.RefreshToken);
    }

    private static bool TokensMatch(HttpContext context)
    {
        var cookie = context.Request.Cookies[AuthCookies.Csrf];
        var header = context.Request.Headers[AuthCookies.CsrfHeader].ToString();

        if (string.IsNullOrEmpty(cookie) || string.IsNullOrEmpty(header))
            return false;

        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(cookie), Encoding.UTF8.GetBytes(header));
    }
}
