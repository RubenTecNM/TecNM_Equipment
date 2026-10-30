namespace TecnmInventario.Api.Core.Entities;

/// <summary>
/// Identidad de Microsoft Entra o Google vinculada a un app_user
/// (tabla user_external_logins, scripts/add-auth-sessions.sql).
/// Mapeo manual en TecnmInventarioDbContext.Custom.cs: no es parte del scaffold.
/// </summary>
public partial class UserExternalLogin
{
    public long UserExternalLoginId { get; set; }

    public long UserId { get; set; }

    public string Provider { get; set; } = null!;

    public string Subject { get; set; } = null!;

    public string Email { get; set; } = null!;

    public DateTime LinkedAt { get; set; }

    public virtual AppUser User { get; set; } = null!;
}
