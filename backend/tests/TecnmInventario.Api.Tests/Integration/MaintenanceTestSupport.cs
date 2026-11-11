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

    protected async Task<MaintenanceEnv> CreateEnvAsync(Action<IServiceCollection>? configureTestServices = null)
        => await MaintenanceEnv.CreateAsync(await CreateDatabaseAsync(), configureTestServices);
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

    public static async Task<MaintenanceEnv> CreateAsync(
        PostgresDatabase db, Action<IServiceCollection>? configureTestServices)
    {
        var factory = new ApiFactory(db.ConnectionString, configureTestServices: configureTestServices);
        var env = new MaintenanceEnv(db, factory, Fase52PerimetroTests.NewClient(factory));
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
    }
}
