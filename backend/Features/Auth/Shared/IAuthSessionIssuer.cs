using TecnmInventario.Api.Core.Entities;

namespace TecnmInventario.Api.Features.Auth.Shared;

/// <summary>
/// Abre una sesión (JWT de acceso + refresh token persistido) y escribe las
/// cookies. Compartido por Login, Refresh y el callback federado.
/// </summary>
public interface IAuthSessionIssuer
{
    /// <param name="user">Usuario con <c>Role</c> cargado.</param>
    /// <param name="familyId">Familia del refresh rotado; null = sesión nueva.</param>
    Task<IssuedSession> IssueAsync(AppUser user, Guid? familyId, CancellationToken cancellationToken);

    Task<AuthUserResponse> BuildProfileAsync(AppUser user, CancellationToken cancellationToken);
}

public sealed record IssuedSession(RefreshToken RefreshToken, AuthUserResponse Profile);
