using System.Text.Json;
using TecnmInventario.Api.Features.Procurement.Acquisitions.CreateAcquisition;
using TecnmInventario.Api.Features.Procurement.Warranties.CreateAssetWarranty;

namespace TecnmInventario.Api.Tests;

/// <summary>
/// Subetapa B2: auditoría de CreateAcquisition y CreateAssetWarranty.
/// </summary>
public class AuditB2Tests
{
    [Fact]
    public async Task CreateAcquisition_audita_INSERT_con_los_activos_de_acquisition_assets()
    {
        var context = TestDb.Create();
        using var _ = context;
        var audit = new RecordingAuditLogService();
        var handler = new CreateAcquisitionHandler(context, new FakeCurrentUser(), audit);

        var response = await handler.Handle(
            new CreateAcquisitionCommand(
                SupplierId: null,
                PurchaseOrder: "PO-AUD-B2",
                InvoiceNumber: "FAC-AUD-B2",
                AcquiredAt: new DateOnly(2026, 1, 15),
                Subtotal: 1000m,
                TaxAmount: 160m,
                TotalAmount: 1160m,
                CurrencyCode: "mxn",
                WarrantyStart: null,
                WarrantyEnd: null,
                Notes: "compra B2",
                Assets: new[]
                {
                    new AcquisitionAssetInput(101, 500m),
                    new AcquisitionAssetInput(102, 500m)
                }),
            CancellationToken.None);

        var entry = Assert.Single(audit.Entries);
        Assert.Equal("acquisitions", entry.TableName);
        Assert.Equal(response.AcquisitionId, entry.RecordId);
        Assert.Equal("INSERT", entry.Action);
        Assert.Null(entry.OldData);

        var json = JsonSerializer.Serialize(entry.NewData);
        Assert.Contains("\"AssetId\":101", json, StringComparison.Ordinal);
        Assert.Contains("\"AssetId\":102", json, StringComparison.Ordinal);
        Assert.Contains("500", json, StringComparison.Ordinal);
        Assert.Contains("\"CurrencyCode\":\"MXN\"", json, StringComparison.Ordinal);
        Assert.Equal(2, response.AssetCount);
    }

    [Fact]
    public async Task CreateAssetWarranty_audita_INSERT_con_oldData_nulo()
    {
        var context = TestDb.Create();
        using var _ = context;
        var audit = new RecordingAuditLogService();
        var handler = new CreateAssetWarrantyHandler(context, new FakeCurrentUser(), audit);

        var response = await handler.Handle(
            new CreateAssetWarrantyCommand(
                AssetId: 101,
                SupplierId: null,
                WarrantyNumber: "GAR-AUD-B2",
                StartsOn: new DateOnly(2026, 1, 1),
                EndsOn: new DateOnly(2027, 1, 1),
                Coverage: "Cobertura B2",
                Terms: "Terminos B2"),
            CancellationToken.None);

        var entry = Assert.Single(audit.Entries);
        Assert.Equal("asset_warranties", entry.TableName);
        Assert.Equal(response.WarrantyId, entry.RecordId);
        Assert.Equal("INSERT", entry.Action);
        Assert.Null(entry.OldData);

        var json = JsonSerializer.Serialize(entry.NewData);
        Assert.Contains("\"AssetId\":101", json, StringComparison.Ordinal);
        Assert.Contains("GAR-AUD-B2", json, StringComparison.Ordinal);
    }
}
