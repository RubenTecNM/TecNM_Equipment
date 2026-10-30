using System.Net;

namespace TecnmInventario.Api.Core.Entities;

/// <summary>
/// Refresh token de sesión (tabla refresh_tokens, scripts/add-auth-sessions.sql).
/// Mapeo manual en TecnmInventarioDbContext.Custom.cs: no es parte del scaffold.
/// </summary>
public partial class RefreshToken
{
    public long RefreshTokenId { get; set; }

    public long UserId { get; set; }

    public string TokenHash { get; set; } = null!;

    public Guid FamilyId { get; set; }

    public DateTime ExpiresAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? RevokedAt { get; set; }

    public long? ReplacedById { get; set; }

    public IPAddress? CreatedIp { get; set; }

    public string? UserAgent { get; set; }

    public virtual AppUser User { get; set; } = null!;
}
