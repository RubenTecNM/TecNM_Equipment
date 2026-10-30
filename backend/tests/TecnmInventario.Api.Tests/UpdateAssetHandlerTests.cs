using TecnmInventario.Api.Common.Domain;
using TecnmInventario.Api.Common.Errors;
using TecnmInventario.Api.Core.Entities;
using TecnmInventario.Api.Features.Assets.UpdateAsset;

namespace TecnmInventario.Api.Tests;

public class UpdateAssetHandlerTests
{
    private static (TecnmInventario.Api.Data.TecnmInventarioDbContext Context, UpdateAssetHandler Handler)
        CreateHarness()
    {
        var context = TestDb.Create();
        var now = DateTime.UtcNow;

        context.AssetStatuses.AddRange(
            new AssetStatus
            {
                AssetStatusId = 1, Code = AssetStatusCodes.Disponible, Name = "Disponible",
                CreatedAt = now, UpdatedAt = now, Version = 1
            },
            new AssetStatus
            {
                AssetStatusId = 2, Code = AssetStatusCodes.DadoDeBaja, Name = "Dado de baja",
                CreatedAt = now, UpdatedAt = now, Version = 1
            });
        context.SaveChanges();

        return (context, new UpdateAssetHandler(context, new FakeCurrentUser(), new RecordingAuditLogService()));
    }

    private static Asset AddAsset(
        TecnmInventario.Api.Data.TecnmInventarioDbContext context,
        long assetStatusId,
        bool isDeleted = false)
    {
        var now = DateTime.UtcNow;
        var asset = new Asset
        {
            AssetId = 10,
            InventoryCode = "TEC-TEST-0010",
            AssetTypeId = 1,
            AssetStatusId = assetStatusId,
            CurrentLocationId = 7,
            Hostname = "host-original",
            CreatedAt = now,
            UpdatedAt = now,
            Version = 1,
            IsDeleted = isDeleted
        };
        context.Assets.Add(asset);
        context.SaveChanges();
        return asset;
    }

    [Fact]
    public async Task Activo_inexistente_devuelve_KeyNotFound()
    {
        var (context, handler) = CreateHarness();
        using var _ = context;

        var command = new UpdateAssetCommand(999, 1, null, null, null, null, null);

        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => handler.Handle(command, CancellationToken.None).AsTask());
    }

    [Fact]
    public async Task Activo_con_IsDeleted_devuelve_KeyNotFound()
    {
        var (context, handler) = CreateHarness();
        using var _ = context;
        AddAsset(context, assetStatusId: 1, isDeleted: true);

        var command = new UpdateAssetCommand(10, 1, null, null, null, null, null);

        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => handler.Handle(command, CancellationToken.None).AsTask());
    }

    [Fact]
    public async Task Activo_DADO_DE_BAJA_devuelve_BusinessRuleViolation()
    {
        var (context, handler) = CreateHarness();
        using var _ = context;
        AddAsset(context, assetStatusId: 2);

        var command = new UpdateAssetCommand(10, 1, null, null, "nuevo-host", null, null);

        var exception = await Assert.ThrowsAsync<BusinessRuleViolationException>(
            () => handler.Handle(command, CancellationToken.None).AsTask());

        Assert.Contains("dado de baja", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Actualizacion_valida_no_cambia_la_ubicacion()
    {
        var (context, handler) = CreateHarness();
        using var _ = context;
        AddAsset(context, assetStatusId: 1);

        var command = new UpdateAssetCommand(10, 1, null, null, null, "host-nuevo", "nota");

        await handler.Handle(command, CancellationToken.None);

        var updated = context.Assets.Single(a => a.AssetId == 10);
        Assert.Equal("host-nuevo", updated.Hostname);
        Assert.Equal(7, updated.CurrentLocationId);
        Assert.Equal(1, updated.UpdatedBy);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task ExpectedVersion_no_positiva_es_error_de_validacion(int expectedVersion)
    {
        var (context, _) = CreateHarness();
        using var __ = context;
        var validator = new UpdateAssetValidator(context);

        var result = await validator.ValidateAsync(
            new UpdateAssetCommand(10, expectedVersion, null, null, null, null, null));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(UpdateAssetCommand.ExpectedVersion));
    }
}
