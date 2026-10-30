using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using TecnmInventario.Api.Common.Domain;
using TecnmInventario.Api.Core.Entities;
using TecnmInventario.Api.Features.Assets.UpdateAsset;

namespace TecnmInventario.Api.Tests.Integration;

/// <summary>
/// f) El trigger touch_version sube la versión en un UPDATE real y un PUT
/// idéntico con ExpectedVersion correcta no escribe ni sube la versión.
/// </summary>
[Trait("Category", "Integration")]
public sealed class UpdateAssetRealTriggerTests : PostgresTestBase
{
    public UpdateAssetRealTriggerTests(PostgresContainerFixture fixture)
        : base(fixture)
    {
    }

    [DockerFact]
    public async Task Update_real_sube_version_por_trigger_y_audita_una_sola_vez()
    {
        await using var db = await CreateDatabaseAsync();
        using var context = db.CreateContext();
        var audit = new Common.Audit.AuditLogService(context, new FakeCurrentUser());
        var handler = new UpdateAssetHandler(context, new FakeCurrentUser(), audit);

        var asset = await AddAssetAsync(context, "TEC-INT-UPD-1");

        var response = await handler.Handle(
            new UpdateAssetCommand(asset.AssetId, 1, null, null, "SN-REAL", "host-real", "nota real"),
            CancellationToken.None);

        Assert.Equal(2, response.Version);
        Assert.Equal(2, await db.ScalarAsync<int>(
            $"select version from inventario.assets where asset_id = {asset.AssetId}"));
        Assert.Equal(1, await db.CountAsync(
            "audit_log", $"table_name = 'assets' and record_id = {asset.AssetId} and action = 'UPDATE'"));
    }

    [DockerFact]
    public async Task Put_identico_con_ExpectedVersion_correcta_no_escribe_ni_sube_version()
    {
        await using var db = await CreateDatabaseAsync();
        using var context = db.CreateContext();
        var audit = new Common.Audit.AuditLogService(context, new FakeCurrentUser());
        var handler = new UpdateAssetHandler(context, new FakeCurrentUser(), audit);

        var asset = await AddAssetAsync(context, "TEC-INT-UPD-2", serial: "SN-IGUAL", hostname: "host-igual");

        // Primer PUT con la versión real: cambia y sube a 2.
        var first = await handler.Handle(
            new UpdateAssetCommand(asset.AssetId, 1, null, null, "SN-NUEVO", "host-nuevo", null),
            CancellationToken.None);
        Assert.Equal(2, first.Version);

        // Segundo PUT con los mismos valores y la versión correcta (2): no-op.
        var second = await handler.Handle(
            new UpdateAssetCommand(asset.AssetId, 2, null, null, "SN-NUEVO", "host-nuevo", null),
            CancellationToken.None);

        Assert.Equal(2, second.Version);
        Assert.Equal(2, await db.ScalarAsync<int>(
            $"select version from inventario.assets where asset_id = {asset.AssetId}"));
        Assert.Equal(1, await db.CountAsync(
            "audit_log", $"table_name = 'assets' and record_id = {asset.AssetId}"));
    }

    private static async Task<Asset> AddAssetAsync(
        Data.TecnmInventarioDbContext context, string inventoryCode,
        string? serial = null, string? hostname = null)
    {
        var now = DateTime.UtcNow;
        var asset = new Asset
        {
            InventoryCode = inventoryCode,
            AssetTypeId = 1,
            AssetStatusId = 1,
            SerialNumber = serial,
            Hostname = hostname,
            CreatedAt = now,
            UpdatedAt = now,
            Version = 1
        };
        context.Assets.Add(asset);
        await context.SaveChangesAsync();
        return asset;
    }
}
