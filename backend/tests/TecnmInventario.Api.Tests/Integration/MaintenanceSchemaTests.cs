using Npgsql;

namespace TecnmInventario.Api.Tests.Integration;

// ═══════════════════════════════════════════════════════════════════════════
// M.1: esquema de maintenance_records (scripts/add-maintenance-workflow.sql).
// Contra el PostgreSQL efímero; datos ficticios.
//
// La plantilla de Testcontainers YA aplica el script, así que el relleno de
// datos antiguos (A2) se prueba en una BD clonada a la que primero se le
// quitan las columnas nuevas (con ellas se van sus CHECK e índices), se
// insertan filas "antiguas" y luego se aplica el script desde su archivo.
// ═══════════════════════════════════════════════════════════════════════════

[Trait("Category", "Integration")]
public sealed class MaintenanceSchemaTests : PostgresTestBase
{
    private const string ScriptName = "add-maintenance-workflow.sql";

    public MaintenanceSchemaTests(PostgresContainerFixture fixture)
        : base(fixture) { }

    // ── A1 ─────────────────────────────────────────────────────────────────

    [DockerFact]
    public async Task Script_EsIdempotente_YNoReescribeEstados()
    {
        await using var db = await CreateDatabaseAsync();
        var sql = await ReadScriptAsync();

        // La plantilla ya lo aplicó una vez: son la segunda y la tercera ejecución.
        await db.ExecuteAsync(sql);

        // Un estado ya guardado NO debe recalcularse en una nueva ejecución
        // (el relleno solo aplica al migrar filas antiguas): EN_CURSO tiene
        // performed_at NULL y un relleno ciego lo volvería PROGRAMADO.
        var assetId = await MaintenanceSql.InsertAssetAsync(db, "M1-IDEM");
        var typeId = await MaintenanceSql.TypeIdAsync(db);
        var recordId = await db.ScalarAsync<long>($"""
            INSERT INTO inventario.maintenance_records (asset_id, maintenance_type_id, status, started_at)
            VALUES ({assetId}, {typeId}, 'EN_CURSO', now())
            RETURNING maintenance_record_id
            """);

        await db.ExecuteAsync(sql);

        Assert.Equal("EN_CURSO", await db.ScalarAsync<string>(
            $"SELECT status FROM inventario.maintenance_records WHERE maintenance_record_id = {recordId}"));
        Assert.Equal(2, await db.ScalarAsync<long>("""
            SELECT count(*) FROM pg_indexes
            WHERE schemaname = 'inventario' AND tablename = 'maintenance_records'
              AND indexname IN ('ux_maintenance_one_in_progress', 'ix_maintenance_status_scheduled')
            """));
        Assert.Equal(7, await db.ScalarAsync<long>("""
            SELECT count(*) FROM pg_constraint
            WHERE conrelid = 'inventario.maintenance_records'::regclass AND conname LIKE 'ck\_maintenance\_records\_%'
            """));
    }

    // ── A2 ─────────────────────────────────────────────────────────────────

    [DockerFact]
    public async Task Relleno_DeFilasAntiguas_AsignaStatusYFechas()
    {
        await using var db = await CreateDatabaseAsync();
        var sql = await ReadScriptAsync();

        // Estado "anterior al script": se quitan las columnas nuevas (sus CHECK
        // e índices desaparecen con ellas) y se insertan filas antiguas.
        await db.ExecuteAsync("""
            ALTER TABLE inventario.maintenance_records
              DROP COLUMN IF EXISTS status,
              DROP COLUMN IF EXISTS started_at,
              DROP COLUMN IF EXISTS incident_id,
              DROP COLUMN IF EXISTS cost,
              DROP COLUMN IF EXISTS status_before_id,
              DROP COLUMN IF EXISTS status_after_id,
              DROP COLUMN IF EXISTS cancelled_at,
              DROP COLUMN IF EXISTS cancellation_reason
            """);

        var assetId = await MaintenanceSql.InsertAssetAsync(db, "M1-RELLENO");
        var typeId = await MaintenanceSql.TypeIdAsync(db);

        // 1 realizado SIN scheduled_at; 2 solo programado; 3 sin fechas;
        // 4 sin fechas y borrado lógicamente; 5 realizado CON scheduled_at.
        var realizado = await InsertLegacyAsync(db, assetId, typeId, "scheduled_at, performed_at, created_at",
            "NULL, '2026-03-10 10:00:00+00', '2026-03-01 00:00:00+00'");
        var programado = await InsertLegacyAsync(db, assetId, typeId, "scheduled_at, performed_at, created_at",
            "'2026-12-01 09:00:00+00', NULL, '2026-03-02 00:00:00+00'");
        var sinFechas = await InsertLegacyAsync(db, assetId, typeId, "created_at",
            "'2026-02-01 12:00:00+00'");
        var borrado = await InsertLegacyAsync(db, assetId, typeId, "created_at, is_deleted, deleted_at",
            "'2026-01-15 00:00:00+00', true, '2026-01-20 00:00:00+00'");
        var realizadoConPlan = await InsertLegacyAsync(db, assetId, typeId, "scheduled_at, performed_at, created_at",
            "'2026-04-01 08:00:00+00', '2026-04-02 08:00:00+00', '2026-03-20 00:00:00+00'");

        await db.ExecuteAsync(sql);

        async Task<(string Status, bool StartedEqPerformed, bool StartedNull, bool ScheduledNull,
            bool ScheduledEqCreated, bool IsDeleted)> Row(long id)
        {
            await using var connection = await db.OpenConnectionAsync();
            await using var command = new NpgsqlCommand($"""
                SELECT status, started_at = performed_at, started_at IS NULL, scheduled_at IS NULL,
                       scheduled_at = created_at, is_deleted
                FROM inventario.maintenance_records WHERE maintenance_record_id = {id}
                """, connection);
            await using var reader = await command.ExecuteReaderAsync();
            Assert.True(await reader.ReadAsync());
            return (reader.GetString(0),
                !reader.IsDBNull(1) && reader.GetBoolean(1),
                reader.GetBoolean(2), reader.GetBoolean(3),
                !reader.IsDBNull(4) && reader.GetBoolean(4), reader.GetBoolean(5));
        }

        // Realizado: COMPLETADO; no se conoce el inicio real, started_at = performed_at.
        var r1 = await Row(realizado);
        Assert.Equal("COMPLETADO", r1.Status);
        Assert.True(r1.StartedEqPerformed);
        Assert.True(r1.ScheduledNull); // no se inventa scheduled_at si hay performed_at

        // Solo programado: PROGRAMADO, fecha intacta, sin started_at.
        var r2 = await Row(programado);
        Assert.Equal("PROGRAMADO", r2.Status);
        Assert.True(r2.StartedNull);
        Assert.False(r2.ScheduledNull);

        // Sin fechas: PROGRAMADO con scheduled_at = created_at.
        var r3 = await Row(sinFechas);
        Assert.Equal("PROGRAMADO", r3.Status);
        Assert.True(r3.ScheduledEqCreated);

        // Borrado lógicamente: también se rellena (los CHECK no distinguen borrados) y sigue borrado.
        var r4 = await Row(borrado);
        Assert.Equal("PROGRAMADO", r4.Status);
        Assert.True(r4.ScheduledEqCreated);
        Assert.True(r4.IsDeleted);

        // Realizado con plan: COMPLETADO y conserva su scheduled_at.
        var r5 = await Row(realizadoConPlan);
        Assert.Equal("COMPLETADO", r5.Status);
        Assert.True(r5.StartedEqPerformed);
        Assert.False(r5.ScheduledNull);
    }

    private static Task<long> InsertLegacyAsync(
        PostgresDatabase db, long assetId, long typeId, string columns, string values)
        => db.ScalarAsync<long>($"""
            INSERT INTO inventario.maintenance_records (asset_id, maintenance_type_id, {columns})
            VALUES ({assetId}, {typeId}, {values})
            RETURNING maintenance_record_id
            """);

    // ── A3 ─────────────────────────────────────────────────────────────────

    [DockerTheory]
    //          status         scheduled                      started                        performed                      cancelled                      cost   constraint
    [InlineData("EN_CURSO",    "NULL",                        "NULL",                        "NULL",                        "NULL",                        "NULL", "ck_maintenance_records_en_curso")]
    [InlineData("COMPLETADO",  "NULL",                        "'2026-01-01 10:00:00+00'",    "NULL",                        "NULL",                        "NULL", "ck_maintenance_records_completado")]
    [InlineData("COMPLETADO",  "NULL",                        "NULL",                        "'2026-01-02 10:00:00+00'",    "NULL",                        "NULL", "ck_maintenance_records_completado")]
    [InlineData("CANCELADO",   "NULL",                        "NULL",                        "NULL",                        "NULL",                        "NULL", "ck_maintenance_records_cancelado")]
    [InlineData("PROGRAMADO",  "NULL",                        "NULL",                        "NULL",                        "NULL",                        "NULL", "ck_maintenance_records_programado")]
    [InlineData("PROGRAMADO",  "'2026-06-01 10:00:00+00'",    "NULL",                        "NULL",                        "NULL",                        "-1",   "ck_maintenance_records_cost")]
    [InlineData("ABIERTO",     "'2026-06-01 10:00:00+00'",    "NULL",                        "NULL",                        "NULL",                        "NULL", "ck_maintenance_records_status")]
    [InlineData("PROGRAMADO",  "'2026-06-01 10:00:00+00'",    "'2026-02-02 10:00:00+00'",    "'2026-02-01 10:00:00+00'",    "NULL",                        "NULL", "ck_maintenance_records_fechas")]
    public async Task Checks_Rechazan23514_ConSuNombre(
        string status, string scheduled, string started, string performed, string cancelled, string cost, string constraint)
    {
        await using var db = await CreateDatabaseAsync();
        var assetId = await MaintenanceSql.InsertAssetAsync(db, "M1-CHECK");
        var typeId = await MaintenanceSql.TypeIdAsync(db);

        var ex = await Assert.ThrowsAsync<PostgresException>(() => db.ExecuteAsync($"""
            INSERT INTO inventario.maintenance_records
                (asset_id, maintenance_type_id, status, scheduled_at, started_at, performed_at, cancelled_at, cost)
            VALUES ({assetId}, {typeId}, '{status}', {scheduled}, {started}, {performed}, {cancelled}, {cost})
            """));

        Assert.Equal("23514", ex.SqlState);
        Assert.Equal(constraint, ex.ConstraintName);
    }

    // Guardia: cada estado válido, con sus campos obligatorios, se acepta.
    [DockerFact]
    public async Task Checks_AceptanFilasValidasDeLosCuatroEstados()
    {
        await using var db = await CreateDatabaseAsync();
        var typeId = await MaintenanceSql.TypeIdAsync(db);
        var assetId = await MaintenanceSql.InsertAssetAsync(db, "M1-VALIDO");

        await db.ExecuteAsync($"""
            INSERT INTO inventario.maintenance_records
                (asset_id, maintenance_type_id, status, scheduled_at, started_at, performed_at, cancelled_at, cost)
            VALUES
                ({assetId}, {typeId}, 'PROGRAMADO',  '2026-06-01 10:00:00+00', NULL, NULL, NULL, 0),
                ({assetId}, {typeId}, 'EN_CURSO',    NULL, '2026-06-02 10:00:00+00', NULL, NULL, NULL),
                ({assetId}, {typeId}, 'COMPLETADO',  NULL, '2026-06-03 10:00:00+00', '2026-06-03 10:00:00+00', NULL, 1500.50),
                ({assetId}, {typeId}, 'CANCELADO',   NULL, NULL, NULL, '2026-06-04 10:00:00+00', NULL)
            """);

        Assert.Equal(4, await db.CountAsync("maintenance_records", $"asset_id = {assetId}"));
    }

    // ── A4 ─────────────────────────────────────────────────────────────────

    [DockerFact]
    public async Task UnSoloEnCurso_DosEnCursoDelMismoActivo_Falla23505()
    {
        await using var db = await CreateDatabaseAsync();
        var assetId = await MaintenanceSql.InsertAssetAsync(db, "M1-UNICO");
        var typeId = await MaintenanceSql.TypeIdAsync(db);
        await MaintenanceSql.InsertInProgressAsync(db, assetId, typeId);

        var ex = await Assert.ThrowsAsync<PostgresException>(
            () => MaintenanceSql.InsertInProgressAsync(db, assetId, typeId));

        Assert.Equal("23505", ex.SqlState);
        Assert.Equal("ux_maintenance_one_in_progress", ex.ConstraintName);
    }

    [DockerFact]
    public async Task UnSoloEnCurso_UnoBorradoLogicamenteNoCuenta()
    {
        await using var db = await CreateDatabaseAsync();
        var assetId = await MaintenanceSql.InsertAssetAsync(db, "M1-BORRADO");
        var otherAssetId = await MaintenanceSql.InsertAssetAsync(db, "M1-OTRO");
        var typeId = await MaintenanceSql.TypeIdAsync(db);

        // Uno EN_CURSO borrado (creado por error) no bloquea a otro EN_CURSO del mismo activo.
        await MaintenanceSql.InsertInProgressAsync(db, assetId, typeId, isDeleted: true);
        await MaintenanceSql.InsertInProgressAsync(db, assetId, typeId);

        // Otro activo puede tener el suyo; los estados no EN_CURSO no cuentan.
        await MaintenanceSql.InsertInProgressAsync(db, otherAssetId, typeId);
        await db.ExecuteAsync($"""
            INSERT INTO inventario.maintenance_records (asset_id, maintenance_type_id, status, scheduled_at)
            VALUES ({assetId}, {typeId}, 'PROGRAMADO', now()), ({assetId}, {typeId}, 'PROGRAMADO', now())
            """);

        Assert.Equal(1, await db.CountAsync("maintenance_records",
            $"asset_id = {assetId} AND status = 'EN_CURSO' AND NOT is_deleted"));
    }

    // ── Soportes ───────────────────────────────────────────────────────────

    private static async Task<string> ReadScriptAsync()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Sql", ScriptName);
        Assert.True(File.Exists(path), $"No existe el script enlazado en el proyecto de tests: {path}");
        return await File.ReadAllTextAsync(path);
    }
}

/// <summary>SQL de apoyo para los tests de mantenimiento (datos ficticios).</summary>
internal static class MaintenanceSql
{
    public static Task<long> TypeIdAsync(PostgresDatabase db)
        => db.ScalarAsync<long>(
            "SELECT maintenance_type_id FROM inventario.maintenance_types WHERE code = 'PREVENTIVO'");

    public static Task<long> InsertAssetAsync(PostgresDatabase db, string inventoryCode)
        => db.ScalarAsync<long>($"""
            INSERT INTO inventario.assets (inventory_code, asset_type_id, asset_status_id)
            SELECT '{inventoryCode}', at.asset_type_id, s.asset_status_id
            FROM inventario.asset_types at
            CROSS JOIN inventario.asset_statuses s
            WHERE at.code = 'PC' AND s.code = 'DISPONIBLE'
            RETURNING asset_id
            """);

    public static Task InsertInProgressAsync(PostgresDatabase db, long assetId, long typeId, bool isDeleted = false)
        => db.ExecuteAsync($"""
            INSERT INTO inventario.maintenance_records
                (asset_id, maintenance_type_id, status, started_at, is_deleted)
            VALUES ({assetId}, {typeId}, 'EN_CURSO', now(), {(isDeleted ? "true" : "false")})
            """);
}
