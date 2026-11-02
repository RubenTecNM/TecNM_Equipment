using Microsoft.EntityFrameworkCore;
using TecnmInventario.Api.Common.Audit;
using TecnmInventario.Api.Common.Domain;
using TecnmInventario.Api.Common.Errors;
using TecnmInventario.Api.Data;
using TecnmInventario.Api.Features.Assets.UpdateAssetStatus;

namespace TecnmInventario.Api.Tests.Integration;

/// <summary>
/// UpdateAssetStatus contra PostgreSQL real. La comprobación de incidentes
/// abiertos se movió DENTRO de la transacción (después del lock del activo):
/// estos casos fijan que el 400 y su mensaje no cambiaron, y que la versión
/// vieja sigue devolviendo 409 antes de evaluar la regla de negocio.
/// </summary>
[Trait("Category", "Integration")]
public sealed class UpdateAssetStatusPostgresTests : PostgresTestBase
{
    private const string OpenIncidentMessage =
        "No se puede liberar el activo: tiene incidentes abiertos. Resuélvalos primero.";

    public UpdateAssetStatusPostgresTests(PostgresContainerFixture fixture)
        : base(fixture)
    {
    }

    [DockerFact]
    public async Task Liberar_con_incidente_abierto_devuelve_el_mismo_400_y_no_escribe()
    {
        await using var db = await CreateDatabaseAsync();
        var assetId = await SeedAssetAsync(db, AssetStatusCodes.ConFalla, withOpenIncident: true);
        using var context = new TecnmInventarioDbContext(db.CreateOptions());
        var currentUser = new FakeCurrentUser();
        var handler = new UpdateAssetStatusHandler(
            context, currentUser, new AuditLogService(context, currentUser));

        var exception = await Assert.ThrowsAsync<BusinessRuleViolationException>(() =>
            handler.Handle(
                new UpdateAssetStatusCommand(
                    assetId, 1, AssetStatusCodes.Disponible, "Intento de liberación"),
                CancellationToken.None).AsTask());

        Assert.Equal(OpenIncidentMessage, exception.Message);
        Assert.Equal(AssetStatusCodes.ConFalla, await StatusCodeAsync(db, assetId));
        Assert.Equal(0, await db.CountAsync("audit_log", "table_name = 'assets'"));
    }

    [DockerFact]
    public async Task Liberar_sin_incidentes_actualiza_el_activo_y_audita()
    {
        await using var db = await CreateDatabaseAsync();
        var assetId = await SeedAssetAsync(db, AssetStatusCodes.ConFalla, withOpenIncident: false);
        using var context = new TecnmInventarioDbContext(db.CreateOptions());
        var currentUser = new FakeCurrentUser();
        var handler = new UpdateAssetStatusHandler(
            context, currentUser, new AuditLogService(context, currentUser));

        var response = await handler.Handle(
            new UpdateAssetStatusCommand(assetId, 1, AssetStatusCodes.Disponible, "Liberado"),
            CancellationToken.None);

        Assert.Equal(AssetStatusCodes.Disponible, response.StatusCode);
        Assert.Equal(AssetStatusCodes.Disponible, await StatusCodeAsync(db, assetId));
        Assert.Equal(1, await db.CountAsync(
            "audit_log",
            $"table_name = 'assets' and record_id = {assetId} and action = 'UPDATE'"));
    }

    [DockerFact]
    public async Task Version_vieja_devuelve_409_antes_de_evaluar_la_regla_de_negocio()
    {
        await using var db = await CreateDatabaseAsync();
        var assetId = await SeedAssetAsync(db, AssetStatusCodes.ConFalla, withOpenIncident: true);
        using var context = new TecnmInventarioDbContext(db.CreateOptions());
        var currentUser = new FakeCurrentUser();
        var handler = new UpdateAssetStatusHandler(
            context, currentUser, new AuditLogService(context, currentUser));

        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() =>
            handler.Handle(
                new UpdateAssetStatusCommand(
                    assetId, 99, AssetStatusCodes.Disponible, "Versión vieja"),
                CancellationToken.None).AsTask());

        Assert.Equal(AssetStatusCodes.ConFalla, await StatusCodeAsync(db, assetId));
        Assert.Equal(0, await db.CountAsync("audit_log", "table_name = 'assets'"));
    }

    private static async Task<long> SeedAssetAsync(
        PostgresDatabase db, string assetStatusCode, bool withOpenIncident)
    {
        var inventoryCode = $"UAS-{Guid.NewGuid():N}"[..20];
        var assetId = await db.ScalarAsync<long>($"""
            insert into inventario.assets (inventory_code, asset_type_id, asset_status_id, created_by)
            select '{inventoryCode}', at.asset_type_id, st.asset_status_id, 1
            from inventario.asset_types at, inventario.asset_statuses st
            where at.code = 'PC' and st.code = '{assetStatusCode}'
            returning asset_id
            """);

        if (withOpenIncident)
        {
            await db.ExecuteAsync($"""
                insert into inventario.incidents
                    (asset_id, incident_type_id, incident_priority_id, incident_status_id,
                     reported_by, title, created_by)
                select {assetId}, it.incident_type_id, ip.incident_priority_id,
                       ist.incident_status_id, 1, 'Falla de prueba', 1
                from inventario.incident_types it, inventario.incident_priorities ip,
                     inventario.incident_statuses ist
                where it.code = 'HARDWARE' and ip.code = 'ALTA'
                  and ist.code = '{IncidentStatusCodes.Abierto}'
                """);
        }

        return assetId;
    }

    private static Task<string> StatusCodeAsync(PostgresDatabase db, long assetId)
        => db.ScalarAsync<string>($"""
            select st.code
            from inventario.assets a
            join inventario.asset_statuses st on st.asset_status_id = a.asset_status_id
            where a.asset_id = {assetId}
            """);
}
