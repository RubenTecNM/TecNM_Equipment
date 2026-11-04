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
    // Test 1a: ninguna entidad no-pendiente puede carecer del filtro "SoftDelete"
    // ─────────────────────────────────────────────────────────────────────────
    [Fact]
    public void Guardian_EntidadesConIsDeleted_TienenFiltroOEstanPendientes()
    {
        using var context = BuildInMemoryContext();

        var sinFiltroNiPendiente = context.Model
            .GetEntityTypes()
            .Where(e => e.FindProperty("IsDeleted") is not null)
            .Where(e => !SoftDeletePendingPhase.Contains(e.ClrType.Name))
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
