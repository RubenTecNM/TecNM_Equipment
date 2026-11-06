using TecnmInventario.Api.Common.Audit;
using TecnmInventario.Api.Common.Domain;
using TecnmInventario.Api.Common.Errors;
using TecnmInventario.Api.Data;
using TecnmInventario.Api.Features.Assets.GetAssetById;
using TecnmInventario.Api.Features.Assets.Shared;
using TecnmInventario.Api.Features.Incidents.GetIncidentById;
using TecnmInventario.Api.Features.Incidents.GetIncidents;
using TecnmInventario.Api.Features.Incidents.ResolveIncident;
using TecnmInventario.Api.Features.Incidents.UpdateIncidentStatus;
using TecnmInventario.Api.Features.Procurement.Acquisitions.GetAcquisitionById;

namespace TecnmInventario.Api.Tests.Integration;

// ═══════════════════════════════════════════════════════════════════════════
// Subcheckpoint 4b.1 — tests de reproducción, SIN tocar código de producción.
//
// (a)/(b): ResolveIncident y UpdateIncidentStatus sobre un incidente cuyo
//   activo tiene is_deleted=true deben rechazar con BusinessRuleViolationException.
//   HOY: ningún handler comprueba Asset.IsDeleted, así que la operación completa
//   con éxito y Assert.ThrowsAsync falla con "No exception was thrown".
//
// (c) HISTORIAL (GetIncidentById, GetIncidents, GetAcquisitionById): la tarea 4
//   encontró que Incident.Asset y AcquisitionAsset.Asset son navegaciones
//   REQUERIDAS (AssetId no nulo) hacia una entidad que aún no tiene el filtro
//   "SoftDelete" (Asset sigue en SoftDeletePendingPhase). Sin ese filtro, HOY
//   esas consultas no pueden perder la fila por el bug de required-navigation:
//   cualquier aserción de "sigue visible" pasaría en verde sin probar nada
//   (ver informe del checkpoint, task 6). No se añade ese test aquí; se escribe
//   en 4b.2 junto con el fix (ahí sí puede estar en rojo antes de aplicar
//   IgnoreQueryFilters(["SoftDelete"]) + Where explícito sobre la raíz).
// ═══════════════════════════════════════════════════════════════════════════

[Trait("Category", "Integration")]
public sealed class Fase4b1AssetSoftDeleteReproTests : PostgresTestBase
{
    public Fase4b1AssetSoftDeleteReproTests(PostgresContainerFixture fixture)
        : base(fixture) { }

    [DockerFact]
    public async Task ResolveIncident_ActivoBorradoLogicamente_DebeRechazarConBusinessRuleViolation()
    {
        await using var db = await CreateDatabaseAsync();
        var (assetId, incidentId) = await SeedAssetConIncidenteAbiertoAsync(db);

        await db.ExecuteAsync(
            $"UPDATE inventario.assets SET is_deleted = true WHERE asset_id = {assetId}");

        await using var context = db.CreateContext();
        var currentUser = new FakeCurrentUser();
        var audit = new AuditLogService(context, currentUser);
        var handler = new ResolveIncidentHandler(
            context, currentUser, audit, new AssetStatusChanger(context, currentUser, audit));

        // Con el fix (4b.2): BusinessRuleViolationException mencionando el activo.
        // HOY: ningún check de Asset.IsDeleted → el handler resuelve el incidente
        // con éxito y ThrowsAsync FALLA con "No exception was thrown".
        var exception = await Assert.ThrowsAsync<BusinessRuleViolationException>(() =>
            handler.Handle(
                new ResolveIncidentCommand(incidentId, 1, "Solución de prueba", null),
                CancellationToken.None).AsTask());

        Assert.Contains("activo", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [DockerFact]
    public async Task UpdateIncidentStatus_ActivoBorradoLogicamente_DebeRechazarConBusinessRuleViolation()
    {
        await using var db = await CreateDatabaseAsync();
        var (assetId, incidentId) = await SeedAssetConIncidenteAbiertoAsync(db);

        await db.ExecuteAsync(
            $"UPDATE inventario.assets SET is_deleted = true WHERE asset_id = {assetId}");

        await using var context = db.CreateContext();
        var currentUser = new FakeCurrentUser();
        var audit = new AuditLogService(context, currentUser);
        var handler = new UpdateIncidentStatusHandler(
            context, currentUser, audit, new AssetStatusChanger(context, currentUser, audit));

        // Con el fix (4b.2): BusinessRuleViolationException mencionando el activo.
        // HOY: ningún check de Asset.IsDeleted → la transición ABIERTO -> EN_REPARACION
        // se completa con éxito y ThrowsAsync FALLA con "No exception was thrown".
        var exception = await Assert.ThrowsAsync<BusinessRuleViolationException>(() =>
            handler.Handle(
                new UpdateIncidentStatusCommand(
                    incidentId, 1, IncidentStatusCodes.EnReparacion, "Reparación de prueba"),
                CancellationToken.None).AsTask());

        Assert.Contains("activo", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Paso A — tests que deben pasar en VERDE hoy (Asset/Manufacturer aún sin
    // filtro "SoftDelete"; Incident y Acquisition ya lo tienen desde la 4a).
    // ─────────────────────────────────────────────────────────────────────────

    [DockerFact]
    public async Task GetIncidentById_ActivoBorradoLogicamente_SigueDevolviendoElIncidente()
    {
        await using var db = await CreateDatabaseAsync();
        var (assetId, incidentId, inventoryCode) = await SeedAssetConIncidenteAbiertoConCodigoAsync(db);

        await db.ExecuteAsync(
            $"UPDATE inventario.assets SET is_deleted = true WHERE asset_id = {assetId}");

        await using var context = db.CreateContext();
        var handler = new GetIncidentByIdHandler(context);

        var result = await handler.Handle(new GetIncidentByIdQuery(incidentId), CancellationToken.None);

        Assert.Equal(inventoryCode, result.AssetInventoryCode);
    }

    [DockerFact]
    public async Task GetIncidents_ActivoBorradoLogicamente_IncluyeElIncidenteYTotalCountCuadra()
    {
        await using var db = await CreateDatabaseAsync();
        var (assetId, incidentId, _) = await SeedAssetConIncidenteAbiertoConCodigoAsync(db);

        await db.ExecuteAsync(
            $"UPDATE inventario.assets SET is_deleted = true WHERE asset_id = {assetId}");

        await using var context = db.CreateContext();
        var handler = new GetIncidentsHandler(context);

        // Sin filtrar por AssetId: ese camino tiene un guard aparte
        // (comprobación `!a.IsDeleted` en GetIncidentsHandler) que no forma
        // parte del alcance de este checkpoint; ver nota en el informe.
        var result = await handler.Handle(
            new GetIncidentsQuery(1, 10, null, null, null, null, null, null, null),
            CancellationToken.None);

        Assert.Contains(result.Items, i => i.IncidentId == incidentId);
        Assert.Equal(1, result.TotalCount);
    }

    [DockerFact]
    public async Task GetAcquisitionById_ActivoBorradoLogicamente_ConservaLaLineaDeCompra()
    {
        await using var db = await CreateDatabaseAsync();
        var (acquisitionId, assetId, inventoryCode) = await SeedAdquisicionConActivoAsync(db);

        await db.ExecuteAsync(
            $"UPDATE inventario.assets SET is_deleted = true WHERE asset_id = {assetId}");

        await using var context = db.CreateContext();
        var handler = new GetAcquisitionByIdHandler(context);

        var result = await handler.Handle(new GetAcquisitionByIdQuery(acquisitionId), CancellationToken.None);

        var line = Assert.Single(result.Assets);
        Assert.Equal(inventoryCode, line.InventoryCode);
    }

    [DockerFact]
    public async Task GetAssetById_FabricanteBorradoLogicamente_MuestraNombreDelFabricante()
    {
        await using var db = await CreateDatabaseAsync();
        var manufacturerName = $"Fabricante SD4B2 {Guid.NewGuid():N}"[..30];
        var manufacturerId = await db.ScalarAsync<long>($"""
            INSERT INTO inventario.manufacturers (name) VALUES ('{manufacturerName}')
            RETURNING manufacturer_id
            """);

        var assetId = await db.ScalarAsync<long>($"""
            INSERT INTO inventario.assets
                (inventory_code, asset_type_id, asset_status_id, manufacturer_id, created_by)
            SELECT 'SD4B2-{Guid.NewGuid():N}'::varchar(24), at.asset_type_id, st.asset_status_id, {manufacturerId}, 1
            FROM inventario.asset_types at, inventario.asset_statuses st
            WHERE at.code = 'PC' AND st.code = '{AssetStatusCodes.Disponible}'
            RETURNING asset_id
            """);

        await db.ExecuteAsync(
            $"UPDATE inventario.manufacturers SET is_deleted = true WHERE manufacturer_id = {manufacturerId}");

        await using var context = db.CreateContext();
        var handler = new GetAssetByIdHandler(context);

        var result = await handler.Handle(new GetAssetByIdQuery(assetId), CancellationToken.None);

        Assert.Equal(manufacturerName, result.ManufacturerName);
    }

    [DockerFact]
    public async Task GetIncidents_IncidenteBorradoLogicamente_NoAparece()
    {
        await using var db = await CreateDatabaseAsync();
        var (_, incidentId, _) = await SeedAssetConIncidenteAbiertoConCodigoAsync(db);

        await db.ExecuteAsync(
            $"UPDATE inventario.incidents SET is_deleted = true WHERE incident_id = {incidentId}");

        await using var context = db.CreateContext();
        var handler = new GetIncidentsHandler(context);

        var result = await handler.Handle(
            new GetIncidentsQuery(1, 10, null, null, null, null, null, null, null),
            CancellationToken.None);

        Assert.DoesNotContain(result.Items, i => i.IncidentId == incidentId);
    }

    [DockerFact]
    public async Task GetIncidentById_IncidenteBorradoLogicamente_DevuelveNoEncontrado()
    {
        await using var db = await CreateDatabaseAsync();
        var (_, incidentId, _) = await SeedAssetConIncidenteAbiertoConCodigoAsync(db);

        await db.ExecuteAsync(
            $"UPDATE inventario.incidents SET is_deleted = true WHERE incident_id = {incidentId}");

        await using var context = db.CreateContext();
        var handler = new GetIncidentByIdHandler(context);

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            handler.Handle(new GetIncidentByIdQuery(incidentId), CancellationToken.None).AsTask());
    }

    [DockerFact]
    public async Task GetAcquisitionById_CompraBorradaLogicamente_DevuelveNoEncontrado()
    {
        await using var db = await CreateDatabaseAsync();
        var (acquisitionId, _, _) = await SeedAdquisicionConActivoAsync(db);

        await db.ExecuteAsync(
            $"UPDATE inventario.acquisitions SET is_deleted = true WHERE acquisition_id = {acquisitionId}");

        await using var context = db.CreateContext();
        var handler = new GetAcquisitionByIdHandler(context);

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            handler.Handle(new GetAcquisitionByIdQuery(acquisitionId), CancellationToken.None).AsTask());
    }

    private static async Task<(long AssetId, long IncidentId, string InventoryCode)>
        SeedAssetConIncidenteAbiertoConCodigoAsync(PostgresDatabase db)
    {
        var inventoryCode = $"SD4B2-{Guid.NewGuid():N}"[..20];
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
                   ist.incident_status_id, 1, 'Falla reproducible 4b.2', 1
            FROM inventario.incident_types it, inventario.incident_priorities ip,
                 inventario.incident_statuses ist
            WHERE it.code = 'HARDWARE' AND ip.code = 'ALTA'
              AND ist.code = '{IncidentStatusCodes.Abierto}'
            RETURNING incident_id
            """);

        return (assetId, incidentId, inventoryCode);
    }

    private static async Task<(long AcquisitionId, long AssetId, string InventoryCode)>
        SeedAdquisicionConActivoAsync(PostgresDatabase db)
    {
        var inventoryCode = $"SD4B2A-{Guid.NewGuid():N}"[..20];
        var assetId = await db.ScalarAsync<long>($"""
            INSERT INTO inventario.assets (inventory_code, asset_type_id, asset_status_id, created_by)
            SELECT '{inventoryCode}', at.asset_type_id, st.asset_status_id, 1
            FROM inventario.asset_types at, inventario.asset_statuses st
            WHERE at.code = 'PC' AND st.code = '{AssetStatusCodes.Disponible}'
            RETURNING asset_id
            """);

        var acquisitionId = await db.ScalarAsync<long>("""
            INSERT INTO inventario.acquisitions (currency_code)
            VALUES ('MXN')
            RETURNING acquisition_id
            """);

        await db.ExecuteAsync($"""
            INSERT INTO inventario.acquisition_assets (acquisition_id, asset_id)
            VALUES ({acquisitionId}, {assetId})
            """);

        return (acquisitionId, assetId, inventoryCode);
    }

    private static async Task<(long AssetId, long IncidentId)> SeedAssetConIncidenteAbiertoAsync(
        PostgresDatabase db)
    {
        var inventoryCode = $"SD4B1-{Guid.NewGuid():N}"[..20];
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
                   ist.incident_status_id, 1, 'Falla reproducible 4b.1', 1
            FROM inventario.incident_types it, inventario.incident_priorities ip,
                 inventario.incident_statuses ist
            WHERE it.code = 'HARDWARE' AND ip.code = 'ALTA'
              AND ist.code = '{IncidentStatusCodes.Abierto}'
            RETURNING incident_id
            """);

        return (assetId, incidentId);
    }
}
