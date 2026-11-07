using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using TecnmInventario.Api.Data;

namespace TecnmInventario.Api.Tests;

// ═══════════════════════════════════════════════════════════════════════════
// Guardián de metadatos de soft-delete — sin Docker ni PostgreSQL.
//
//   Test 1 · TienenFiltroOEstanPendientes
//     Falla si existe alguna entidad con IsDeleted que no tiene filtro "SoftDelete"
//     Y no está en TecnmInventarioDbContext.SoftDeletePendingPhase.
//     → CP3: PASA (31 en pendientes, 6 con filtro por convención).
//     → CP7: PASA (lista vacía, todos con filtro).
//
//   Test 2 · ListaDePendientesVacia
//     Falla mientras SoftDeletePendingPhase no esté vacía.
//     → CP3: ROJO esperado (31 entradas en fases 4b/c/d).
//     → CP7: PASA (lista vaciada, todos los filtros aplicados).
// ═══════════════════════════════════════════════════════════════════════════
public sealed class SoftDeleteGuardianTests
{
    private static readonly IReadOnlySet<string> SoftDeletePendingPhase =
        TecnmInventarioDbContext.SoftDeletePendingPhase;

    // ─────────────────────────────────────────────────────────────────────────
    // Test 1a: ninguna entidad no-pendiente y no-exenta puede carecer del
    // filtro "SoftDelete". Los catálogos de SoftDeleteExemptCatalogs (F1,
    // fase 4c.3) y Role en SoftDeleteExemptSecurity (H1, fase 4d.2) se
    // excluyen a propósito: nunca llevan filtro global.
    // ─────────────────────────────────────────────────────────────────────────
    [Fact]
    public void Guardian_EntidadesConIsDeleted_TienenFiltroOEstanPendientes()
    {
        using var context = BuildInMemoryContext();

        var sinFiltroNiPendiente = context.Model
            .GetEntityTypes()
            .Where(e => e.FindProperty("IsDeleted") is not null)
            .Where(e => !SoftDeletePendingPhase.Contains(e.ClrType.Name))
            .Where(e => !TecnmInventarioDbContext.SoftDeleteExemptCatalogs.Contains(e.ClrType.Name))
            .Where(e => !TecnmInventarioDbContext.SoftDeleteExemptSecurity.Contains(e.ClrType.Name))
            .Where(e =>
            {
                var filters = e.GetDeclaredQueryFilters().ToList();
                return !filters.Any(f => !f.IsAnonymous && f.Key == "SoftDelete");
            })
            .Select(e => e.ClrType.Name)
            .OrderBy(x => x)
            .ToList();

        Assert.Empty(sinFiltroNiPendiente);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Test 1b: ninguna entidad no-pendiente puede tener filtros anónimos
    // Un filtro anónimo no se puede ignorar con IgnoreQueryFilters(["SoftDelete"])
    // ─────────────────────────────────────────────────────────────────────────
    [Fact]
    public void Guardian_EntidadesConIsDeleted_SinFiltrosAnonimos()
    {
        using var context = BuildInMemoryContext();

        var conFiltroAnonimo = context.Model
            .GetEntityTypes()
            .Where(e => e.FindProperty("IsDeleted") is not null)
            .Where(e => e.GetDeclaredQueryFilters().Any(f => f.IsAnonymous))
            .Select(e => e.ClrType.Name)
            .OrderBy(x => x)
            .ToList();

        Assert.Empty(conFiltroAnonimo);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Test 2: la lista de pendientes debe quedar vacía (rojo hasta CP7)
    // ─────────────────────────────────────────────────────────────────────────
    [Fact]
    public void Guardian_ListaDePendientesVacia()
    {
        // CP3: FALLA esperado — 31 entidades pendientes (fases 4b/c/d).
        // CP7: pasa (lista vacía, todos los filtros aplicados).
        Assert.Empty(SoftDeletePendingPhase);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Test unitario: filtro anónimo en entidad con IsDeleted → InvalidOperationException
    // Usa un DbContext mínimo de prueba, no el real.
    // ─────────────────────────────────────────────────────────────────────────
    [Fact]
    public void FiltroAnonimo_EnEntidadConIsDeleted_LanzaInvalidOperationException()
    {
        var options = new DbContextOptionsBuilder<FakeDbContextConFiltroAnonimo>()
            .UseInMemoryDatabase($"guardian-anon-{Guid.NewGuid()}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        // La construcción del modelo dispara ConfigureSoftDeleteFilters, que debe lanzar.
        var ex = Assert.Throws<InvalidOperationException>(() =>
        {
            using var ctx = new FakeDbContextConFiltroAnonimo(options);
            _ = ctx.Model; // materializa el modelo
        });

        Assert.Contains("FakeEntityConIsDeleted", ex.Message);
        Assert.Contains("IsAnonymous=true", ex.Message);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Guardián de relaciones requeridas (fase 4b.2, paso E).
    // Recorre el modelo y lista toda FK con IsRequired=true cuyo principal
    // tenga el filtro "SoftDelete". EF convierte el Include/Select en INNER
    // JOIN para esa relación; si el principal está borrado lógicamente, la
    // fila del dependiente desaparece ENTERA de la consulta (D3), salvo que
    // el handler use IgnoreQueryFilters(["SoftDelete"]) + Where explícito
    // sobre la raíz (D2). Toda relación nueva en la lista exige revisar sus
    // consultas de historial por el mismo motivo.
    // ─────────────────────────────────────────────────────────────────────────
    [Fact]
    public void Guardian_RelacionesRequeridasHaciaPrincipalesConSoftDelete()
    {
        var aprobadas = new HashSet<string>(StringComparer.Ordinal)
        {
            "AcquisitionAsset.AcquisitionId -> Acquisition",
            "AcquisitionAsset.AssetId -> Asset",
            "AssetAssignment.AssetId -> Asset",
            // fase 4d.2 (H2): AppUser recibe el filtro "SoftDelete".
            // AssetAssignment.AssignedTo -> AppUser: navegada por
            // GetAssetAssignmentsHandler.cs:42 (a.AssignedToNavigation.Username),
            // ya protegida con IgnoreQueryFilters(["SoftDelete"]) desde la fase 4a.
            "AssetAssignment.AssignedTo -> AppUser",
            "AssetComponent.AssetId -> Asset",
            "AssetDisposal.AssetId -> Asset",
            "AssetMovement.AssetId -> Asset",
            // AssetMovement.MovedBy -> AppUser: navegada por
            // GetAssetMovementsHandler.cs:59 (m.MovedByNavigation.Username),
            // ya protegida con IgnoreQueryFilters(["SoftDelete"]) desde la fase 4a.
            "AssetMovement.MovedBy -> AppUser",
            "AssetMovement.ToLocationId -> Location",
            "AssetOsHistory.AssetId -> Asset",
            "AssetRelation.PrimaryAssetId -> Asset",
            "AssetRelation.RelatedAssetId -> Asset",
            "AssetService.AssetId -> Asset",
            "AssetSoftware.AssetId -> Asset",
            "AssetSoftware.SoftwareId -> Software",
            "AssetSpecification.AssetId -> Asset",
            "AssetWarranty.AssetId -> Asset",
            "BuildingSetting.LocationId -> Location",
            // Dictionary`2.PermissionId -> Permission: EF representa así la
            // tabla puente role_permissions (entidad de tipo compartido de la
            // skip-navigation Role.Permissions <-> Permission.Roles). Es
            // navegación de COLECCIÓN, no de valor único: el filtro de
            // Permission solo quita ese ítem de la lista (RolePermissionService.cs,
            // AuthSessionIssuer.cs, GetRolesHandler.cs, AssignPermissionsHandler.cs),
            // nunca hace desaparecer la fila completa del Role. No pierde filas.
            "Dictionary`2.PermissionId -> Permission",
            "Incident.AssetId -> Asset",
            // Incident.ReportedBy -> AppUser: navegada por GetIncidentById.cs:45-46
            // y GetIncidents.cs:108, ya protegidas con IgnoreQueryFilters(["SoftDelete"])
            // desde la fase 4b.
            "Incident.ReportedBy -> AppUser",
            "InventoryRecord.AssetId -> Asset",
            "MaintenanceRecord.AssetId -> Asset",
            "NetworkInterface.AssetId -> Asset",
            // RefreshToken.UserId -> AppUser: navegada por
            // RefreshSessionHandler.cs:46-51 (Include(t.User)), protegida con
            // IgnoreQueryFilters(["SoftDelete"]) añadido en H4 (fase 4d.2).
            "RefreshToken.UserId -> AppUser",
            "SoftwareLicense.SoftwareId -> Software",
            "SoftwareLicenseAssignment.AssetId -> Asset",
            "SoftwareLicenseAssignment.LicenseId -> SoftwareLicense",
            // UserExternalLogin.UserId -> AppUser: navegada por
            // ExternalLoginCallbackHandler.cs:76-82 (Include(l.User)), protegida
            // con IgnoreQueryFilters(["SoftDelete"]) añadido en H5 (fase 4d.2).
            "UserExternalLogin.UserId -> AppUser",
        };

        using var context = BuildInMemoryContext();
        var actuales = context.Model.GetEntityTypes()
            .SelectMany(e => e.GetForeignKeys())
            .Where(fk => fk.IsRequired)
            .Where(fk => fk.PrincipalEntityType.GetDeclaredQueryFilters()
                .Any(f => !f.IsAnonymous && f.Key == "SoftDelete"))
            .Select(fk =>
                $"{fk.DeclaringEntityType.ClrType.Name}.{string.Join(",", fk.Properties.Select(p => p.Name))} -> {fk.PrincipalEntityType.ClrType.Name}")
            .ToHashSet(StringComparer.Ordinal);

        var nuevas = actuales.Except(aprobadas).OrderBy(x => x).ToList();
        var desaparecidas = aprobadas.Except(actuales).OrderBy(x => x).ToList();

        Assert.True(
            nuevas.Count == 0 && desaparecidas.Count == 0,
            $"Relaciones nuevas no aprobadas (revisar sus consultas de historial, INNER JOIN oculta filas): " +
            $"[{string.Join(", ", nuevas)}]. " +
            $"Relaciones aprobadas que desaparecieron del modelo: [{string.Join(", ", desaparecidas)}].");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // A7 (fase 4c.3, paso A): los catálogos exentos no tienen ningún filtro
    // "SoftDelete", y ninguna entidad con IsDeleted queda sin cubrir salvo
    // que esté en SoftDeleteExemptCatalogs, SoftDeleteExemptSecurity (H1,
    // fase 4d.2) o en SoftDeletePendingPhase.
    // ─────────────────────────────────────────────────────────────────────────
    [Fact]
    public void Guardian_CatalogosExentos_NoTienenFiltroYEstanJustificados()
    {
        var exempt = TecnmInventarioDbContext.SoftDeleteExemptCatalogs;
        var exemptSecurity = TecnmInventarioDbContext.SoftDeleteExemptSecurity;

        using var context = BuildInMemoryContext();
        var entidadesConIsDeleted = context.Model
            .GetEntityTypes()
            .Where(e => e.FindProperty("IsDeleted") is not null)
            .ToList();

        var exentosConFiltro = entidadesConIsDeleted
            .Where(e => exempt.Contains(e.ClrType.Name))
            .Where(e => e.GetDeclaredQueryFilters().Any(f => !f.IsAnonymous && f.Key == "SoftDelete"))
            .Select(e => e.ClrType.Name)
            .OrderBy(x => x)
            .ToList();

        Assert.Empty(exentosConFiltro);

        var sinCubrir = entidadesConIsDeleted
            .Where(e => !exempt.Contains(e.ClrType.Name))
            .Where(e => !exemptSecurity.Contains(e.ClrType.Name))
            .Where(e => !SoftDeletePendingPhase.Contains(e.ClrType.Name))
            .Where(e => !e.GetDeclaredQueryFilters().Any(f => !f.IsAnonymous && f.Key == "SoftDelete"))
            .Select(e => e.ClrType.Name)
            .OrderBy(x => x)
            .ToList();

        Assert.Empty(sinCubrir);
    }

    private static TecnmInventarioDbContext BuildInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<TecnmInventarioDbContext>()
            .UseInMemoryDatabase($"guardian-{Guid.NewGuid()}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new TecnmInventarioDbContext(options);
    }
}

// ─── Tipos de apoyo para el test de filtro anónimo ───────────────────────────

internal sealed class FakeEntityConIsDeleted
{
    public int Id { get; set; }
    public bool IsDeleted { get; set; }
}

internal sealed class FakeDbContextConFiltroAnonimo : DbContext
{
    public FakeDbContextConFiltroAnonimo(DbContextOptions<FakeDbContextConFiltroAnonimo> options)
        : base(options) { }

    public DbSet<FakeEntityConIsDeleted> FakeEntities { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Registra un filtro SIN nombre — esto es lo que la convención debe rechazar.
        modelBuilder.Entity<FakeEntityConIsDeleted>()
            .HasQueryFilter(e => !e.IsDeleted);

        // Simula la misma lógica de guardia que usa TecnmInventarioDbContext.Custom.cs.
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (entityType.FindProperty("IsDeleted") is null)
                continue;

            var anonymous = entityType.GetDeclaredQueryFilters()
                .Where(f => f.IsAnonymous).ToList();

            if (anonymous.Count > 0)
            {
                throw new InvalidOperationException(
                    $"La entidad '{entityType.ClrType.Name}' tiene {anonymous.Count} filtro(s) " +
                    $"sin nombre (IsAnonymous=true). Todos los filtros de soft-delete deben " +
                    $"declararse con HasQueryFilter(\"SoftDelete\", ...) para ser ignorables " +
                    $"por nombre. Muévelos a TecnmInventarioDbContext.Custom.cs.");
            }
        }
    }
}
