using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TecnmInventario.Api.Common.Audit;
using TecnmInventario.Api.Common.Behaviors;
using TecnmInventario.Api.Common.Deletion;
using TecnmInventario.Api.Common.Domain;
using TecnmInventario.Api.Common.Errors;
using TecnmInventario.Api.Common.Security;
using TecnmInventario.Api.Data;
using TecnmInventario.Api.Features.Incidents.GetIncidents;
using TecnmInventario.Api.Features.Locations.DeleteLocation;
using TecnmInventario.Api.Features.Procurement.Acquisitions.DeleteAcquisition;
using TecnmInventario.Api.Features.Procurement.Suppliers.DeleteSupplier;

namespace TecnmInventario.Api.Tests.Integration;

// ═══════════════════════════════════════════════════════════════════════════
// Subcheckpoint 4c.1 — análisis + tests en rojo, SIN tocar código de producción.
//
// (a)/(b): deuda de 4b en GetIncidentsHandler (guard `!a.IsDeleted` previo al
//   filtro por AssetId). (a) debe fallar hoy con KeyNotFoundException
//   (bug activo); (b) protege que un AssetId realmente inexistente siga
//   devolviendo 404 después de corregir (a).
//
// (c): no hay endpoints de borrado de catálogo (fase 4c, los 15 son de solo
//   lectura vía GetCatalogs/GetIncidents-validators); no se inventan tests.
//
// (d): variantes "solo fila borrada lógicamente" para los 3 Delete* ya en rojo.
//   Justificación por handler (task 5 del informe):
//   - DeleteSupplierHandler: CERO comprobación de referencias (ni activas ni
//     borradas) — cualquier variante falla igual, por el mismo motivo.
//   - DeleteLocationHandler: SÍ tiene una comprobación de activos (hasAssets),
//     pero con `&& !a.IsDeleted` explícito — IGNORA a propósito los activos
//     borrados lógicamente. Caso justificado de forma directa.
//   - DeleteAcquisitionHandler: CERO comprobación de acquisition_assets.
// ═══════════════════════════════════════════════════════════════════════════

[Trait("Category", "Integration")]
public sealed class Fase4c1SoftDeleteReproTests : PostgresTestBase
{
    public Fase4c1SoftDeleteReproTests(PostgresContainerFixture fixture)
        : base(fixture) { }

    // ─────────────────────────────────────────────────────────────────────────
    // (a) Deuda de 4b: filtrar incidentes por un activo borrado lógicamente
    // debe devolver su historial, no 404.
    // ─────────────────────────────────────────────────────────────────────────
    [DockerFact]
    public async Task GetIncidents_FiltradoPorActivoBorradoLogicamente_DevuelveSuHistorial()
    {
        await using var db = await CreateDatabaseAsync();
        var (assetId, incidentId) = await SeedAssetConIncidenteAbiertoAsync(db);

        await db.ExecuteAsync(
            $"UPDATE inventario.assets SET is_deleted = true WHERE asset_id = {assetId}");

        await using var context = db.CreateContext();
        var handler = new GetIncidentsHandler(context);

        // Con el fix (4c.2): 200 con el incidente en Items.
        // HOY: el guard `!a.IsDeleted` previo al filtro por AssetId lanza
        // KeyNotFoundException para un activo que SÍ existe, solo borrado.
        var result = await handler.Handle(
            new GetIncidentsQuery(1, 10, assetId, null, null, null, null, null, null),
            CancellationToken.None);

        Assert.Contains(result.Items, i => i.IncidentId == incidentId);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // (b) Protege que un AssetId realmente inexistente siga siendo 404.
    // ─────────────────────────────────────────────────────────────────────────
    [DockerFact]
    public async Task GetIncidents_FiltradoPorActivoInexistente_LanzaNoEncontrado()
    {
        await using var db = await CreateDatabaseAsync();
        var maxAssetId = await db.ScalarAsync<long>(
            "SELECT COALESCE(MAX(asset_id), 0) FROM inventario.assets");
        var inexistente = maxAssetId + 1000;

        await using var context = db.CreateContext();
        var handler = new GetIncidentsHandler(context);

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            handler.Handle(
                new GetIncidentsQuery(1, 10, inexistente, null, null, null, null, null, null),
                CancellationToken.None).AsTask());
    }

    // ─────────────────────────────────────────────────────────────────────────
    // (d) DeleteSupplier: cero comprobación hoy, ninguna variante distingue
    // fila activa de fila borrada lógicamente.
    // ─────────────────────────────────────────────────────────────────────────
    [DockerFact]
    public async Task DeleteSupplier_ReferenciadoSoloPorAdquisicionBorradaLogicamente_Rechaza409()
    {
        await using var db = await CreateDatabaseAsync();

        var supplierId = await db.ScalarAsync<long>("""
            INSERT INTO inventario.suppliers (name) VALUES ('Proveedor SD4C1 D1')
            RETURNING supplier_id
            """);

        var acquisitionId = await db.ScalarAsync<long>($"""
            INSERT INTO inventario.acquisitions (supplier_id, currency_code)
            VALUES ({supplierId}, 'MXN')
            RETURNING acquisition_id
            """);

        await db.ExecuteAsync(
            $"UPDATE inventario.acquisitions SET is_deleted = true WHERE acquisition_id = {acquisitionId}");

        await using var svc = CreateDeleteServices(db);
        var handler = new DeleteSupplierHandler(svc.Context, svc.Auditor);

        // Con el fix (4c.2): "en uso" incluye referencias desde filas borradas
        // lógicamente (reversibles) -> ReferenceInUseException.
        // HOY: sin ninguna comprobación -> ThrowsAsync FALLA.
        await Assert.ThrowsAsync<ReferenceInUseException>(() =>
            handler.Handle(
                new DeleteSupplierCommand(supplierId, 1), CancellationToken.None).AsTask());
    }

    // ─────────────────────────────────────────────────────────────────────────
    // (d) DeleteLocation: hasAssets ya existe pero con `&& !a.IsDeleted`
    // explícito -> ignora a propósito los activos borrados lógicamente.
    // ─────────────────────────────────────────────────────────────────────────
    [DockerFact]
    public async Task DeleteLocation_ReferenciadoSoloPorActivoBorradoLogicamente_Rechaza409()
    {
        await using var db = await CreateDatabaseAsync();

        var locationId = await db.ScalarAsync<long>("""
            INSERT INTO inventario.locations (code, name, location_type_id)
            SELECT 'SD4C1-D2', 'Ubicación SD4C1 D2', lt.location_type_id
            FROM inventario.location_types lt WHERE lt.code = 'LABORATORIO'
            RETURNING location_id
            """);

        var assetId = await db.ScalarAsync<long>($"""
            INSERT INTO inventario.assets
                (inventory_code, asset_type_id, asset_status_id, current_location_id)
            SELECT 'SD4C1-D2-A1', at.asset_type_id, s.asset_status_id, {locationId}
            FROM inventario.asset_types at
            CROSS JOIN inventario.asset_statuses s
            WHERE at.code = 'PC' AND s.code = 'DISPONIBLE'
            RETURNING asset_id
            """);

        await db.ExecuteAsync(
            $"UPDATE inventario.assets SET is_deleted = true WHERE asset_id = {assetId}");

        await using var svc = CreateDeleteServices(db);
        var handler = new DeleteLocationHandler(svc.Context, svc.Auditor);

        // Con el fix (4c.2): "en uso" incluye el activo borrado lógicamente
        // cuyo current_location_id sigue siendo esta ubicación (el historial
        // de movimientos lo sigue mostrando aquí) -> ReferenceInUseException.
        // HOY: hasAssets filtra `&& !a.IsDeleted` -> no bloquea -> ThrowsAsync FALLA.
        await Assert.ThrowsAsync<ReferenceInUseException>(() =>
            handler.Handle(
                new DeleteLocationCommand(locationId, 1), CancellationToken.None).AsTask());
    }

    // ─────────────────────────────────────────────────────────────────────────
    // (d) DeleteAcquisition: cero comprobación hoy (ni fila activa ni borrada).
    // ─────────────────────────────────────────────────────────────────────────
    [DockerFact]
    public async Task DeleteAcquisition_ReferenciadoSoloPorActivoBorradoLogicamente_Rechaza409()
    {
        await using var db = await CreateDatabaseAsync();

        var acquisitionId = await db.ScalarAsync<long>("""
            INSERT INTO inventario.acquisitions (currency_code)
            VALUES ('MXN')
            RETURNING acquisition_id
            """);

        var assetId = await db.ScalarAsync<long>("""
            INSERT INTO inventario.assets (inventory_code, asset_type_id, asset_status_id)
            SELECT 'SD4C1-D3-A1', at.asset_type_id, s.asset_status_id
            FROM inventario.asset_types at
            CROSS JOIN inventario.asset_statuses s
            WHERE at.code = 'PC' AND s.code = 'DISPONIBLE'
            RETURNING asset_id
            """);

        await db.ExecuteAsync($"""
            INSERT INTO inventario.acquisition_assets (acquisition_id, asset_id)
            VALUES ({acquisitionId}, {assetId})
            """);

        await db.ExecuteAsync(
            $"UPDATE inventario.assets SET is_deleted = true WHERE asset_id = {assetId}");

        await using var svc = CreateDeleteServices(db);
        var handler = new DeleteAcquisitionHandler(svc.Context, svc.Auditor);

        // Con el fix (4c.2): la línea de compra de un activo borrado
        // lógicamente sigue siendo historial (D3, GetAcquisitionById) ->
        // ReferenceInUseException. HOY: sin comprobación -> ThrowsAsync FALLA.
        await Assert.ThrowsAsync<ReferenceInUseException>(() =>
            handler.Handle(
                new DeleteAcquisitionCommand(acquisitionId, 1), CancellationToken.None).AsTask());
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Subcheckpoint 4c.2, paso A — tests que deben fallar hoy (A1-A4, A7) o
    // pasar en verde hoy (A5-A6, protegen contra bloquear de más).
    // ─────────────────────────────────────────────────────────────────────────

    [DockerFact]
    public async Task DeleteSupplier_ReferenciadoPorGarantia_Rechaza409()
    {
        await using var db = await CreateDatabaseAsync();

        var supplierId = await db.ScalarAsync<long>("""
            INSERT INTO inventario.suppliers (name) VALUES ('Proveedor SD4C2 A1')
            RETURNING supplier_id
            """);

        var assetId = await db.ScalarAsync<long>("""
            INSERT INTO inventario.assets (inventory_code, asset_type_id, asset_status_id)
            SELECT 'SD4C2-A1-A1', at.asset_type_id, s.asset_status_id
            FROM inventario.asset_types at
            CROSS JOIN inventario.asset_statuses s
            WHERE at.code = 'PC' AND s.code = 'DISPONIBLE'
            RETURNING asset_id
            """);

        await db.ExecuteAsync($"""
            INSERT INTO inventario.asset_warranties (asset_id, supplier_id)
            VALUES ({assetId}, {supplierId})
            """);

        await using var svc = CreateDeleteServices(db);
        var handler = new DeleteSupplierHandler(svc.Context, svc.Auditor);

        // Con el fix (E2): tiene garantías registradas -> ReferenceInUseException.
        // HOY: sin comprobación de garantías -> ThrowsAsync FALLA.
        await Assert.ThrowsAsync<ReferenceInUseException>(() =>
            handler.Handle(
                new DeleteSupplierCommand(supplierId, 1), CancellationToken.None).AsTask());
    }

    [DockerFact]
    public async Task DeleteLocation_ConHijoBorradoLogicamente_Rechaza409()
    {
        await using var db = await CreateDatabaseAsync();

        var parentId = await db.ScalarAsync<long>("""
            INSERT INTO inventario.locations (code, name, location_type_id)
            SELECT 'SD4C2-A2-P', 'Ubicación SD4C2 A2 padre', lt.location_type_id
            FROM inventario.location_types lt WHERE lt.code = 'LABORATORIO'
            RETURNING location_id
            """);

        var childId = await db.ScalarAsync<long>($"""
            INSERT INTO inventario.locations (code, name, location_type_id, parent_location_id)
            SELECT 'SD4C2-A2-H', 'Ubicación SD4C2 A2 hija', lt.location_type_id, {parentId}
            FROM inventario.location_types lt WHERE lt.code = 'LABORATORIO'
            RETURNING location_id
            """);

        await db.ExecuteAsync(
            $"UPDATE inventario.locations SET is_deleted = true WHERE location_id = {childId}");

        await using var svc = CreateDeleteServices(db);
        var handler = new DeleteLocationHandler(svc.Context, svc.Auditor);

        // Con el fix (E4, E1): hijo borrado lógicamente también cuenta como
        // "en uso" -> ReferenceInUseException.
        // HOY: hasChildren usa el filtro por defecto de Location (excluye
        // borrados) -> no bloquea -> ThrowsAsync FALLA.
        await Assert.ThrowsAsync<ReferenceInUseException>(() =>
            handler.Handle(
                new DeleteLocationCommand(parentId, 1), CancellationToken.None).AsTask());
    }

    [DockerFact]
    public async Task DeleteLocation_ReferenciadoSoloPorAsignacionDevuelta_Rechaza409()
    {
        await using var db = await CreateDatabaseAsync();
        await db.ExecuteAsync(BaselineSeedSql);

        var locationId = await db.ScalarAsync<long>("""
            INSERT INTO inventario.locations (code, name, location_type_id)
            SELECT 'SD4C2-A3', 'Ubicación SD4C2 A3', lt.location_type_id
            FROM inventario.location_types lt WHERE lt.code = 'LABORATORIO'
            RETURNING location_id
            """);

        var assetId = await db.ScalarAsync<long>("""
            INSERT INTO inventario.assets (inventory_code, asset_type_id, asset_status_id)
            SELECT 'SD4C2-A3-A1', at.asset_type_id, s.asset_status_id
            FROM inventario.asset_types at
            CROSS JOIN inventario.asset_statuses s
            WHERE at.code = 'PC' AND s.code = 'DISPONIBLE'
            RETURNING asset_id
            """);

        // Asignación DEVUELTA (returned_at no nulo): referencia histórica, no activa.
        await db.ExecuteAsync($"""
            INSERT INTO inventario.asset_assignments
                (asset_id, assigned_to, assignment_type_id, location_id, returned_at)
            SELECT {assetId}, 2, at.assignment_type_id, {locationId}, now()
            FROM inventario.assignment_types at WHERE at.code = 'PERSONAL'
            """);

        await using var svc = CreateDeleteServices(db);
        var handler = new DeleteLocationHandler(svc.Context, svc.Auditor);

        // Con el fix (E4, E1): cualquier asset_assignment referencia la
        // ubicación, devuelta o no -> ReferenceInUseException.
        // HOY: sin comprobación de asignaciones -> ThrowsAsync FALLA.
        await Assert.ThrowsAsync<ReferenceInUseException>(() =>
            handler.Handle(
                new DeleteLocationCommand(locationId, 1), CancellationToken.None).AsTask());
    }

    [DockerFact]
    public async Task DeleteLocation_ReferenciadoSoloPorMovimientoHistorico_Rechaza409()
    {
        await using var db = await CreateDatabaseAsync();
        await db.ExecuteAsync(BaselineSeedSql);

        var locationId = await db.ScalarAsync<long>("""
            INSERT INTO inventario.locations (code, name, location_type_id)
            SELECT 'SD4C2-A4', 'Ubicación SD4C2 A4', lt.location_type_id
            FROM inventario.location_types lt WHERE lt.code = 'LABORATORIO'
            RETURNING location_id
            """);

        var assetId = await db.ScalarAsync<long>("""
            INSERT INTO inventario.assets (inventory_code, asset_type_id, asset_status_id, current_location_id)
            SELECT 'SD4C2-A4-A1', at.asset_type_id, s.asset_status_id, 1
            FROM inventario.asset_types at
            CROSS JOIN inventario.asset_statuses s
            WHERE at.code = 'PC' AND s.code = 'DISPONIBLE'
            RETURNING asset_id
            """);

        // Movimiento histórico: el activo ya no está en location_id, solo pasó por ahí.
        await db.ExecuteAsync($"""
            INSERT INTO inventario.asset_movements
                (asset_id, from_location_id, to_location_id, movement_reason_id, moved_by)
            SELECT {assetId}, {locationId}, 1, mr.movement_reason_id, 2
            FROM inventario.movement_reasons mr WHERE mr.code = 'REUBICACION'
            """);

        await using var svc = CreateDeleteServices(db);
        var handler = new DeleteLocationHandler(svc.Context, svc.Auditor);

        // Con el fix (E4, E1): cualquier asset_movement con esta ubicación
        // como origen o destino -> ReferenceInUseException.
        // HOY: sin comprobación de movimientos -> ThrowsAsync FALLA.
        await Assert.ThrowsAsync<ReferenceInUseException>(() =>
            handler.Handle(
                new DeleteLocationCommand(locationId, 1), CancellationToken.None).AsTask());
    }

    [DockerFact]
    public async Task DeleteLocation_ReferenciadoPorBuildingSetting_Rechaza409()
    {
        await using var db = await CreateDatabaseAsync();

        var locationId = await db.ScalarAsync<long>("""
            INSERT INTO inventario.locations (code, name, location_type_id)
            SELECT 'SD4C2-A7', 'Ubicación SD4C2 A7', lt.location_type_id
            FROM inventario.location_types lt WHERE lt.code = 'LABORATORIO'
            RETURNING location_id
            """);

        await db.ExecuteAsync($"""
            INSERT INTO inventario.building_settings (location_id, setting_key, setting_value)
            VALUES ({locationId}, 'capacidad_personas', '30')
            """);

        await using var svc = CreateDeleteServices(db);
        var handler = new DeleteLocationHandler(svc.Context, svc.Auditor);

        // Con el fix (E4): building_settings referenciando la ubicación ->
        // ReferenceInUseException.
        // HOY: sin comprobación de building_settings -> ThrowsAsync FALLA.
        await Assert.ThrowsAsync<ReferenceInUseException>(() =>
            handler.Handle(
                new DeleteLocationCommand(locationId, 1), CancellationToken.None).AsTask());
    }

    [DockerFact]
    public async Task DeleteSupplier_SinReferencias_BorraLogicamente()
    {
        await using var db = await CreateDatabaseAsync();

        var supplierId = await db.ScalarAsync<long>("""
            INSERT INTO inventario.suppliers (name) VALUES ('Proveedor SD4C2 A5')
            RETURNING supplier_id
            """);

        await using var svc = CreateDeleteServices(db);
        var handler = new DeleteSupplierHandler(svc.Context, svc.Auditor);

        // Protege contra bloquear de más: sin ninguna referencia, debe borrar
        // lógicamente sin lanzar. Debe seguir en VERDE después del fix (E2).
        var result = await handler.Handle(
            new DeleteSupplierCommand(supplierId, 1), CancellationToken.None);

        Assert.True(result.Deleted);
    }

    [DockerFact]
    public async Task DeleteLocation_SinReferencias_BorraLogicamente()
    {
        await using var db = await CreateDatabaseAsync();

        var locationId = await db.ScalarAsync<long>("""
            INSERT INTO inventario.locations (code, name, location_type_id)
            SELECT 'SD4C2-A6', 'Ubicación SD4C2 A6', lt.location_type_id
            FROM inventario.location_types lt WHERE lt.code = 'LABORATORIO'
            RETURNING location_id
            """);

        await using var svc = CreateDeleteServices(db);
        var handler = new DeleteLocationHandler(svc.Context, svc.Auditor);

        // Protege contra bloquear de más: sin ninguna referencia, debe borrar
        // lógicamente sin lanzar. Debe seguir en VERDE después del fix (E4).
        var result = await handler.Handle(
            new DeleteLocationCommand(locationId, 1), CancellationToken.None);

        Assert.True(result.Deleted);
    }

    private const string BaselineSeedSql = """
        INSERT INTO inventario.locations (location_id, code, name, location_type_id)
        OVERRIDING SYSTEM VALUE
        SELECT 1, 'SD4C2-BASE-1', 'Ubicación base SD4C2', lt.location_type_id
        FROM inventario.location_types lt WHERE lt.code = 'LABORATORIO'
        ON CONFLICT (location_id) DO NOTHING;

        SELECT setval(
            pg_get_serial_sequence('inventario.locations', 'location_id'),
            (SELECT MAX(location_id) FROM inventario.locations));

        INSERT INTO inventario.app_users
            (user_id, username, email, password_hash, first_name, last_name, role_id, status)
        OVERRIDING SYSTEM VALUE
        SELECT 2, 'tec_sd4c2', 'tec-sd4c2@test.local', 'NO_LOGIN',
               'Técnico', 'SD4C2', r.role_id, 'activo'
        FROM inventario.roles r WHERE r.code = 'TECNICO'
        ON CONFLICT (user_id) DO NOTHING;

        SELECT setval(
            pg_get_serial_sequence('inventario.app_users', 'user_id'),
            (SELECT MAX(user_id) FROM inventario.app_users));
        """;

    private static async Task<(long AssetId, long IncidentId)> SeedAssetConIncidenteAbiertoAsync(
        PostgresDatabase db)
    {
        var inventoryCode = $"SD4C1-{Guid.NewGuid():N}"[..20];
        var assetId = await db.ScalarAsync<long>($"""
            INSERT INTO inventario.assets (inventory_code, asset_type_id, asset_status_id, created_by)
            SELECT '{inventoryCode}', at.asset_type_id, st.asset_status_id, 1
            FROM inventario.asset_types at, inventario.asset_statuses st
            WHERE at.code = 'PC' AND st.code = '{AssetStatusCodes.ConFalla}'
            RETURNING asset_id
            """);

        var incidentId = await db.ScalarAsync<long>($"""
            INSERT INTO inventario.incidents
                (asset_id, incident_type_id, incident_priority_id, incident_status_id,
                 reported_by, title, created_by)
            SELECT {assetId}, it.incident_type_id, ip.incident_priority_id,
                   ist.incident_status_id, 1, 'Falla reproducible 4c.1', 1
            FROM inventario.incident_types it, inventario.incident_priorities ip,
                 inventario.incident_statuses ist
            WHERE it.code = 'HARDWARE' AND ip.code = 'ALTA'
              AND ist.code = '{IncidentStatusCodes.Abierto}'
            RETURNING incident_id
            """);

        return (assetId, incidentId);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Fábrica de servicios de borrado lógico, igual patrón que
    // SoftDeleteFiltersIntegrationTests.cs (necesaria para construir los
    // Delete*Handler reales con ISoftDeleteAuditor inyectado).
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
