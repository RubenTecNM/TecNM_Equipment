using Microsoft.EntityFrameworkCore;
using TecnmInventario.Api.Common.Domain;
using TecnmInventario.Api.Core.Entities;
using TecnmInventario.Api.Features.Assets.CreateAsset;
using TecnmInventario.Api.Features.Assets.UpdateAsset;
using TecnmInventario.Api.Features.Locations.CreateLocation;
using TecnmInventario.Api.Features.Locations.UpdateLocation;
using TecnmInventario.Api.Features.Procurement.Suppliers.CreateSupplier;
using TecnmInventario.Api.Features.Procurement.Suppliers.UpdateSupplier;

namespace TecnmInventario.Api.Tests;

/// <summary>
/// Subetapa B1: auditoría explícita de Assets, Locations y Suppliers.
/// Un UPDATE sin cambios reales no debe escribir en audit_log, ni llamar a
/// SaveChanges, ni tocar UpdatedBy o Version. Un UPDATE real y cada INSERT
/// deben dejar exactamente una fila con la acción correcta.
/// </summary>
public class AuditB1Tests
{
    // ---------------------------------------------------------------- Assets

    private static (TecnmInventario.Api.Data.TecnmInventarioDbContext Context,
        UpdateAssetHandler Handler,
        RecordingAuditLogService Audit,
        SaveChangesCounter Counter,
        Asset Asset) CreateAssetHarness()
    {
        var counter = new SaveChangesCounter();
        var context = TestDb.Create(counter);
        var now = DateTime.UtcNow;

        context.AssetStatuses.Add(new AssetStatus
        {
            AssetStatusId = 1,
            Code = AssetStatusCodes.Disponible,
            Name = "Disponible",
            CreatedAt = now,
            UpdatedAt = now,
            Version = 1
        });

        var asset = new Asset
        {
            AssetId = 10,
            InventoryCode = "TEC-AUDIT-0010",
            AssetTypeId = 1,
            AssetStatusId = 1,
            CurrentLocationId = 7,
            Hostname = "host-original",
            CreatedAt = now,
            UpdatedAt = now,
            Version = 1
        };
        context.Assets.Add(asset);
        context.SaveChanges();
        counter.Reset();

        var audit = new RecordingAuditLogService();
        var handler = new UpdateAssetHandler(context, new FakeCurrentUser(), audit);
        return (context, handler, audit, counter, asset);
    }

    [Fact]
    public async Task UpdateAsset_identico_no_audita_ni_guarda_ni_sube_version()
    {
        var (context, handler, audit, counter, asset) = CreateAssetHarness();
        using var _ = context;

        var response = await handler.Handle(
            new UpdateAssetCommand(10, 1, null, null, null, "host-original", null),
            CancellationToken.None);

        Assert.Empty(audit.Entries);
        Assert.Equal(0, counter.Count);
        Assert.Null(asset.UpdatedBy);
        Assert.Equal(1, response.Version);
        Assert.Equal(1, asset.Version);
    }

    [Fact]
    public async Task UpdateAsset_version_vieja_con_datos_identicos_devuelve_conflicto()
    {
        var (context, handler, audit, counter, _) = CreateAssetHarness();
        using var __ = context;

        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => handler.Handle(
            new UpdateAssetCommand(10, 99, null, null, null, "host-original", null),
            CancellationToken.None).AsTask());

        Assert.Empty(audit.Entries);
        Assert.Equal(0, counter.Count);
    }

    [Fact]
    public async Task UpdateAsset_con_cambios_audita_UPDATE_con_snapshot_antes_y_despues()
    {
        var (context, handler, audit, counter, asset) = CreateAssetHarness();
        using var _ = context;

        await handler.Handle(
            new UpdateAssetCommand(10, 1, null, null, "SER-1", "host-nuevo", "nota"),
            CancellationToken.None);

        var entry = Assert.Single(audit.Entries);
        Assert.Equal("assets", entry.TableName);
        Assert.Equal(10, entry.RecordId);
        Assert.Equal("UPDATE", entry.Action);
        Assert.NotNull(entry.OldData);
        Assert.NotNull(entry.NewData);
        Assert.True(counter.Count >= 1);
        Assert.Equal(1, asset.UpdatedBy);
        Assert.Equal("host-nuevo", asset.Hostname);
    }

    [Fact]
    public async Task CreateAsset_audita_INSERT_con_oldData_nulo()
    {
        var context = TestDb.Create();
        using var _ = context;
        var now = DateTime.UtcNow;
        context.AssetTypes.Add(new AssetType
        {
            AssetTypeId = 5, Code = "LAPTOP", Name = "Laptop",
            CreatedAt = now, UpdatedAt = now, Version = 1
        });
        context.AssetStatuses.Add(new AssetStatus
        {
            AssetStatusId = 1, Code = AssetStatusCodes.Disponible, Name = "Disponible",
            CreatedAt = now, UpdatedAt = now, Version = 1
        });
        await context.SaveChangesAsync();

        var audit = new RecordingAuditLogService();
        var handler = new CreateAssetHandler(context, new FakeCurrentUser(), audit);

        var response = await handler.Handle(
            new CreateAssetCommand("TEC-AUDIT-0011", 5, null, null, 7, null, "host-a", null),
            CancellationToken.None);

        var entry = Assert.Single(audit.Entries);
        Assert.Equal("assets", entry.TableName);
        Assert.Equal(response.AssetId, entry.RecordId);
        Assert.Equal("INSERT", entry.Action);
        Assert.Null(entry.OldData);
        Assert.NotNull(entry.NewData);
    }

    // ------------------------------------------------------------- Locations

    private static (TecnmInventario.Api.Data.TecnmInventarioDbContext Context,
        UpdateLocationHandler Handler,
        RecordingAuditLogService Audit,
        SaveChangesCounter Counter,
        Location Location) CreateLocationHarness()
    {
        var counter = new SaveChangesCounter();
        var context = TestDb.Create(counter);
        var now = DateTime.UtcNow;

        var location = new Location
        {
            LocationId = 20,
            Code = "AUD-LAB",
            Name = "Laboratorio de auditoría",
            LocationTypeId = 2,
            CreatedAt = now,
            UpdatedAt = now,
            Version = 1
        };
        context.Locations.Add(location);
        context.SaveChanges();
        counter.Reset();

        var audit = new RecordingAuditLogService();
        var handler = new UpdateLocationHandler(context, new FakeCurrentUser(), audit);
        return (context, handler, audit, counter, location);
    }

    [Fact]
    public async Task UpdateLocation_identico_no_audita_ni_guarda_ni_sube_version()
    {
        var (context, handler, audit, counter, location) = CreateLocationHarness();
        using var _ = context;

        var response = await handler.Handle(
            new UpdateLocationCommand(20, 1, "Laboratorio de auditoría", 2, null),
            CancellationToken.None);

        Assert.Empty(audit.Entries);
        Assert.Equal(0, counter.Count);
        Assert.Null(location.UpdatedBy);
        Assert.Equal(1, response.Version);
    }

    [Fact]
    public async Task UpdateLocation_version_vieja_con_datos_identicos_devuelve_conflicto()
    {
        var (context, handler, audit, counter, _) = CreateLocationHarness();
        using var __ = context;

        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => handler.Handle(
            new UpdateLocationCommand(20, 99, "Laboratorio de auditoría", 2, null),
            CancellationToken.None).AsTask());

        Assert.Empty(audit.Entries);
        Assert.Equal(0, counter.Count);
    }

    [Fact]
    public async Task UpdateLocation_con_cambios_audita_UPDATE()
    {
        var (context, handler, audit, _, location) = CreateLocationHarness();
        using var __ = context;

        await handler.Handle(
            new UpdateLocationCommand(20, 1, "Laboratorio renombrado", 2, null),
            CancellationToken.None);

        var entry = Assert.Single(audit.Entries);
        Assert.Equal("locations", entry.TableName);
        Assert.Equal(20, entry.RecordId);
        Assert.Equal("UPDATE", entry.Action);
        Assert.NotNull(entry.OldData);
        Assert.NotNull(entry.NewData);
        Assert.Equal(1, location.UpdatedBy);
        Assert.Equal("Laboratorio renombrado", location.Name);
    }

    [Fact]
    public async Task CreateLocation_audita_INSERT_con_oldData_nulo()
    {
        var context = TestDb.Create();
        using var _ = context;
        var audit = new RecordingAuditLogService();
        var handler = new CreateLocationHandler(context, new FakeCurrentUser(), audit);

        var response = await handler.Handle(
            new CreateLocationCommand("AUD-LAB-2", "Laboratorio 2", 2, null),
            CancellationToken.None);

        var entry = Assert.Single(audit.Entries);
        Assert.Equal("locations", entry.TableName);
        Assert.Equal(response.LocationId, entry.RecordId);
        Assert.Equal("INSERT", entry.Action);
        Assert.Null(entry.OldData);
        Assert.NotNull(entry.NewData);
    }

    // ------------------------------------------------------------- Suppliers

    private static (TecnmInventario.Api.Data.TecnmInventarioDbContext Context,
        UpdateSupplierHandler Handler,
        RecordingAuditLogService Audit,
        SaveChangesCounter Counter,
        Supplier Supplier) CreateSupplierHarness()
    {
        var counter = new SaveChangesCounter();
        var context = TestDb.Create(counter);
        var now = DateTime.UtcNow;

        var supplier = new Supplier
        {
            SupplierId = 30,
            Name = "Proveedor de auditoría",
            Email = "proveedor@example.com",
            CreatedAt = now,
            UpdatedAt = now,
            Version = 1
        };
        context.Suppliers.Add(supplier);
        context.SaveChanges();
        counter.Reset();

        var audit = new RecordingAuditLogService();
        var handler = new UpdateSupplierHandler(context, new FakeCurrentUser(), audit);
        return (context, handler, audit, counter, supplier);
    }

    [Fact]
    public async Task UpdateSupplier_identico_no_audita_ni_guarda_ni_sube_version()
    {
        var (context, handler, audit, counter, supplier) = CreateSupplierHarness();
        using var _ = context;

        var response = await handler.Handle(
            new UpdateSupplierCommand(
                30, 1, "Proveedor de auditoría", null, null, "proveedor@example.com",
                null, null, null),
            CancellationToken.None);

        Assert.Empty(audit.Entries);
        Assert.Equal(0, counter.Count);
        Assert.Null(supplier.UpdatedBy);
        Assert.Equal(1, response.Version);
    }

    [Fact]
    public async Task UpdateSupplier_version_vieja_con_datos_identicos_devuelve_conflicto()
    {
        var (context, handler, audit, counter, _) = CreateSupplierHarness();
        using var __ = context;

        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => handler.Handle(
            new UpdateSupplierCommand(
                30, 99, "Proveedor de auditoría", null, null, "proveedor@example.com",
                null, null, null),
            CancellationToken.None).AsTask());

        Assert.Empty(audit.Entries);
        Assert.Equal(0, counter.Count);
    }

    [Fact]
    public async Task UpdateSupplier_con_cambios_audita_UPDATE()
    {
        var (context, handler, audit, _, supplier) = CreateSupplierHarness();
        using var __ = context;

        await handler.Handle(
            new UpdateSupplierCommand(
                30, 1, "Proveedor de auditoría", null, null, "proveedor@example.com",
                "555-0100", null, null),
            CancellationToken.None);

        var entry = Assert.Single(audit.Entries);
        Assert.Equal("suppliers", entry.TableName);
        Assert.Equal(30, entry.RecordId);
        Assert.Equal("UPDATE", entry.Action);
        Assert.NotNull(entry.OldData);
        Assert.NotNull(entry.NewData);
        Assert.Equal(1, supplier.UpdatedBy);
        Assert.Equal("555-0100", supplier.Phone);
    }

    [Fact]
    public async Task CreateSupplier_audita_INSERT_con_oldData_nulo()
    {
        var context = TestDb.Create();
        using var _ = context;
        var audit = new RecordingAuditLogService();
        var handler = new CreateSupplierHandler(context, new FakeCurrentUser(), audit);

        var response = await handler.Handle(
            new CreateSupplierCommand("Proveedor nuevo", "RFC-1", null, null, null, null, null),
            CancellationToken.None);

        var entry = Assert.Single(audit.Entries);
        Assert.Equal("suppliers", entry.TableName);
        Assert.Equal(response.SupplierId, entry.RecordId);
        Assert.Equal("INSERT", entry.Action);
        Assert.Null(entry.OldData);
        Assert.NotNull(entry.NewData);
    }
}
