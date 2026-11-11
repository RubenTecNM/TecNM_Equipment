using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using TecnmInventario.Api.Core.Entities;
using TecnmInventario.Api.Data;

namespace TecnmInventario.Api.Tests.Integration;

// ═══════════════════════════════════════════════════════════════════════════
// Subcheckpoint 5.4: unicidad sin mayúsculas en la BD (U1) y su traducción a
// 409 (U4). Contra el PostgreSQL efímero; los usuarios son ficticios.
// ═══════════════════════════════════════════════════════════════════════════

[Trait("Category", "Integration")]
public sealed class Fase54IndicesUnicidadTests : PostgresTestBase
{
    private const string UsernameIndex = "ux_app_users_username_lower";
    private const string EmailIndex = "ux_app_users_email_lower";
    private const string ScriptName = "add-app-users-lower-indexes.sql";
    private const string Password = "Sd54Correcta!";

    public Fase54IndicesUnicidadTests(PostgresContainerFixture fixture)
        : base(fixture) { }

    [DockerFact]
    public async Task BD_InsertarUsernameQueSoloDifiereEnMayusculas_FallaPorIndice()
    {
        await using var db = await CreateDatabaseAsync();
        await InsertRawUserAsync(db, "sd54_pepe", "sd54-pepe1@test.local");

        var ex = await Assert.ThrowsAsync<PostgresException>(
            () => InsertRawUserAsync(db, "SD54_PEPE", "sd54-pepe2@test.local"));

        Assert.Equal("23505", ex.SqlState);
        Assert.Equal(UsernameIndex, ex.ConstraintName);
    }

    [DockerFact]
    public async Task BD_InsertarEmailQueSoloDifiereEnMayusculas_FallaPorIndice()
    {
        await using var db = await CreateDatabaseAsync();
        await InsertRawUserAsync(db, "sd54_ana1", "sd54-ana@test.local");

        var ex = await Assert.ThrowsAsync<PostgresException>(
            () => InsertRawUserAsync(db, "sd54_ana2", "SD54-Ana@Test.Local"));

        Assert.Equal("23505", ex.SqlState);
        Assert.Equal(EmailIndex, ex.ConstraintName);
    }

    // Por HTTP y con la carrera real: el validador comprueba la unicidad ANTES
    // de insertar, así que una carrera entre dos altas solo la frena el índice.
    // El interceptor (solo de test) inserta la fila rival justo antes del INSERT
    // de Register para llegar a la restricción de la BD.
    [DockerTheory]
    [InlineData("username", "Ya existe un usuario con ese nombre.")]
    [InlineData("email", "Ya existe un usuario con ese correo.")]
    public async Task Violacion23505DeIndiceLower_SeTraduceA409(string column, string expectedDetail)
    {
        await using var db = await CreateDatabaseAsync();
        await InsertRawUserAsync(db, "sd54_admin", "sd54-admin@test.local", Hash(Password));

        var race = new RivalInsertInterceptor(db.ConnectionString);
        await using var factory = new ApiFactory(db.ConnectionString, configureTestServices: services =>
            services.ConfigureDbContext<TecnmInventarioDbContext>(o => o.AddInterceptors(race)));
        using var client = Fase52PerimetroTests.NewClient(factory);
        var token = await LoginAsync(client, "sd54_admin");

        // Rival con la misma identidad en OTRAS mayúsculas (los UNIQUE antiguos no lo ven).
        race.Arm(column == "username"
            ? ("SD54_CARRERA", "sd54-rival1@test.local")
            : ("sd54_rival2", "SD54-Carrera@Test.Local"));

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/register")
        {
            Content = JsonContent.Create(new
            {
                username = "sd54_carrera",
                email = "sd54-carrera@test.local",
                password = "ClaveSegura123!",
                firstName = "Nombre",
                lastName = "Apellido",
                roleCode = "TECNICO"
            })
        };
        if (column == "email")
        {
            request.Content = JsonContent.Create(new
            {
                username = "sd54_carrera2",
                email = "sd54-carrera@test.local",
                password = "ClaveSegura123!",
                firstName = "Nombre",
                lastName = "Apellido",
                roleCode = "TECNICO"
            });
        }
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        Assert.True(race.Fired, "El interceptor no llegó a insertar la fila rival.");
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(expectedDetail, JsonDocument.Parse(body).RootElement.GetProperty("detail").GetString());
        foreach (var leak in new[] { "ux_app_users", "duplicate key", "23505", "violates", "Npgsql", "inventario." })
            Assert.DoesNotContain(leak, body, StringComparison.OrdinalIgnoreCase);
    }

    [DockerFact]
    public async Task ScriptIndices_EsIdempotente()
    {
        await using var db = await CreateDatabaseAsync();
        var path = Path.Combine(AppContext.BaseDirectory, "Sql", ScriptName);
        Assert.True(File.Exists(path), $"No existe el script enlazado en el proyecto de tests: {path}");
        var sql = await File.ReadAllTextAsync(path);

        // La plantilla ya lo aplicó una vez: son la segunda y la tercera ejecución.
        await db.ExecuteAsync(sql);
        await db.ExecuteAsync(sql);

        var indexes = await db.ScalarAsync<long>($"""
            SELECT count(*) FROM pg_indexes
            WHERE schemaname = 'inventario' AND tablename = 'app_users'
              AND indexname IN ('{UsernameIndex}', '{EmailIndex}')
            """);
        Assert.Equal(2, indexes);
    }

    // ── Helpers ────────────────────────────────────────────────────────────

    private static string Hash(string password) => BCrypt.Net.BCrypt.HashPassword(password, 4);

    private static Task InsertRawUserAsync(
        PostgresDatabase db, string username, string email, string passwordHash = "NO_LOGIN")
        => db.ExecuteAsync($"""
            INSERT INTO inventario.app_users
                (username, email, password_hash, first_name, last_name, role_id, status)
            SELECT '{username}', '{email}', '{passwordHash}', 'Usuario', 'Indices', role_id, 'activo'
            FROM inventario.roles WHERE code = 'ADMIN'
            """);

    private static async Task<string> LoginAsync(HttpClient client, string username)
    {
        var response = await client.PostAsJsonAsync("/api/auth/login", new { usernameOrEmail = username, password = Password });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var header = response.Headers.GetValues("Set-Cookie").First(c => c.StartsWith("tecnm_at=", StringComparison.Ordinal));
        return header["tecnm_at=".Length..].Split(';')[0];
    }

    // Inserta una fila rival por otra conexión justo antes del primer INSERT de
    // un AppUser (simula otra alta concurrente). Solo existe en tests.
    private sealed class RivalInsertInterceptor : SaveChangesInterceptor
    {
        private readonly string _connectionString;
        private readonly object _gate = new();
        private (string Username, string Email)? _rival;

        public RivalInsertInterceptor(string connectionString) => _connectionString = connectionString;

        public bool Fired { get; private set; }

        public void Arm((string Username, string Email) rival)
        {
            lock (_gate) _rival = rival;
        }

        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            (string Username, string Email)? rival;
            lock (_gate)
            {
                var adding = eventData.Context!.ChangeTracker.Entries<AppUser>().Any(e => e.State == EntityState.Added);
                rival = adding ? _rival : null;
                if (rival is not null) _rival = null;
            }

            if (rival is { } r)
            {
                await using var connection = new NpgsqlConnection(_connectionString);
                await connection.OpenAsync(cancellationToken);
                await using var command = new NpgsqlCommand("""
                    INSERT INTO inventario.app_users
                        (username, email, password_hash, first_name, last_name, role_id, status)
                    SELECT @u, @e, 'NO_LOGIN', 'Rival', 'Carrera', role_id, 'activo'
                    FROM inventario.roles WHERE code = 'TECNICO'
                    """, connection);
                command.Parameters.AddWithValue("u", r.Username);
                command.Parameters.AddWithValue("e", r.Email);
                await command.ExecuteNonQueryAsync(cancellationToken);
                Fired = true;
            }

            return result;
        }
    }
}

/// <summary>[Theory] que se omite sin Docker igual que <see cref="DockerFactAttribute"/>.</summary>
public sealed class DockerTheoryAttribute : TheoryAttribute
{
    public DockerTheoryAttribute()
    {
        if (!DockerGuard.IsDockerAvailable && !DockerGuard.IsRequired)
        {
            Skip = "Docker no disponible: tests de integración omitidos. " +
                   "Arranca Docker o ejecuta --filter \"Category!=Integration\".";
        }
    }
}
