using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Npgsql;
using TecnmInventario.Api.Core.Contracts;
using TecnmInventario.Api.Core.Entities;

namespace TecnmInventario.Api.Tests.Integration;

/// <summary>
/// M.1: el modelo EF de MaintenanceRecord frente al esquema real. Las propiedades
/// nuevas viven en Core/Entities/Extensions (partial) y su mapeo en
/// TecnmInventarioDbContext.Custom.cs; el archivo generado no se toca (regla 8).
/// </summary>
[Trait("Category", "Integration")]
public sealed class MaintenanceModelTests : PostgresTestBase
{
    public MaintenanceModelTests(PostgresContainerFixture fixture)
        : base(fixture) { }

    // ── A6 ─────────────────────────────────────────────────────────────────

    [DockerFact]
    public async Task ModeloEf_LeeYEscribeTodasLasColumnasNuevas_IdaYVuelta()
    {
        await using var db = await CreateDatabaseAsync();
        var assetId = await MaintenanceSql.InsertAssetAsync(db, "M1-MODELO");
        var typeId = await MaintenanceSql.TypeIdAsync(db);
        var userId = await db.ScalarAsync<long>("SELECT min(user_id) FROM inventario.app_users");
        var incidentId = await InsertIncidentAsync(db, assetId, userId);
        var beforeId = await StatusIdAsync(db, "DISPONIBLE");
        var afterId = await StatusIdAsync(db, "EN_MANTENIMIENTO");

        var startedAt = new DateTime(2026, 5, 1, 9, 30, 0, DateTimeKind.Utc);
        var cancelledAt = new DateTime(2026, 5, 1, 11, 0, 0, DateTimeKind.Utc);

        long recordId;
        await using (var writer = db.CreateContext())
        {
            var record = new MaintenanceRecord
            {
                AssetId = assetId,
                MaintenanceTypeId = typeId,
                Status = "CANCELADO",
                StartedAt = startedAt,
                CancelledAt = cancelledAt,
                CancellationReason = "Se canceló por falta de refacciones.",
                Cost = 1234.56m,
                IncidentId = incidentId,
                StatusBeforeId = beforeId,
                StatusAfterId = afterId,
                PerformedBy = userId,
                Description = "Revisión ficticia",
                PartsUsed = "Pasta térmica"
            };
            writer.MaintenanceRecords.Add(record);
            await writer.SaveChangesAsync();
            recordId = record.MaintenanceRecordId;
        }

        await using (var reader = db.CreateContext())
        {
            var loaded = await reader.MaintenanceRecords
                .Include(m => m.Incident)
                .Include(m => m.StatusBefore)
                .Include(m => m.StatusAfter)
                .AsNoTracking()
                .SingleAsync(m => m.MaintenanceRecordId == recordId);

            Assert.Equal("CANCELADO", loaded.Status);
            Assert.Equal(startedAt, loaded.StartedAt?.ToUniversalTime());
            Assert.Equal(cancelledAt, loaded.CancelledAt?.ToUniversalTime());
            Assert.Equal("Se canceló por falta de refacciones.", loaded.CancellationReason);
            Assert.Equal(1234.56m, loaded.Cost);
            Assert.Equal(incidentId, loaded.IncidentId);
            Assert.Equal(incidentId, loaded.Incident?.IncidentId);
            Assert.Equal(beforeId, loaded.StatusBeforeId);
            Assert.Equal("DISPONIBLE", loaded.StatusBefore?.Code);
            Assert.Equal(afterId, loaded.StatusAfterId);
            Assert.Equal("EN_MANTENIMIENTO", loaded.StatusAfter?.Code);
            Assert.Equal(1, loaded.Version);
        }

        // Actualización por EF: el trigger incrementa la versión y los valores nuevos persisten.
        await using (var updater = db.CreateContext())
        {
            var tracked = await updater.MaintenanceRecords.SingleAsync(m => m.MaintenanceRecordId == recordId);
            tracked.Cost = 99.90m;
            tracked.CancellationReason = "Motivo corregido.";
            tracked.IncidentId = null;
            await updater.SaveChangesAsync();
        }

        await using (var reread = db.CreateContext())
        {
            var loaded = await reread.MaintenanceRecords.AsNoTracking()
                .SingleAsync(m => m.MaintenanceRecordId == recordId);
            Assert.Equal(99.90m, loaded.Cost);
            Assert.Equal("Motivo corregido.", loaded.CancellationReason);
            Assert.Null(loaded.IncidentId);
            Assert.Equal(2, loaded.Version);
        }
    }

    // Sin Status explícito, la BD pone su valor por defecto (PROGRAMADO).
    [DockerFact]
    public async Task ModeloEf_SinStatusExplicito_UsaElDefaultDeLaBd()
    {
        await using var db = await CreateDatabaseAsync();
        var assetId = await MaintenanceSql.InsertAssetAsync(db, "M1-DEFAULT");
        var typeId = await MaintenanceSql.TypeIdAsync(db);

        long id;
        await using (var writer = db.CreateContext())
        {
            var record = new MaintenanceRecord
            {
                AssetId = assetId,
                MaintenanceTypeId = typeId,
                ScheduledAt = new DateTime(2026, 9, 1, 8, 0, 0, DateTimeKind.Utc)
            };
            writer.MaintenanceRecords.Add(record);
            await writer.SaveChangesAsync();
            id = record.MaintenanceRecordId;
        }

        Assert.Equal("PROGRAMADO", await db.ScalarAsync<string>(
            $"SELECT status FROM inventario.maintenance_records WHERE maintenance_record_id = {id}"));
    }

    // ── A7 ─────────────────────────────────────────────────────────────────

    [DockerFact]
    public async Task ModeloVsEsquema_CadaPropiedadMapeadaExisteComoColumnaCompatible()
    {
        await using var db = await CreateDatabaseAsync();
        await using var context = db.CreateContext();
        var entity = context.Model.FindEntityType(typeof(MaintenanceRecord))!;
        var table = StoreObjectIdentifier.Table("maintenance_records", "inventario");

        var columns = await ReadColumnsAsync(db);
        var problems = new List<string>();
        var mapped = new HashSet<string>(StringComparer.Ordinal);

        foreach (var property in entity.GetProperties())
        {
            var name = property.GetColumnName(table);
            if (name is null)
            {
                problems.Add($"{property.Name}: sin columna mapeada");
                continue;
            }

            mapped.Add(name);
            if (!columns.TryGetValue(name, out var column))
            {
                problems.Add($"{property.Name}: la columna '{name}' no existe en la tabla");
                continue;
            }

            var clr = Nullable.GetUnderlyingType(property.ClrType) ?? property.ClrType;
            var expectedTypes = ExpectedDataTypes(clr);
            if (!expectedTypes.Contains(column.DataType))
                problems.Add($"{property.Name}: tipo CLR {clr.Name} vs columna '{column.DataType}'");

            if (property.IsNullable != column.IsNullable)
                problems.Add($"{property.Name}: nulabilidad del modelo ({property.IsNullable}) vs columna ({column.IsNullable})");

            if (property.GetMaxLength() is { } maxLength && column.MaxLength is { } columnLength && maxLength != columnLength)
                problems.Add($"{property.Name}: longitud {maxLength} vs columna {columnLength}");

            if (clr == typeof(decimal))
            {
                var precision = property.GetPrecision();
                var scale = property.GetScale();
                if (precision != column.NumericPrecision || scale != column.NumericScale)
                    problems.Add($"{property.Name}: precisión ({precision},{scale}) vs columna ({column.NumericPrecision},{column.NumericScale})");
            }
        }

        // Sentido inverso: una columna nueva sin propiedad mapeada también es un desfase.
        foreach (var column in columns.Keys.Where(c => !mapped.Contains(c)))
            problems.Add($"columna '{column}' sin propiedad mapeada en MaintenanceRecord");

        Assert.True(problems.Count == 0, "Desfases modelo/esquema: " + string.Join("; ", problems));
    }

    // MaintenanceRecord ya cumple el contrato de borrado lógico (para ISoftDeleteAuditor).
    [Fact]
    public void MaintenanceRecord_EsISoftDeletable()
        => Assert.True(typeof(ISoftDeletable).IsAssignableFrom(typeof(MaintenanceRecord)));

    // ── Soportes ───────────────────────────────────────────────────────────

    private sealed record ColumnInfo(string DataType, bool IsNullable, int? MaxLength, int? NumericPrecision, int? NumericScale);

    private static async Task<Dictionary<string, ColumnInfo>> ReadColumnsAsync(PostgresDatabase db)
    {
        await using var connection = await db.OpenConnectionAsync();
        await using var command = new NpgsqlCommand("""
            SELECT column_name, data_type, is_nullable, character_maximum_length, numeric_precision, numeric_scale
            FROM information_schema.columns
            WHERE table_schema = 'inventario' AND table_name = 'maintenance_records'
            """, connection);
        var result = new Dictionary<string, ColumnInfo>(StringComparer.Ordinal);
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            result[reader.GetString(0)] = new ColumnInfo(
                reader.GetString(1),
                reader.GetString(2) == "YES",
                reader.IsDBNull(3) ? null : reader.GetInt32(3),
                reader.IsDBNull(4) ? null : reader.GetInt32(4),
                reader.IsDBNull(5) ? null : reader.GetInt32(5));
        }

        return result;
    }

    private static HashSet<string> ExpectedDataTypes(Type clr)
    {
        if (clr == typeof(string)) return ["character varying", "text", "character"];
        if (clr == typeof(long)) return ["bigint"];
        if (clr == typeof(int)) return ["integer"];
        if (clr == typeof(bool)) return ["boolean"];
        if (clr == typeof(DateTime)) return ["timestamp with time zone"];
        if (clr == typeof(decimal)) return ["numeric"];
        return [];
    }

    private static Task<long> StatusIdAsync(PostgresDatabase db, string code)
        => db.ScalarAsync<long>($"SELECT asset_status_id FROM inventario.asset_statuses WHERE code = '{code}'");

    private static Task<long> InsertIncidentAsync(PostgresDatabase db, long assetId, long reportedBy)
        => db.ScalarAsync<long>($"""
            INSERT INTO inventario.incidents
                (asset_id, incident_type_id, incident_priority_id, incident_status_id, reported_by, title, created_by)
            SELECT {assetId}, it.incident_type_id, ip.incident_priority_id, ist.incident_status_id,
                   {reportedBy}, 'Falla ficticia M.1', {reportedBy}
            FROM inventario.incident_types it, inventario.incident_priorities ip, inventario.incident_statuses ist
            WHERE it.code = 'HARDWARE' AND ip.code = 'ALTA' AND ist.code = 'ABIERTO'
            RETURNING incident_id
            """);
}
