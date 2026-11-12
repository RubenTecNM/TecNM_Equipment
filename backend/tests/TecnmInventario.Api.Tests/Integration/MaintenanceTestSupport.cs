using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace TecnmInventario.Api.Tests.Integration;

/// <summary>
/// Base de los tests HTTP del módulo de mantenimiento (M.2): API real
/// (ApiFactory) sobre el PostgreSQL efímero. Los JWT salen del login real.
/// Datos ficticios.
/// </summary>
public abstract class MaintenanceTestBase : PostgresTestBase
{
    protected MaintenanceTestBase(PostgresContainerFixture fixture)
        : base(fixture) { }

    protected async Task<MaintenanceEnv> CreateEnvAsync(
        Action<IServiceCollection>? configureTestServices = null, bool isolatedStorage = false)
        => await MaintenanceEnv.CreateAsync(await CreateDatabaseAsync(), configureTestServices, isolatedStorage);
}

public sealed class MaintenanceEnv : IAsyncDisposable
{
    public const string Password = "Mant1Correcta!";

    private readonly ApiFactory _factory;
    private int _counter;

    private MaintenanceEnv(PostgresDatabase db, ApiFactory factory, HttpClient client)
    {
        Db = db;
        _factory = factory;
        Client = client;
    }

    public PostgresDatabase Db { get; }
    public HttpClient Client { get; }
    public string AdminToken { get; private set; } = "";
    public long AdminId { get; private set; }
    public long TypeId { get; private set; }

    public string? StorageRoot { get; private set; }
    private string? _tempDir;

    public static async Task<MaintenanceEnv> CreateAsync(
        PostgresDatabase db, Action<IServiceCollection>? configureTestServices, bool isolatedStorage = false)
    {
        // Storage temporal propio del test (nunca la carpeta real de documentos).
        string? tempDir = null;
        Dictionary<string, string?>? overrides = null;
        if (isolatedStorage)
        {
            tempDir = Path.Combine(Path.GetTempPath(), "tecnm-m5-" + Guid.NewGuid().ToString("N"));
            overrides = new Dictionary<string, string?> { ["Storage:LocalPath"] = Path.Combine(tempDir, "storage") };
        }

        var factory = new ApiFactory(db.ConnectionString, overrides, configureTestServices: configureTestServices);
        var env = new MaintenanceEnv(db, factory, Fase52PerimetroTests.NewClient(factory))
        {
            _tempDir = tempDir,
            StorageRoot = tempDir is null ? null : Path.Combine(tempDir, "storage")
        };
        (env.AdminToken, env.AdminId) = await env.CreateUserAsync("m2_admin", "ADMIN");
        env.TypeId = await db.ScalarAsync<long>(
            "SELECT maintenance_type_id FROM inventario.maintenance_types WHERE code = 'PREVENTIVO'");
        return env;
    }

    // ── Datos ──────────────────────────────────────────────────────────────

    public async Task<(string Token, long UserId)> CreateUserAsync(string username, string roleCode)
    {
        var hash = BCrypt.Net.BCrypt.HashPassword(Password, 4);
        var userId = await Db.ScalarAsync<long>($"""
            INSERT INTO inventario.app_users
                (username, email, password_hash, first_name, last_name, role_id, status)
            SELECT '{username}', '{username}@test.local', '{hash}', 'Usuario', 'Mantenimiento', role_id, 'activo'
            FROM inventario.roles WHERE code = '{roleCode}'
            RETURNING user_id
            """);

        var login = await Client.PostAsJsonAsync("/api/auth/login",
            new { usernameOrEmail = username, password = Password });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var cookie = login.Headers.GetValues("Set-Cookie").First(c => c.StartsWith("tecnm_at=", StringComparison.Ordinal));
        return (cookie["tecnm_at=".Length..].Split(';')[0], userId);
    }

    public Task<long> InsertAssetAsync(string statusCode = "DISPONIBLE")
    {
        var code = $"M2-{Interlocked.Increment(ref _counter):D4}-{Guid.NewGuid().ToString("N")[..6]}";
        return Db.ScalarAsync<long>($"""
            INSERT INTO inventario.assets (inventory_code, asset_type_id, asset_status_id)
            SELECT '{code}', at.asset_type_id, s.asset_status_id
            FROM inventario.asset_types at
            CROSS JOIN inventario.asset_statuses s
            WHERE at.code = 'PC' AND s.code = '{statusCode}'
            RETURNING asset_id
            """);
    }

    public Task SetAssetStatusAsync(long assetId, string statusCode)
        => Db.ExecuteAsync($"""
            UPDATE inventario.assets
            SET asset_status_id = (SELECT asset_status_id FROM inventario.asset_statuses WHERE code = '{statusCode}')
            WHERE asset_id = {assetId}
            """);

    public Task<long> InsertIncidentAsync(long assetId, string statusCode)
        => Db.ScalarAsync<long>($"""
            INSERT INTO inventario.incidents
                (asset_id, incident_type_id, incident_priority_id, incident_status_id, reported_by, title, created_by)
            SELECT {assetId}, it.incident_type_id, ip.incident_priority_id, ist.incident_status_id,
                   {AdminId}, 'Falla ficticia M.2', {AdminId}
            FROM inventario.incident_types it, inventario.incident_priorities ip, inventario.incident_statuses ist
            WHERE it.code = 'HARDWARE' AND ip.code = 'ALTA' AND ist.code = '{statusCode}'
            RETURNING incident_id
            """);

    public Task<long> InsertAssignmentAsync(long assetId)
        => Db.ScalarAsync<long>($"""
            INSERT INTO inventario.asset_assignments (asset_id, assigned_to, assignment_type_id)
            SELECT {assetId}, {AdminId}, assignment_type_id
            FROM inventario.assignment_types WHERE code = 'PERSONAL'
            RETURNING asset_assignment_id
            """);

    /// <summary>Inserta un registro PROGRAMADO por SQL (sin pasar por la API).</summary>
    public Task<long> InsertScheduledAsync(long assetId)
        => Db.ScalarAsync<long>($"""
            INSERT INTO inventario.maintenance_records (asset_id, maintenance_type_id, status, scheduled_at, created_by)
            VALUES ({assetId}, {TypeId}, 'PROGRAMADO', now() + interval '7 days', {AdminId})
            RETURNING maintenance_record_id
            """);

    public Task<string> AssetStatusAsync(long assetId)
        => Db.ScalarAsync<string>($"""
            SELECT s.code FROM inventario.assets a
            JOIN inventario.asset_statuses s ON s.asset_status_id = a.asset_status_id
            WHERE a.asset_id = {assetId}
            """);

    public Task<int> AssetVersionAsync(long assetId)
        => Db.ScalarAsync<int>($"SELECT version FROM inventario.assets WHERE asset_id = {assetId}");

    public Task<string> RecordStatusAsync(long recordId)
        => Db.ScalarAsync<string>($"SELECT status FROM inventario.maintenance_records WHERE maintenance_record_id = {recordId}");

    public Task<int> RecordVersionAsync(long recordId)
        => Db.ScalarAsync<int>($"SELECT version FROM inventario.maintenance_records WHERE maintenance_record_id = {recordId}");

    /// <summary>Código del estado del activo guardado en una columna FK del registro.</summary>
    public Task<string?> RecordStatusColumnAsync(long recordId, string column)
        => ScalarOrNullAsync($"""
            SELECT s.code FROM inventario.maintenance_records m
            JOIN inventario.asset_statuses s ON s.asset_status_id = m.{column}
            WHERE m.maintenance_record_id = {recordId}
            """);

    public Task<long> AuditCountAsync(string table, long recordId, string action)
        => Db.ScalarAsync<long>($"""
            SELECT count(*) FROM inventario.audit_log
            WHERE table_name = '{table}' AND record_id = {recordId} AND action = '{action}'
            """);

    public async Task<string?> ScalarOrNullAsync(string sql)
    {
        await using var connection = await Db.OpenConnectionAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        var value = await command.ExecuteScalarAsync();
        return value is null or DBNull ? null : Convert.ToString(value);
    }

    // ── HTTP ───────────────────────────────────────────────────────────────

    public Task<HttpResponseMessage> SendAsync(HttpMethod method, string path, object? body = null, string? token = null)
    {
        var request = new HttpRequestMessage(method, path);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token ?? AdminToken);
        return Client.SendAsync(request);
    }

    public Task<HttpResponseMessage> PostAsync(string path, object? body = null, string? token = null)
        => SendAsync(HttpMethod.Post, path, body ?? new { }, token);

    public Task<HttpResponseMessage> ScheduleAsync(long assetId, object? extra = null, string? token = null)
        => PostAsync("/api/maintenance", new
        {
            assetId,
            maintenanceTypeId = TypeId,
            scheduledAt = "2026-12-01T09:00:00Z",
            description = "Revisión ficticia"
        }, token);

    public Task<HttpResponseMessage> StartDirectAsync(long assetId, long? incidentId = null, long? performedBy = null, string? token = null)
        => PostAsync("/api/maintenance/start", new
        {
            assetId,
            maintenanceTypeId = TypeId,
            performedBy,
            description = "Intervención directa ficticia",
            incidentId
        }, token);

    public Task<HttpResponseMessage> StartAsync(long recordId, int expectedVersion, long? incidentId = null, string? token = null)
        => PostAsync($"/api/maintenance/{recordId}/start", new { expectedVersion, incidentId }, token);

    public Task<HttpResponseMessage> CompleteAsync(long recordId, int expectedVersion, object? extra = null, string? token = null)
        => PostAsync($"/api/maintenance/{recordId}/complete", extra ?? new { expectedVersion }, token);

    public Task<HttpResponseMessage> CancelAsync(long recordId, int expectedVersion, string? reason = "Cancelado en prueba", string? token = null)
        => PostAsync($"/api/maintenance/{recordId}/cancel", new { expectedVersion, reason }, token);

    // ── Incidentes, estado, movimientos y asignaciones por la API real (M.3) ──

    public async Task<long> CreateIncidentAsync(long assetId)
    {
        var response = await PostAsync("/api/incidents", new
        {
            assetId,
            incidentTypeId = await Db.ScalarAsync<long>("SELECT incident_type_id FROM inventario.incident_types WHERE code = 'HARDWARE'"),
            incidentPriorityId = await Db.ScalarAsync<long>("SELECT incident_priority_id FROM inventario.incident_priorities WHERE code = 'ALTA'"),
            title = "Falla ficticia M.3"
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await ReadAsync(response)).GetProperty("incidentId").GetInt64();
    }

    public Task<int> IncidentVersionAsync(long incidentId)
        => Db.ScalarAsync<int>($"SELECT version FROM inventario.incidents WHERE incident_id = {incidentId}");

    public async Task IncidentToRepairAsync(long incidentId)
    {
        var response = await SendAsync(HttpMethod.Patch,
            $"/api/incidents/{incidentId}/status?expectedVersion={await IncidentVersionAsync(incidentId)}",
            new { newStatusCode = "EN_REPARACION", reason = "En reparación (prueba)" });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    public async Task<HttpResponseMessage> ResolveIncidentAsync(long incidentId)
        => await SendAsync(HttpMethod.Post,
            $"/api/incidents/{incidentId}/resolve?expectedVersion={await IncidentVersionAsync(incidentId)}",
            new { solution = "Reparado en prueba" });

    public async Task<HttpResponseMessage> PatchStatusAsync(long assetId, string newStatusCode)
        => await SendAsync(HttpMethod.Patch,
            $"/api/assets/{assetId}/status?expectedVersion={await AssetVersionAsync(assetId)}",
            new { newStatusCode, reason = "Cambio de prueba" });

    public async Task<HttpResponseMessage> MoveAsync(long assetId)
    {
        var locationId = await Db.ScalarAsync<long>($"""
            INSERT INTO inventario.locations (code, name, location_type_id)
            SELECT 'M3-{Guid.NewGuid().ToString("N")[..8]}', 'Ubicación ficticia', location_type_id
            FROM inventario.location_types ORDER BY location_type_id LIMIT 1
            RETURNING location_id
            """);
        var reasonId = await Db.ScalarAsync<long>(
            "SELECT movement_reason_id FROM inventario.movement_reasons WHERE code = 'REUBICACION'");
        return await SendAsync(HttpMethod.Post,
            $"/api/assets/{assetId}/movements?expectedVersion={await AssetVersionAsync(assetId)}",
            new { toLocationId = locationId, movementReasonId = reasonId });
    }

    public async Task<HttpResponseMessage> AssignAsync(long assetId)
        => await SendAsync(HttpMethod.Post,
            $"/api/assets/{assetId}/assignments?expectedVersion={await AssetVersionAsync(assetId)}",
            new
            {
                assignedTo = AdminId,
                assignmentTypeId = await Db.ScalarAsync<long>(
                    "SELECT assignment_type_id FROM inventario.assignment_types WHERE code = 'PERSONAL'")
            });

    // ── Consultas (M.4) ────────────────────────────────────────────────────

    private static string Ts(string? value) => value is null ? "NULL" : $"'{value}'::timestamptz";
    private static string Txt(string? value) => value is null ? "NULL" : $"'{value.Replace("'", "''")}'";

    /// <summary>Inserta un registro por SQL con las fechas indicadas (ISO 8601 UTC).</summary>
    public Task<long> InsertRecordAsync(
        long assetId,
        string status,
        string? scheduledAt = null,
        string? startedAt = null,
        string? performedAt = null,
        string? nextAt = null,
        string typeCode = "PREVENTIVO",
        string? description = null,
        long? incidentId = null,
        long? performedBy = null,
        string? cancelledAt = null,
        string? reason = null,
        decimal? cost = null,
        string? partsUsed = null,
        string? statusBefore = null,
        string? statusAfter = null,
        bool deleted = false)
        => Db.ScalarAsync<long>($"""
            INSERT INTO inventario.maintenance_records
                (asset_id, maintenance_type_id, status, scheduled_at, started_at, performed_at, next_maintenance_at,
                 description, incident_id, performed_by, cancelled_at, cancellation_reason, cost, parts_used,
                 status_before_id, status_after_id, is_deleted, deleted_at, created_by)
            VALUES ({assetId},
                (SELECT maintenance_type_id FROM inventario.maintenance_types WHERE code = '{typeCode}'),
                '{status}', {Ts(scheduledAt)}, {Ts(startedAt)}, {Ts(performedAt)}, {Ts(nextAt)},
                {Txt(description)}, {(incidentId?.ToString() ?? "NULL")}, {(performedBy?.ToString() ?? "NULL")},
                {Ts(cancelledAt)}, {Txt(reason)}, {(cost?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "NULL")}, {Txt(partsUsed)},
                {(statusBefore is null ? "NULL" : $"(SELECT asset_status_id FROM inventario.asset_statuses WHERE code = '{statusBefore}')")},
                {(statusAfter is null ? "NULL" : $"(SELECT asset_status_id FROM inventario.asset_statuses WHERE code = '{statusAfter}')")},
                {(deleted ? "true" : "false")}, {(deleted ? "now()" : "NULL")}, {AdminId})
            RETURNING maintenance_record_id
            """);

    public Task SetAssetAsync(long assetId, string setClause)
        => Db.ExecuteAsync($"UPDATE inventario.assets SET {setClause} WHERE asset_id = {assetId}");

    public Task<long> TypeIdAsync(string code)
        => Db.ScalarAsync<long>($"SELECT maintenance_type_id FROM inventario.maintenance_types WHERE code = '{code}'");

    public async Task<(HttpStatusCode Status, JsonElement Body, string Raw)> GetAsync(string path, string? token = null)
    {
        var response = await SendAsync(HttpMethod.Get, path, null, token);
        var raw = await response.Content.ReadAsStringAsync();
        JsonElement body = default;
        if (raw.Length > 0 && (raw.TrimStart().StartsWith('{') || raw.TrimStart().StartsWith('[')))
        {
            body = JsonDocument.Parse(raw).RootElement.Clone();
        }

        return (response.StatusCode, body, raw);
    }

    public static long[] Ids(JsonElement array)
        => array.EnumerateArray().Select(i => i.GetProperty("maintenanceRecordId").GetInt64()).ToArray();

    // ── Edición, borrado y documentos (M.5) ────────────────────────────────

    public Task<HttpResponseMessage> UpdateAsync(long recordId, object body, string? token = null)
        => SendAsync(HttpMethod.Put, $"/api/maintenance/{recordId}", body, token);

    public Task<HttpResponseMessage> DeleteAsync(long recordId, int expectedVersion, string? token = null)
        => SendAsync(HttpMethod.Delete, $"/api/maintenance/{recordId}?expectedVersion={expectedVersion}", null, token);

    public Task<HttpResponseMessage> UploadDocumentAsync(
        byte[] bytes, string fileName, string contentType, long ownerId, string ownerType = "MAINTENANCE", string? token = null)
    {
        var form = new MultipartFormDataContent();
        form.Add(new StringContent(ownerType), "ownerType");
        form.Add(new StringContent(ownerId.ToString()), "ownerId");
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = MediaTypeHeaderValue.Parse(contentType);
        form.Add(file, "file", fileName);

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/documents") { Content = form };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token ?? AdminToken);
        return Client.SendAsync(request);
    }

    public Task<HttpResponseMessage> DownloadDocumentAsync(long documentId, string? token = null)
        => SendAsync(HttpMethod.Get, $"/api/documents/{documentId}/download", null, token);

    /// <summary>Valor guardado de una columna timestamptz, en ISO 8601 UTC (para reenviarlo idéntico).</summary>
    public Task<string> IsoAsync(long recordId, string column)
        => Db.ScalarAsync<string>($"""
            SELECT to_char({column} AT TIME ZONE 'UTC', 'YYYY-MM-DD"T"HH24:MI:SS.US"Z"')
            FROM inventario.maintenance_records WHERE maintenance_record_id = {recordId}
            """);

    public static async Task<JsonElement> ReadAsync(HttpResponseMessage response)
        => JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.Clone();

    /// <summary>Texto del problema (detail y, si hay, el primer mensaje de errors).</summary>
    public static async Task<string> ProblemTextAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();
        return body;
    }

    public async ValueTask DisposeAsync()
    {
        Client.Dispose();
        await _factory.DisposeAsync();
        await Db.DisposeAsync();
        if (_tempDir is not null && Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }
}
