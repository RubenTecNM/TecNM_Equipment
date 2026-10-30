using Npgsql;
using TecnmInventario.Api.Common.Domain;

namespace TecnmInventario.Api.Tests.Integration;

/// <summary>
/// Comprueba que las constantes de longitud usadas por los validadores
/// coincidan con las columnas reales del esquema inventario. Usa la base
/// PostgreSQL efímera del fixture: ya no depende de database/.env ni de la
/// base de desarrollo.
/// </summary>
[Trait("Category", "Integration")]
public sealed class DbFieldLengthsTests : PostgresTestBase
{
    private static readonly Dictionary<(string Table, string Column), int> Expected = new()
    {
        [("assets", "inventory_code")] = DbFieldLengths.Assets.InventoryCode,
        [("assets", "serial_number")] = DbFieldLengths.Assets.SerialNumber,
        [("assets", "hostname")] = DbFieldLengths.Assets.Hostname,
        [("roles", "code")] = DbFieldLengths.Roles.Code,
        [("roles", "name")] = DbFieldLengths.Roles.Name,
        [("app_users", "username")] = DbFieldLengths.Users.Username,
        [("app_users", "email")] = DbFieldLengths.Users.Email,
        [("app_users", "first_name")] = DbFieldLengths.Users.FirstName,
        [("app_users", "last_name")] = DbFieldLengths.Users.LastName,
        [("acquisitions", "purchase_order")] = DbFieldLengths.Acquisitions.PurchaseOrder,
        [("acquisitions", "invoice_number")] = DbFieldLengths.Acquisitions.InvoiceNumber,
        [("acquisitions", "currency_code")] = DbFieldLengths.Acquisitions.CurrencyCode,
        [("documents", "file_name")] = DbFieldLengths.Documents.FileName,
        [("documents", "mime_type")] = DbFieldLengths.Documents.MimeType,
        [("suppliers", "name")] = DbFieldLengths.Suppliers.Name,
        [("suppliers", "tax_id")] = DbFieldLengths.Suppliers.TaxId,
        [("suppliers", "contact_name")] = DbFieldLengths.Suppliers.ContactName,
        [("suppliers", "email")] = DbFieldLengths.Suppliers.Email,
        [("suppliers", "phone")] = DbFieldLengths.Suppliers.Phone,
        [("asset_warranties", "warranty_number")] = DbFieldLengths.Warranties.WarrantyNumber
    };

    public DbFieldLengthsTests(PostgresContainerFixture fixture)
        : base(fixture)
    {
    }

    [DockerFact]
    public async Task Constantes_de_longitud_coinciden_con_information_schema()
    {
        await using var db = await CreateDatabaseAsync();
        await using var connection = await db.OpenConnectionAsync();

        var actual = new Dictionary<(string Table, string Column), int>();
        await using (var command = new NpgsqlCommand(
            "select table_name, column_name, character_maximum_length " +
            "from information_schema.columns where table_schema = 'inventario' " +
            "and character_maximum_length is not null", connection))
        await using (var reader = await command.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                actual[(reader.GetString(0), reader.GetString(1))] = reader.GetInt32(2);
            }
        }

        var mismatches = Expected
            .Where(pair => !actual.TryGetValue(pair.Key, out var length) || length != pair.Value)
            .Select(pair => actual.TryGetValue(pair.Key, out var length)
                ? $"{pair.Key.Table}.{pair.Key.Column}: constante={pair.Value}, BD={length}"
                : $"{pair.Key.Table}.{pair.Key.Column}: no existe en information_schema")
            .ToList();

        Assert.True(mismatches.Count == 0, string.Join(Environment.NewLine, mismatches));
    }
}
