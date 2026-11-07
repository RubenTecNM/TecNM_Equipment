using FluentValidation;
using Microsoft.EntityFrameworkCore;
using TecnmInventario.Api.Common.Audit;
using TecnmInventario.Api.Common.Domain;
using TecnmInventario.Api.Data;
using TecnmInventario.Api.Features.Assets.CreateAsset;
using TecnmInventario.Api.Features.Assets.GetAssetById;
using TecnmInventario.Api.Features.Assets.UpdateAssetStatus;
using TecnmInventario.Api.Features.Catalogs.GetCatalogs;
using TecnmInventario.Api.Features.Locations.GetLocationById;
using TecnmInventario.Api.Features.Locations.GetLocations;

namespace TecnmInventario.Api.Tests.Integration;

// ═══════════════════════════════════════════════════════════════════════════
// Subcheckpoint 4c.3 — opción B (catálogos exentos del filtro global).
//
// A1: un caso por cada catálogo que GetCatalogsResponse expone (13 de los
//   15: RelationType y Service no tienen propiedad en GetCatalogsResponse,
//   así que no entran en este Theory).
// A2-A6: deben pasar en VERDE hoy, porque los catálogos NUNCA tuvieron
//   filtro global (ni siquiera estaban filtrados antes de este checkpoint,
//   solo pendientes); el fix de 4c.3 formaliza esa exención, no la crea.
// ═══════════════════════════════════════════════════════════════════════════

[Trait("Category", "Integration")]
public sealed class Fase4c3CatalogosExentosTests : PostgresTestBase
{
    public Fase4c3CatalogosExentosTests(PostgresContainerFixture fixture)
        : base(fixture) { }

    public static IEnumerable<object[]> CatalogosDeGetCatalogs()
    {
        yield return new object[] { "asset_types", "asset_type_id", (Func<GetCatalogsResponse, IReadOnlyList<CatalogItem>>)(r => r.AssetTypes) };
        yield return new object[] { "asset_statuses", "asset_status_id", (Func<GetCatalogsResponse, IReadOnlyList<CatalogItem>>)(r => r.AssetStatuses) };
        yield return new object[] { "location_types", "location_type_id", (Func<GetCatalogsResponse, IReadOnlyList<CatalogItem>>)(r => r.LocationTypes) };
        yield return new object[] { "incident_types", "incident_type_id", (Func<GetCatalogsResponse, IReadOnlyList<CatalogItem>>)(r => r.IncidentTypes) };
        yield return new object[] { "incident_priorities", "incident_priority_id", (Func<GetCatalogsResponse, IReadOnlyList<CatalogItem>>)(r => r.IncidentPriorities) };
        yield return new object[] { "incident_statuses", "incident_status_id", (Func<GetCatalogsResponse, IReadOnlyList<CatalogItem>>)(r => r.IncidentStatuses) };
        yield return new object[] { "maintenance_types", "maintenance_type_id", (Func<GetCatalogsResponse, IReadOnlyList<CatalogItem>>)(r => r.MaintenanceTypes) };
        yield return new object[] { "movement_reasons", "movement_reason_id", (Func<GetCatalogsResponse, IReadOnlyList<CatalogItem>>)(r => r.MovementReasons) };
        yield return new object[] { "assignment_types", "assignment_type_id", (Func<GetCatalogsResponse, IReadOnlyList<CatalogItem>>)(r => r.AssignmentTypes) };
        yield return new object[] { "component_types", "component_type_id", (Func<GetCatalogsResponse, IReadOnlyList<CatalogItem>>)(r => r.ComponentTypes) };
        yield return new object[] { "disposal_reasons", "disposal_reason_id", (Func<GetCatalogsResponse, IReadOnlyList<CatalogItem>>)(r => r.DisposalReasons) };
        yield return new object[] { "software_types", "software_type_id", (Func<GetCatalogsResponse, IReadOnlyList<CatalogItem>>)(r => r.SoftwareTypes) };
        yield return new object[] { "license_types", "license_type_id", (Func<GetCatalogsResponse, IReadOnlyList<CatalogItem>>)(r => r.LicenseTypes) };
    }

    [Theory]
    [MemberData(nameof(CatalogosDeGetCatalogs))]
    public async Task GetCatalogs_CatalogoBorradoLogicamente_NoApareceEnLosCombos(
        string table, string idColumn, Func<GetCatalogsResponse, IReadOnlyList<CatalogItem>> selector)
    {
        await using var db = await CreateDatabaseAsync();

        var code = $"SD4C3{Guid.NewGuid():N}"[..10].ToUpperInvariant();
        var id = await db.ScalarAsync<long>($"""
            INSERT INTO inventario.{table} (code, name) VALUES ('{code}', 'Catálogo SD4C3')
            RETURNING {idColumn}
            """);

        await db.ExecuteAsync($"UPDATE inventario.{table} SET is_deleted = true WHERE {idColumn} = {id}");

        await using var context = db.CreateContext();
        var handler = new GetCatalogsHandler(context);
        var result = await handler.Handle(new GetCatalogsQuery(), CancellationToken.None);

        Assert.DoesNotContain(selector(result), item => item.Id == id);
    }

    [DockerFact]
    public async Task GetAssetById_TipoYEstadoBorradosLogicamente_SigueMostrandoElActivoConSusNombres()
    {
        await using var db = await CreateDatabaseAsync();

        var typeId = await db.ScalarAsync<long>("""
            INSERT INTO inventario.asset_types (code, name) VALUES ('SD4C3-A2-T', 'Tipo SD4C3 A2')
            RETURNING asset_type_id
            """);
        var statusId = await db.ScalarAsync<long>("""
            INSERT INTO inventario.asset_statuses (code, name) VALUES ('SD4C3-A2-S', 'Estado SD4C3 A2')
            RETURNING asset_status_id
            """);

        var assetId = await db.ScalarAsync<long>($"""
            INSERT INTO inventario.assets (inventory_code, asset_type_id, asset_status_id)
            VALUES ('SD4C3-A2-AST', {typeId}, {statusId})
            RETURNING asset_id
            """);

        await db.ExecuteAsync($"""
            UPDATE inventario.asset_types SET is_deleted = true WHERE asset_type_id = {typeId};
            UPDATE inventario.asset_statuses SET is_deleted = true WHERE asset_status_id = {statusId};
            """);

        await using var context = db.CreateContext();
        var handler = new GetAssetByIdHandler(context);

        var result = await handler.Handle(new GetAssetByIdQuery(assetId), CancellationToken.None);

        Assert.Equal("SD4C3-A2-T", result.AssetTypeCode);
        Assert.Equal("SD4C3-A2-S", result.StatusCode);
    }

    [DockerFact]
    public async Task GetLocations_TipoDeUbicacionBorradoLogicamente_SigueListandoLaUbicacion()
    {
        await using var db = await CreateDatabaseAsync();

        var typeId = await db.ScalarAsync<long>("""
            INSERT INTO inventario.location_types (code, name) VALUES ('SD4C3-A3-T', 'Tipo ubicación SD4C3 A3')
            RETURNING location_type_id
            """);

        var locationId = await db.ScalarAsync<long>($"""
            INSERT INTO inventario.locations (code, name, location_type_id)
            VALUES ('SD4C3-A3-L', 'Ubicación SD4C3 A3', {typeId})
            RETURNING location_id
            """);

        await db.ExecuteAsync(
            $"UPDATE inventario.location_types SET is_deleted = true WHERE location_type_id = {typeId}");

        await using var context = db.CreateContext();
        var handler = new GetLocationsHandler(context);

        var result = await handler.Handle(new GetLocationsQuery(), CancellationToken.None);

        var location = Assert.Single(result, l => l.LocationId == locationId);
        Assert.Equal("SD4C3-A3-T", location.LocationTypeCode);
    }

    [DockerFact]
    public async Task GetLocationById_TipoDeUbicacionBorradoLogicamente_SigueDevolviendoLaUbicacion()
    {
        await using var db = await CreateDatabaseAsync();

        var typeId = await db.ScalarAsync<long>("""
            INSERT INTO inventario.location_types (code, name) VALUES ('SD4C3-A4-T', 'Tipo ubicación SD4C3 A4')
            RETURNING location_type_id
            """);

        var locationId = await db.ScalarAsync<long>($"""
            INSERT INTO inventario.locations (code, name, location_type_id)
            VALUES ('SD4C3-A4-L', 'Ubicación SD4C3 A4', {typeId})
            RETURNING location_id
            """);

        await db.ExecuteAsync(
            $"UPDATE inventario.location_types SET is_deleted = true WHERE location_type_id = {typeId}");

        await using var context = db.CreateContext();
        var handler = new GetLocationByIdHandler(context);

        var result = await handler.Handle(new GetLocationByIdQuery(locationId), CancellationToken.None);

        Assert.Equal("SD4C3-A4-T", result.LocationTypeCode);
    }

    [DockerFact]
    public async Task UpdateAssetStatus_EstadoActualBorradoLogicamente_SigueOperando()
    {
        await using var db = await CreateDatabaseAsync();

        var assetId = await db.ScalarAsync<long>($"""
            INSERT INTO inventario.assets (inventory_code, asset_type_id, asset_status_id)
            SELECT 'SD4C3-A5-A1', at.asset_type_id, s.asset_status_id
            FROM inventario.asset_types at
            CROSS JOIN inventario.asset_statuses s
            WHERE at.code = 'PC' AND s.code = '{AssetStatusCodes.ConFalla}'
            RETURNING asset_id
            """);

        // El activo queda con el estado actual (CON_FALLA) borrado lógicamente
        // en el catálogo -- no debe impedir que el activo se encuentre ni que
        // la transición a un estado distinto, no borrado, funcione.
        await db.ExecuteAsync($"""
            UPDATE inventario.asset_statuses SET is_deleted = true WHERE code = '{AssetStatusCodes.ConFalla}'
            """);

        await using var context = db.CreateContext();
        var currentUser = new FakeCurrentUser();
        var audit = new AuditLogService(context, currentUser);
        var handler = new UpdateAssetStatusHandler(context, currentUser, audit);

        // Con el fix (F1/F2) y HOY (los catálogos nunca tuvieron filtro):
        // la transición se completa sin "activo no encontrado".
        var result = await handler.Handle(
            new UpdateAssetStatusCommand(
                assetId, 1, AssetStatusCodes.EnMantenimiento, "Reparación SD4C3 A5"),
            CancellationToken.None);

        Assert.Equal(AssetStatusCodes.EnMantenimiento, result.StatusCode);

        // Restaurar el catálogo para no afectar otras pruebas que reutilicen la plantilla.
        await db.ExecuteAsync($"""
            UPDATE inventario.asset_statuses SET is_deleted = false WHERE code = '{AssetStatusCodes.ConFalla}'
            """);
    }

    [DockerFact]
    public async Task CreateAsset_ConTipoBorradoLogicamente_FallaValidacion()
    {
        await using var db = await CreateDatabaseAsync();

        var typeId = await db.ScalarAsync<long>("""
            INSERT INTO inventario.asset_types (code, name) VALUES ('SD4C3-A6-T', 'Tipo SD4C3 A6')
            RETURNING asset_type_id
            """);

        await db.ExecuteAsync(
            $"UPDATE inventario.asset_types SET is_deleted = true WHERE asset_type_id = {typeId}");

        await using var context = db.CreateContext();
        var validator = new CreateAssetValidator(context);

        // F2: un catálogo borrado lógicamente no se puede elegir para
        // registros nuevos. El validador ya comprueba "!IsDeleted" hoy.
        var result = await validator.ValidateAsync(
            new CreateAssetCommand("SD4C3-A6-AST", typeId, null, null, null, null, null, null));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(CreateAssetCommand.AssetTypeId));
    }
}
