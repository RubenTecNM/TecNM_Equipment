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

    // Entidades con is_deleted pendientes de recibir el filtro "SoftDelete".
    // Vacía desde la fase 4d: AppUser y Permission ya tienen el filtro (H2);
    // Role está exento a propósito (ver SoftDeleteExemptSecurity).
    // ⚠ No eliminar esta lista hasta que todos los filtros estén aplicados.
    public static readonly IReadOnlySet<string> SoftDeletePendingPhase =
        new HashSet<string>(StringComparer.Ordinal);

    // H1 (fase 4d.2): Role queda exento del filtro global, igual que los
    // catálogos de SoftDeleteExemptCatalogs. AppUser.RoleId -> Role es una FK
    // requerida y no existe endpoint que borre roles; con el filtro activo,
    // un rol borrado por SQL directo haría desaparecer ENTERA la fila de
    // cualquier usuario con ese rol en toda consulta que navegue u.Role
    // (login -> "credenciales inválidas" aunque sean correctas; GetUsers,
    // refresh y GetMe con el mismo problema). La autorización sigue cerrada
    // sin el filtro: RolePermissionService ya excluye roles borrados con un
    // Where(!r.IsDeleted) explícito (no depende de este filtro global).
    // Consecuencia aceptada: un usuario activo con rol borrado se autentica
    // pero recibe 403 en todo endpoint con permiso.
    public static readonly IReadOnlySet<string> SoftDeleteExemptSecurity =
        new HashSet<string>(StringComparer.Ordinal)
        {
            "Role",
        };

    // F1 (fase 4c.3, decisión opción B): catálogos que NUNCA llevan filtro
    // global "SoftDelete", a diferencia de SoftDeletePendingPhase (que es
    // temporal). Las 15 son el extremo requerido de una FK de Asset,
    // Incident, Location u otra entidad operativa: con filtro global, un
    // catálogo borrado lógicamente convertiría el Include/Select en INNER
    // JOIN y haría desaparecer filas activas de listados y escrituras
    // (ver Guardian_RelacionesRequeridasHaciaPrincipalesConSoftDelete). No
    // existen endpoints que los borren hoy. F2: un catálogo borrado
    // lógicamente deja de ofrecerse para registros nuevos (los validadores
    // ya comprueban "!IsDeleted") pero los registros existentes lo siguen
    // mostrando con su nombre, sin excepción.
    public static readonly IReadOnlySet<string> SoftDeleteExemptCatalogs =
        new HashSet<string>(StringComparer.Ordinal)
        {
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

            if (SoftDeleteExemptCatalogs.Contains(entityType.ClrType.Name))
                continue;

            if (SoftDeleteExemptSecurity.Contains(entityType.ClrType.Name))
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
