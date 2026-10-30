using Microsoft.EntityFrameworkCore;
using TecnmInventario.Api.Core.Entities;

namespace TecnmInventario.Api.Data;

public partial class TecnmInventarioDbContext
{
    // Tablas de sesión (scripts/add-auth-sessions.sql). Se mapean aquí, no en el
    // archivo generado, para no editar el scaffold. Si se vuelve a hacer scaffold
    // con estas tablas, excluirlas (-t) o borrar este mapeo para no duplicarlo.
    public virtual DbSet<RefreshToken> RefreshTokens { get; set; }

    public virtual DbSet<UserExternalLogin> UserExternalLogins { get; set; }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<RefreshToken>(entity =>
        {
            entity.HasKey(e => e.RefreshTokenId);
            entity.ToTable("refresh_tokens", "inventario");

            entity.HasIndex(e => e.TokenHash).IsUnique();

            entity.Property(e => e.RefreshTokenId).UseIdentityAlwaysColumn().HasColumnName("refresh_token_id");
            entity.Property(e => e.UserId).HasColumnName("user_id");
            entity.Property(e => e.TokenHash).HasMaxLength(64).IsFixedLength().HasColumnName("token_hash");
            entity.Property(e => e.FamilyId).HasColumnName("family_id");
            entity.Property(e => e.ExpiresAt).HasColumnName("expires_at");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()").HasColumnName("created_at");
            entity.Property(e => e.RevokedAt).HasColumnName("revoked_at");
            entity.Property(e => e.ReplacedById).HasColumnName("replaced_by_id");
            entity.Property(e => e.CreatedIp).HasColumnName("created_ip");
            entity.Property(e => e.UserAgent).HasMaxLength(512).HasColumnName("user_agent");

            entity.HasOne(e => e.User).WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull);
        });

        modelBuilder.Entity<UserExternalLogin>(entity =>
        {
            entity.HasKey(e => e.UserExternalLoginId);
            entity.ToTable("user_external_logins", "inventario");

            entity.HasIndex(e => new { e.Provider, e.Subject }).IsUnique();
            entity.HasIndex(e => new { e.UserId, e.Provider }).IsUnique();

            entity.Property(e => e.UserExternalLoginId).UseIdentityAlwaysColumn().HasColumnName("user_external_login_id");
            entity.Property(e => e.UserId).HasColumnName("user_id");
            entity.Property(e => e.Provider).HasMaxLength(20).HasColumnName("provider");
            entity.Property(e => e.Subject).HasMaxLength(255).HasColumnName("subject");
            entity.Property(e => e.Email).HasMaxLength(254).HasColumnName("email");
            entity.Property(e => e.LinkedAt).HasDefaultValueSql("now()").HasColumnName("linked_at");

            entity.HasOne(e => e.User).WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull);
        });

        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            var versionProp = entityType.FindProperty("Version");
            if (versionProp is not null && versionProp.ClrType == typeof(int))
                versionProp.IsConcurrencyToken = true;
        }
    }
}
