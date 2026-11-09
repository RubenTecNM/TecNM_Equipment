namespace TecnmInventario.Api.Common.Security;

/// <summary>
/// Cabeceras de seguridad en todas las respuestas (también errores y 429):
/// la API no se incrusta en marcos, no deja que el navegador adivine tipos y
/// no filtra la URL de origen. Las respuestas de /api/auth/* no se guardan en
/// ninguna caché (llevan perfil de usuario o cookies de sesión).
/// Se escriben antes de llamar al siguiente middleware, así que nada que
/// genere la respuesta después puede olvidarlas.
/// </summary>
public sealed class SecurityHeadersMiddleware
{
    private readonly RequestDelegate _next;

    public SecurityHeadersMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public Task InvokeAsync(HttpContext context)
    {
        var headers = context.Response.Headers;
        headers.XContentTypeOptions = "nosniff";
        headers.XFrameOptions = "DENY";
        headers["Referrer-Policy"] = "no-referrer";

        if (context.Request.Path.StartsWithSegments("/api/auth", StringComparison.OrdinalIgnoreCase))
        {
            headers.CacheControl = "no-store";
        }

        return _next(context);
    }
}
