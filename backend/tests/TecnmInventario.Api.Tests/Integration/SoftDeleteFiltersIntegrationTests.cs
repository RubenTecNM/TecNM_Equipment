using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TecnmInventario.Api.Common.Audit;
using TecnmInventario.Api.Common.Behaviors;
using TecnmInventario.Api.Common.Deletion;
using TecnmInventario.Api.Common.Errors;
using TecnmInventario.Api.Common.Security;
using TecnmInventario.Api.Data;
using TecnmInventario.Api.Features.Assets.Assignments.GetAssetAssignments;
using TecnmInventario.Api.Features.Assets.Movements.GetAssetMovements;
using TecnmInventario.Api.Features.Locations.DeleteLocation;
using TecnmInventario.Api.Features.Procurement.Acquisitions.DeleteAcquisition;
using TecnmInventario.Api.Features.Procurement.Suppliers.DeleteSupplier;
// ReferenceInUseException → HTTP 409 (ExceptionHandlingMiddleware)

namespace TecnmInventario.Api.Tests.Integration;

// ═══════════════════════════════════════════════════════════════════════════
// Checkpoint 2 — tests que FALLAN sin el fix.
//   (a) Bug activo GetAssetMovements: destino borrado → nombre perdido.
//   (a2) Bug latente GetAssetAssignments: ubicación borrada → nombre perdido.
//   (b) Muestra representativa por fase: consultas que devuelven is_deleted=true.
//   (c) Handlers de borrado sin protección 409 cuando hay referencias activas.
//   (d) Guardián → SoftDeleteGuardianTests.cs (sin Docker).
//   (e) GetAssetMovements con activo borrado → hoy KeyNotFoundException (404).
//
// Excepción usada en tests (c): ReferenceInUseException → HTTP 409.
// Mapeo en ExceptionHandlingMiddleware: catch(ReferenceInUseException) → HttpStatusCode.Conflict.
// Los tests (c) seguirán en ROJO hasta la fase 4c, cuando los handlers de borrado
// añadan las comprobaciones que lanzan ReferenceInUseException.
// ═══════════════════════════════════════════════════════════════════════════

[Trait("Category", "Integration")]
public sealed class SoftDeleteFiltersIntegrationTests : PostgresTestBase
{
    public SoftDeleteFiltersIntegrationTests(PostgresContainerFixture fixture)
        : base(fixture) { }

    // ── Seed SQL común: dos ubicaciones y un técnico ──────────────────────────
    private const string BaselineSeedSql = """
        INSERT INTO inventario.locations (location_id, code, name, location_type_id)
        OVERRIDING SYSTEM VALUE
        SELECT 1, 'SD-LOC-1', 'Ubicación SD 1', lt.location_type_id
        FROM inventario.location_types lt WHERE lt.code = 'LABORATORIO'
        ON CONFLICT (location_id) DO NOTHING;

        INSERT INTO inventario.locations (location_id, code, name, location_type_id)
        OVERRIDING SYSTEM VALUE
        SELECT 2, 'SD-LOC-2', 'Ubicación SD 2', lt.location_type_id
        FROM inventario.location_types lt WHERE lt.code = 'LABORATORIO'
        ON CONFLICT (location_id) DO NOTHING;

        SELECT setval(
            pg_get_serial_sequence('inventario.locations', 'location_id'),
            (SELECT MAX(location_id) FROM inventario.locations));

        INSERT INTO inventario.app_users
            (user_id, username, email, password_hash, first_name, last_name, role_id, status)
        OVERRIDING SYSTEM VALUE
        SELECT 2, 'tec_sd', 'tec-sd@test.local', 'NO_LOGIN',
               'Técnico', 'SD', r.role_id, 'activo'
        FROM inventario.roles r WHERE r.code = 'TECNICO'
        ON CONFLICT (user_id) DO NOTHING;

        SELECT setval(
            pg_get_serial_sequence('inventario.app_users', 'user_id'),
            (SELECT MAX(user_id) FROM inventario.app_users));
        """;

    // ─────────────────────────────────────────────────────────────────────────
    // (a) Bug activo: movimiento con destino borrado lógicamente pierde el nombre
    // ─────────────────────────────────────────────────────────────────────────
    // GetAssetMovementsHandler proyecta m.ToLocation.Name en Select (FK NOT NULL).
    // Location ya tiene HasQueryFilter(!IsDeleted). EF traduce la proyección con
    // INNER JOIN entre asset_movements y locations con la condición del filtro.
    // Cuando el destino está borrado, el INNER JOIN no produce match y la fila
    // completa del movimiento desaparece de Items (bug activo, D3 violado).
    // TotalCount viene de un CountAsync() separado sin JOIN → siempre 1.
    //
    // HOY Assert.Single(result.Items) FALLA — Items vacío (fila perdida).
    //      (Si Assert.Single pasara, Assert.NotNull(item.ToLocationName) fallaría.)
    // Con el fix (IgnoreQueryFilters("SoftDelete") en la query): Items tiene 1 fila
    //   con ToLocationName = "SD-LOC-2".
    [DockerFact]
    public async Task GetAssetMovements_DestinoEliminado_NoPierdeNombreDestino()
    {
        await using var db = await CreateDatabaseAsync();
        await db.ExecuteAsync(BaselineSeedSql);

        var assetId = await db.ScalarAsync<long>("""
            INSERT INTO inventario.assets
                (inventory_code, asset_type_id, asset_status_id, current_location_id)
            SELECT 'SD-TEST-A001', at.asset_type_id, s.asset_status_id, 1
            FROM inventario.asset_types at
            CROSS JOIN inventario.asset_statuses s
            WHERE at.code = 'PC' AND s.code = 'DISPONIBLE'
            RETURNING asset_id
            """);

        // Insertar movimiento vía SQL directo para no alterar current_location_id
        // (el handler de movimiento lo haría y rompería el setup del test de eliminación).
        await db.ExecuteAsync($"""
            INSERT INTO inventario.asset_movements
                (asset_id, from_location_id, to_location_id, movement_reason_id, moved_by)
            SELECT {assetId}, 1, 2, mr.movement_reason_id, 2
            FROM inventario.movement_reasons mr WHERE mr.code = 'REUBICACION'
            """);

        // Borrar lógicamente la ubicación destino (loc 2 = "SD-LOC-2").
        await db.ExecuteAsync(
            "UPDATE inventario.locations SET is_deleted = true WHERE location_id = 2");

        using var ctx = db.CreateContext();
        var handler = new GetAssetMovementsHandler(ctx);
        var result = await handler.Handle(
            new GetAssetMovementsQuery(assetId, 1, 10), default);

        // Assert 1: la fila del movimiento DEBE aparecer (LEFT JOIN, no INNER JOIN).
        // HOY: PASA — EF usa LEFT JOIN, la fila está presente aunque ToLocation sea null.
        var item = Assert.Single(result.Items);

        // Assert 2: el nombre del destino NO debe perderse por el borrado lógico.
        // HOY: FALLA — ToLocationName == null porque el filtro de Location impide resolver el nombre.
        // Con el fix: "SD-LOC-2".
        Assert.NotNull(item.ToLocationName);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // (a2) Bug latente: asignación con ubicación borrada pierde el nombre
    // ─────────────────────────────────────────────────────────────────────────
    // GetAssetAssignmentsHandler incluye Location (FK nullable en asset_assignments).
    // La condición en el Select: "a.Location != null ? a.Location.Name : null"
    // EF traduce como LEFT JOIN con filtro de Location. Si la ubicación está borrada,
    // el LEFT JOIN no produce match → LocationName == null (nombre histórico perdido).
    //
    // HOY Assert.Single(result) PASA (asignación presente).
    //      Assert.NotNull(result[0].LocationName) FALLA (nombre null).
    // Con el fix (IgnoreQueryFilters("SoftDelete") en GetAssetAssignments + Where(!borrado)):
    //   LocationName = "SD-LOC-2".
    [DockerFact]
    public async Task GetAssetAssignments_UbicacionEliminada_NoPierdeNombreUbicacion()
    {
        await using var db = await CreateDatabaseAsync();
        await db.ExecuteAsync(BaselineSeedSql);

        var assetId = await db.ScalarAsync<long>("""
            INSERT INTO inventario.assets
                (inventory_code, asset_type_id, asset_status_id, current_location_id)
            SELECT 'SD-TEST-A201', at.asset_type_id, s.asset_status_id, 1
            FROM inventario.asset_types at
            CROSS JOIN inventario.asset_statuses s
            WHERE at.code = 'PC' AND s.code = 'ASIGNADO'
            RETURNING asset_id
            """);

        // Crear asignación con location_id = 2 ("SD-LOC-2") vía SQL directo.
        await db.ExecuteAsync($"""
            INSERT INTO inventario.asset_assignments
                (asset_id, assigned_to, assignment_type_id, location_id)
            SELECT {assetId}, 2, at.assignment_type_id, 2
            FROM inventario.assignment_types at WHERE at.code = 'PERSONAL'
            """);

        // Borrar lógicamente la ubicación referenciada en la asignación.
        await db.ExecuteAsync(
            "UPDATE inventario.locations SET is_deleted = true WHERE location_id = 2");

        using var ctx = db.CreateContext();
        var handler = new GetAssetAssignmentsHandler(ctx);
        var result = await handler.Handle(
            new GetAssetAssignmentsQuery(assetId), default);

        // Assert 1: la asignación debe aparecer (historial completo, D3).
        // HOY: PASA — la fila de asignación no depende de Location para existir.
        var assignment = Assert.Single(result);

        // Assert 2: el nombre de la ubicación NO debe perderse por el borrado lógico.
        // HOY: FALLA — LocationName == null porque el filtro de Location bloquea el JOIN.
        // Con el fix: "SD-LOC-2".
        Assert.NotNull(assignment.LocationName);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // (b1) Fase 4b — Asset sin filtro global hoy devuelve filas con is_deleted=true
    // ─────────────────────────────────────────────────────────────────────────
    [DockerFact]
    public async Task Fase4b_Asset_SinFiltroGlobal_DevuelveFilasBorradas()
    {
        await using var db = await CreateDatabaseAsync();
        await db.ExecuteAsync(BaselineSeedSql);

        var assetId = await db.ScalarAsync<long>("""
            INSERT INTO inventario.assets
                (inventory_code, asset_type_id, asset_status_id, current_location_id)
            SELECT 'SD-TEST-B001', at.asset_type_id, s.asset_status_id, 1
            FROM inventario.asset_types at
            CROSS JOIN inventario.asset_statuses s
            WHERE at.code = 'PC' AND s.code = 'DISPONIBLE'
            RETURNING asset_id
            """);

        await db.ExecuteAsync(
            $"UPDATE inventario.assets SET is_deleted = true WHERE asset_id = {assetId}");

        using var ctx = db.CreateContext();
        var ids = await ctx.Assets.Select(a => a.AssetId).ToListAsync();

        // Con el filtro global (fase 4b): la fila borrada no aparece.
        // HOY: aparece → Assert.DoesNotContain FALLA.
        Assert.DoesNotContain(assetId, ids);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // (b3) Fase 4d — AppUser sin filtro global devuelve filas borradas
    // ─────────────────────────────────────────────────────────────────────────
    [DockerFact]
    public async Task Fase4d_AppUser_SinFiltroGlobal_DevuelveFilasBorradas()
    {
        await using var db = await CreateDatabaseAsync();
        await db.ExecuteAsync(BaselineSeedSql);

        await db.ExecuteAsync(
            "UPDATE inventario.app_users SET is_deleted = true WHERE user_id = 2");

        using var ctx = db.CreateContext();
        var ids = await ctx.AppUsers.Select(u => u.UserId).ToListAsync();

        // Con el filtro global (fase 4d): el usuario borrado no aparece.
        // HOY: aparece → Assert.DoesNotContain FALLA.
        Assert.DoesNotContain(2L, ids);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // (c1) DeleteSupplier con adquisición activa no devuelve 409 hoy
    // ─────────────────────────────────────────────────────────────────────────
    // Excepción esperada: BusinessRuleViolationException.
    // Mapeo en middleware: ExceptionHandlingMiddleware.cs:L44-L52 → HTTP 400.
    // Objetivo (A4): HTTP 409. Pendiente: crear ReferenceInUseException → 409 en CP3.
    [DockerFact]
    public async Task DeleteSupplier_ConAdquisicionActiva_DebeRechazarConExcepcion()
    {
        await using var db = await CreateDatabaseAsync();

        var supplierId = await db.ScalarAsync<long>("""
            INSERT INTO inventario.suppliers (name) VALUES ('Proveedor SD C1')
            RETURNING supplier_id
            """);

        await db.ExecuteAsync($"""
            INSERT INTO inventario.acquisitions (supplier_id, currency_code)
            VALUES ({supplierId}, 'MXN')
            """);

        await using var svc = CreateDeleteServices(db);
        var handler = new DeleteSupplierHandler(svc.Context, svc.Auditor);

        // Con el fix (fase 4c): lanza ReferenceInUseException → HTTP 409.
        // HOY: sin check → borrado completa → ThrowsAsync FALLA.
        await Assert.ThrowsAsync<ReferenceInUseException>(() =>
            handler.Handle(
                new DeleteSupplierCommand(supplierId, 1), default).AsTask());
    }

    // ─────────────────────────────────────────────────────────────────────────
    // (c2) DeleteLocation con asignación activa no devuelve 409 hoy
    // ─────────────────────────────────────────────────────────────────────────
    // DeleteLocationHandler comprueba activos y sub-ubicaciones,
    // pero NO asignaciones activas (returned_at IS NULL). D6 + A4 exigen 409.
    // Excepción esperada: BusinessRuleViolationException (misma nota que c1).
    [DockerFact]
    public async Task DeleteLocation_ConAsignacionActiva_DebeRechazarConExcepcion()
    {
        await using var db = await CreateDatabaseAsync();
        await db.ExecuteAsync(BaselineSeedSql);

        // Ubicación 3 = la que se intentará borrar.
        await db.ExecuteAsync("""
            INSERT INTO inventario.locations (location_id, code, name, location_type_id)
            OVERRIDING SYSTEM VALUE
            SELECT 3, 'SD-LOC-3', 'Ubicación SD 3', lt.location_type_id
            FROM inventario.location_types lt WHERE lt.code = 'LABORATORIO'
            ON CONFLICT (location_id) DO NOTHING;

            SELECT setval(
                pg_get_serial_sequence('inventario.locations', 'location_id'),
                (SELECT MAX(location_id) FROM inventario.locations));
            """);

        // Activo en ubicación 1 (no en la 3) → hasAssets para loc 3 = false.
        var assetId = await db.ScalarAsync<long>("""
            INSERT INTO inventario.assets
                (inventory_code, asset_type_id, asset_status_id, current_location_id)
            SELECT 'SD-TEST-C201', at.asset_type_id, s.asset_status_id, 1
            FROM inventario.asset_types at
            CROSS JOIN inventario.asset_statuses s
            WHERE at.code = 'PC' AND s.code = 'ASIGNADO'
            RETURNING asset_id
            """);

        // Asignación activa (returned_at IS NULL) con location_id = 3.
        await db.ExecuteAsync($"""
            INSERT INTO inventario.asset_assignments
                (asset_id, assigned_to, assignment_type_id, location_id)
            SELECT {assetId}, 2, at.assignment_type_id, 3
            FROM inventario.assignment_types at WHERE at.code = 'PERSONAL'
            """);

        await using var svc = CreateDeleteServices(db);
        var handler = new DeleteLocationHandler(svc.Context, svc.Auditor);

        // Con el fix (fase 4c): lanza ReferenceInUseException → HTTP 409.
        // HOY: sin check de asignaciones → borrado completa → ThrowsAsync FALLA.
        await Assert.ThrowsAsync<ReferenceInUseException>(() =>
            handler.Handle(
                new DeleteLocationCommand(3, 1), default).AsTask());
    }

    // ─────────────────────────────────────────────────────────────────────────
    // (c3) DeleteAcquisition con activos asociados no devuelve 409 hoy  (A5)
    // ─────────────────────────────────────────────────────────────────────────
    // Excepción esperada: BusinessRuleViolationException (misma nota que c1).
    [DockerFact]
    public async Task DeleteAcquisition_ConActivosAsociados_DebeRechazarConExcepcion()
    {
        await using var db = await CreateDatabaseAsync();
        await db.ExecuteAsync(BaselineSeedSql);

        var supplierId = await db.ScalarAsync<long>("""
            INSERT INTO inventario.suppliers (name) VALUES ('Proveedor SD C3')
            RETURNING supplier_id
            """);

        var acquisitionId = await db.ScalarAsync<long>($"""
            INSERT INTO inventario.acquisitions (supplier_id, currency_code)
            VALUES ({supplierId}, 'MXN')
            RETURNING acquisition_id
            """);

        var assetId = await db.ScalarAsync<long>("""
            INSERT INTO inventario.assets
                (inventory_code, asset_type_id, asset_status_id, current_location_id)
            SELECT 'SD-TEST-C301', at.asset_type_id, s.asset_status_id, 1
            FROM inventario.asset_types at
            CROSS JOIN inventario.asset_statuses s
            WHERE at.code = 'PC' AND s.code = 'DISPONIBLE'
            RETURNING asset_id
            """);

        await db.ExecuteAsync($"""
            INSERT INTO inventario.acquisition_assets (acquisition_id, asset_id)
            VALUES ({acquisitionId}, {assetId})
            """);

        await using var svc = CreateDeleteServices(db);
        var handler = new DeleteAcquisitionHandler(svc.Context, svc.Auditor);

        // Con el fix (fase 4c): lanza ReferenceInUseException → HTTP 409.
        // HOY: sin check de acquisition_assets → borrado completa → ThrowsAsync FALLA.
        await Assert.ThrowsAsync<ReferenceInUseException>(() =>
            handler.Handle(
                new DeleteAcquisitionCommand(acquisitionId, 1), default).AsTask());
    }

    // ─────────────────────────────────────────────────────────────────────────
    // (e) GetAssetMovements con activo borrado → hoy KeyNotFoundException (404)
    //     Con A6 debe devolver 200 con el historial
    // ─────────────────────────────────────────────────────────────────────────
    [DockerFact]
    public async Task GetAssetMovements_ActivoBorrado_DevuelveHistorial_No404()
    {
        await using var db = await CreateDatabaseAsync();
        await db.ExecuteAsync(BaselineSeedSql);

        var assetId = await db.ScalarAsync<long>("""
            INSERT INTO inventario.assets
                (inventory_code, asset_type_id, asset_status_id, current_location_id)
            SELECT 'SD-TEST-E001', at.asset_type_id, s.asset_status_id, 1
            FROM inventario.asset_types at
            CROSS JOIN inventario.asset_statuses s
            WHERE at.code = 'PC' AND s.code = 'DISPONIBLE'
            RETURNING asset_id
            """);

        await db.ExecuteAsync($"""
            INSERT INTO inventario.asset_movements
                (asset_id, from_location_id, to_location_id, movement_reason_id, moved_by)
            SELECT {assetId}, 1, 2, mr.movement_reason_id, 2
            FROM inventario.movement_reasons mr WHERE mr.code = 'REUBICACION'
            """);

        await db.ExecuteAsync(
            $"UPDATE inventario.assets SET is_deleted = true WHERE asset_id = {assetId}");

        using var ctx = db.CreateContext();
        var handler = new GetAssetMovementsHandler(ctx);

        // Con el fix (A6, fase 4b): devuelve el historial (PagedResult con 1 fila).
        // HOY: lanza KeyNotFoundException porque el check es "!a.IsDeleted" → FALLA.
        var result = await handler.Handle(
            new GetAssetMovementsQuery(assetId, 1, 10), default);
        Assert.Equal(1, result.TotalCount);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Fábrica de servicios de borrado lógico para los tests (c*)
    // ─────────────────────────────────────────────────────────────────────────
    private static DeleteServicesScope CreateDeleteServices(PostgresDatabase database)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<TecnmInventarioDbContext>(
            opts => opts.UseNpgsql(database.ConnectionString));
        services.AddBusinessPipeline();
        services.AddScoped<ICurrentUserService>(_ => new FakeCurrentUser());
        services.AddScoped<IAuditLogService, AuditLogService>();
        services.AddScoped<ISoftDeleteAuditor, SoftDeleteAuditor>();

        var provider = services.BuildServiceProvider();
        var scope = provider.CreateScope();
        return new DeleteServicesScope(provider, scope);
    }

    private sealed class DeleteServicesScope : IAsyncDisposable
    {
        private readonly ServiceProvider _provider;
        private readonly IServiceScope _scope;

        public DeleteServicesScope(ServiceProvider provider, IServiceScope scope)
        {
            _provider = provider;
            _scope = scope;
            Context = scope.ServiceProvider.GetRequiredService<TecnmInventarioDbContext>();
            Auditor = scope.ServiceProvider.GetRequiredService<ISoftDeleteAuditor>();
        }

        public TecnmInventarioDbContext Context { get; }
        public ISoftDeleteAuditor Auditor { get; }

        public ValueTask DisposeAsync()
        {
            _scope.Dispose();
            return _provider.DisposeAsync();
        }
    }
}


