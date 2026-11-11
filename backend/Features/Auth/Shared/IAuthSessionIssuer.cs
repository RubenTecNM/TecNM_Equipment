using TecnmInventario.Api.Core.Entities;

namespace TecnmInventario.Api.Features.Auth.Shared;

/// <summary>
/// Abre una sesión (JWT de acceso + refresh token persistido). Compartido por
/// Login, Refresh y el callback federado.
/// S1: IssueAsync NO escribe cookies; el llamador llama a WriteCookies solo
/// DESPUÉS de SaveChanges/Commit, para que un fallo de BD nunca deje al cliente
/// con la cookie de un refresh token que no existe.
/// </summary>
public interface IAuthSessionIssuer
{
    /// <param name="user">Usuario con <c>Role</c> cargado.</param>
    /// <param name="familyId">Familia del refresh rotado; null = sesión nueva.</param>
    Task<IssuedSession> IssueAsync(AppUser user, Guid? familyId, CancellationToken cancellationToken);

    Task<AuthUserResponse> BuildProfileAsync(AppUser user, CancellationToken cancellationToken);

    /// <summary>Escribe las cookies de la sesión ya confirmada en la BD.</summary>
    void WriteCookies(IssuedSession session);
}

/// <summary>Tokens en claro de una sesión emitida; solo viajan en cookies.</summary>
public sealed record SessionTokens(
    string AccessToken, DateTime AccessExpiresAt, string RefreshToken, DateTime RefreshExpiresAt);

public sealed record IssuedSession(RefreshToken RefreshToken, AuthUserResponse Profile, SessionTokens Tokens);
