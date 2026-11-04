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

    // Entidades con is_deleted pendientes de recibir el filtro "SoftDelete"
    // (fases 4b/c/d). Cuando la lista quede vacía, Guardian_ListaDePendientesVacia pasa.
    // ⚠ No eliminar esta lista hasta que todos los filtros estén aplicados.
    public static readonly IReadOnlySet<string> SoftDeletePendingPhase =
        new HashSet<string>(StringComparer.Ordinal)
        {
            // fase 4b — activo y subentidades
            "Asset",
            "AssetComponent",
            "AssetOsHistory",
            "AssetService",
            "AssetSoftware",
            "AssetSpecification",
            "MaintenanceRecord",
            "NetworkInterface",
            "Manufacturer",
            "Model",
            "Software",
            "SoftwareLicense",
            "BuildingSetting",
            // fase 4c — catálogos
            "AssetStatus",
            "AssetType",
            "AssignmentType",
            "ComponentType",
            "DisposalReason",
            "IncidentPriority",
            "IncidentStatus",
            "IncidentType",
            "LicenseType",
            "LocationType",
            "MaintenanceType",
            "MovementReason",
            "RelationType",
            "Service",
            "SoftwareType",
            // fase 4d — seguridad
            "AppUser",
            "Role",
            "Permission",
        };

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder)
    {
        // Los filtros de soft-delete viven aquí y sobreviven a un re-scaffold
        // del archivo generado (TecnmInventarioDbContext.cs).
        ConfigureSoftDeleteFilters(modelBuilder);

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

    private static void ConfigureSoftDeleteFilters(ModelBuilder modelBuilder)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            var isDeletedProp = entityType.FindProperty("IsDeleted");
            if (isDeletedProp is null)
                continue;

            var filters = entityType.GetDeclaredQueryFilters().ToList();

            // Un filtro sin nombre (IsAnonymous=true) no puede ignorarse con
            // IgnoreQueryFilters(["SoftDelete"]) y reintroduciría el bug sin que
            // el guardián lo detecte. Fallar en la construcción del modelo fuerza
            // a que el desarrollador lo migre a HasQueryFilter("SoftDelete", ...).
            var anonymous = filters.Where(f => f.IsAnonymous).ToList();
            if (anonymous.Count > 0)
            {
                throw new InvalidOperationException(
                    $"La entidad '{entityType.ClrType.Name}' tiene {anonymous.Count} filtro(s) " +
                    $"sin nombre (IsAnonymous=true). Todos los filtros de soft-delete deben " +
                    $"declararse con HasQueryFilter(\"SoftDelete\", ...) para ser ignorables " +
                    $"por nombre. Muévelos a TecnmInventarioDbContext.Custom.cs.");
            }

            // Si ya tiene el filtro nombrado "SoftDelete", no hace falta nada más.
            if (filters.Any(f => !f.IsAnonymous))
                continue;

            if (SoftDeletePendingPhase.Contains(entityType.ClrType.Name))
                continue;

            var clrType = entityType.ClrType;
            var param = System.Linq.Expressions.Expression.Parameter(clrType, "e");
            var body = System.Linq.Expressions.Expression.Not(
                System.Linq.Expressions.Expression.Property(param, "IsDeleted"));
            var lambda = System.Linq.Expressions.Expression.Lambda(body, param);

            modelBuilder.Entity(clrType).HasQueryFilter("SoftDelete", lambda);
        }
    }
}
