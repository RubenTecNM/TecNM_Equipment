using System.Security.Cryptography;
using Microsoft.Extensions.Options;

namespace TecnmInventario.Api.Common.Security;

public sealed class AuthCookieWriter : IAuthCookieWriter
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly AuthOptions _options;

    public AuthCookieWriter(IHttpContextAccessor httpContextAccessor, IOptions<AuthOptions> options)
    {
        _httpContextAccessor = httpContextAccessor;
        _options = options.Value;
    }

    private HttpContext Context =>
        _httpContextAccessor.HttpContext
        ?? throw new InvalidOperationException("No hay una petición HTTP activa.");

    public void Write(string accessToken, DateTime accessExpiresAt, string refreshToken, DateTime refreshExpiresAt)
    {
        var cookies = Context.Response.Cookies;

        // Lax (no Strict) para que la cookie viaje en la navegación de vuelta
        // del login federado, que llega desde el dominio del proveedor.
        cookies.Append(AuthCookies.AccessToken, accessToken,
            Options(httpOnly: true, SameSiteMode.Lax, "/api", accessExpiresAt));
        cookies.Append(AuthCookies.RefreshToken, refreshToken,
            Options(httpOnly: true, SameSiteMode.Strict, "/api/auth", refreshExpiresAt));
        cookies.Append(AuthCookies.Csrf, Convert.ToHexString(RandomNumberGenerator.GetBytes(32)),
            Options(httpOnly: false, SameSiteMode.Strict, "/", refreshExpiresAt));
    }

    public void Clear()
    {
        var cookies = Context.Response.Cookies;
        cookies.Delete(AuthCookies.AccessToken, Options(httpOnly: true, SameSiteMode.Lax, "/api", null));
        cookies.Delete(AuthCookies.RefreshToken, Options(httpOnly: true, SameSiteMode.Strict, "/api/auth", null));
        cookies.Delete(AuthCookies.Csrf, Options(httpOnly: false, SameSiteMode.Strict, "/", null));
    }

    public string? ReadRefreshToken() =>
        Context.Request.Cookies.TryGetValue(AuthCookies.RefreshToken, out var value) ? value : null;

    private CookieOptions Options(bool httpOnly, SameSiteMode sameSite, string path, DateTime? expiresAt) => new()
    {
        HttpOnly = httpOnly,
        Secure = _options.CookieSecure,
        SameSite = sameSite,
        Path = path,
        Expires = expiresAt is null ? null : new DateTimeOffset(expiresAt.Value, TimeSpan.Zero),
        IsEssential = true
    };
}
