namespace TecnmInventario.Api.Common.Security;

/// <summary>
/// Lee y escribe las cookies de sesión en la respuesta actual, para que los
/// Handlers de Auth no dependan de HttpContext y los controllers sigan delgados.
/// </summary>
public interface IAuthCookieWriter
{
    void Write(string accessToken, DateTime accessExpiresAt, string refreshToken, DateTime refreshExpiresAt);

    void Clear();

    string? ReadRefreshToken();
}
