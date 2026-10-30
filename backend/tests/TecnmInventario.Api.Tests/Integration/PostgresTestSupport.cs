using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using TecnmInventario.Api.Data;
using Testcontainers.PostgreSql;

namespace TecnmInventario.Api.Tests.Integration;

/// <summary>
/// <c>[Fact]</c> que se marca como Omitido en descubrimiento cuando Docker no
/// está disponible y <c>REQUIRE_DOCKER_TESTS</c> no exige ejecutar. xUnit 2.9.3
/// con xunit.runner.visualstudio 3.1.4 NO reconoce el skip dinámico
/// (<c>SkipException</c> en el cuerpo del test): lo reporta como fallo con el
/// prefijo <c>$XunitDynamicSkip$</c>. Por eso la decisión se toma en el
/// atributo, que es el mecanismo que el runner sí respeta.
/// </summary>
public sealed class DockerFactAttribute : FactAttribute
{
    public DockerFactAttribute()
    {
        if (!DockerGuard.IsDockerAvailable && !DockerGuard.IsRequired)
        {
            Skip = "Docker no disponible: tests de integración omitidos. " +
                   "Arranca Docker o ejecuta --filter \"Category!=Integration\".";
        }
    }
}

/// <summary>
/// Comportamiento cuando Docker no está disponible:
/// <list type="bullet">
///   <item>Sin <c>REQUIRE_DOCKER_TESTS=1</c>: los tests de integración se OMITEN
///   con un mensaje explícito (la suite no falla).</item>
///   <item>Con <c>REQUIRE_DOCKER_TESTS=1</c>: FALLAN con un mensaje claro.</item>
/// </list>
/// La sonda se evalúa una sola vez por proceso.
/// </summary>
internal static class DockerGuard
{
    private static readonly Lazy<bool> Available = new(ProbeDocker, isThreadSafe: true);

    public static bool IsDockerAvailable => Available.Value;

    public static bool IsRequired
        => string.Equals(Environment.GetEnvironmentVariable("REQUIRE_DOCKER_TESTS"), "1", StringComparison.Ordinal);

    public static void EnsureAvailable()
    {
        if (IsDockerAvailable)
        {
            return;
        }

        const string reason =
            "Docker no disponible (la sonda 'docker info' falló). " +
            "Los tests de integración necesitan un PostgreSQL real vía Testcontainers.";

        if (IsRequired)
        {
            throw new InvalidOperationException(
                $"{reason} REQUIRE_DOCKER_TESTS=1 exige ejecutarlos: se considera un fallo, no un omitido.");
        }

        // Si el atributo [DockerFact] no omitió el test, es que la sonda pasó
        // en descubrimiento y Docker cayó después: se falla con contexto en
        // lugar de pretender omitir a mitad de la corrida.
        throw new InvalidOperationException(
            $"{reason} La sonda pasó en descubrimiento pero Docker ya no responde: " +
            "no se puede omitir en ejecución con xUnit 2.9.3.");
    }

    private static bool ProbeDocker()
    {
        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo("docker", "info --format {{.ServerVersion}}")
                {
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            if (!process.Start())
            {
                return false;
            }

            // Vaciar stdout/stderr para no bloquear si el proceso escribe mucho.
            process.StandardOutput.ReadToEnd();
            process.StandardError.ReadToEnd();

            if (!process.WaitForExit(20_000))
            {
                try
                {
                    process.Kill(entireProcessTree: true);
                }
                catch
                {
                    // La sonda ya falló; el kill es best-effort.
                }

                return false;
            }

            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }
}

/// <summary>
/// Colección compartida: un único contenedor PostgreSQL por corrida. Cada
/// clase/test crea su propia base de datos clonando <c>template_inventario</c>.
/// </summary>
[CollectionDefinition(Name)]
public sealed class PostgresCollection : ICollectionFixture<PostgresContainerFixture>
{
    public const string Name = "postgres-integration";
}

/// <summary>
/// Contenedor PostgreSQL efímero con puerto aleatorio. Al arrancar crea la
/// base plantilla y aplica los scripts de esquema/seed (nunca los de datos de
/// prueba de desarrollo). El arranque es perezoso: si Docker no está, los
/// tests pueden omitirse en lugar de fallar en la construcción del fixture.
/// </summary>
public sealed class PostgresContainerFixture : IAsyncLifetime
{
    public const string TemplateDatabase = "template_inventario";

    /// <summary>
    /// Orden autoritativo. <c>seed-documents.sql</c> también es esquema: crea
    /// <c>inventario.documents</c> y sus triggers. <c>add-auth-sessions.sql</c>
    /// es idempotente y redundante con database.sql §18 (paridad con scripts/README.md).
    /// </summary>
    private static readonly string[] Scripts =
    {
        "database.sql",
        "seed-database.sql",
        "seed-permissions.sql",
        "seed-documents.sql",
        "add-procurement-permission.sql",
        "add-incidents-permissions.sql",
        "seed-users-view-permission.sql",
        "add-auth-sessions.sql"
    };

    private readonly SemaphoreSlim _gate = new(1, 1);
    private PostgreSqlContainer? _container;
    private string? _adminConnectionString;
    private bool _started;

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task<string> GetAdminConnectionStringAsync()
    {
        await EnsureStartedAsync();
        return _adminConnectionString!;
    }

    public async Task<PostgresDatabase> CreateDatabaseAsync(string label)
    {
        await EnsureStartedAsync();

        var name = BuildDatabaseName(label);
        await using (var connection = new NpgsqlConnection(_adminConnectionString))
        {
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand(
                $"CREATE DATABASE \"{name}\" TEMPLATE \"{TemplateDatabase}\"", connection);
            await command.ExecuteNonQueryAsync();
        }

        return new PostgresDatabase(name, BuildConnectionString(name), _adminConnectionString!);
    }

    public async Task DisposeAsync()
    {
        if (_container is not null)
        {
            NpgsqlConnection.ClearAllPools();
            await _container.DisposeAsync();
            _container = null;
        }

        _gate.Dispose();
    }

    private async Task EnsureStartedAsync()
    {
        // Fail-fast/skip ANTES de crear el contenedor: con Docker caído el
        // builder tardaría en agotar reintentos.
        DockerGuard.EnsureAvailable();

        await _gate.WaitAsync();
        try
        {
            if (_started)
            {
                return;
            }

            _container = new PostgreSqlBuilder("postgres:18-alpine")
                .WithDatabase("postgres")
                .WithUsername("tecnm_test")
                .WithPassword(Guid.NewGuid().ToString("N"))
                // Puerto aleatorio en el host: nunca el 5433 de desarrollo.
                .WithPortBinding(0, 5432)
                .WithCleanUp(true)
                .Build();

            await _container.StartAsync();

            var baseConnectionString = _container.GetConnectionString();
            var builder = new NpgsqlConnectionStringBuilder(baseConnectionString)
            {
                // Sin pool en la conexión de administración/plantilla: PostgreSQL
                // rechaza CREATE DATABASE ... TEMPLATE si hay sesiones abiertas.
                Pooling = false,
                Timeout = 30
            };

            Assert.NotEqual(5433, builder.Port);
            _adminConnectionString = builder.ConnectionString;

            await CreateTemplateAsync(_adminConnectionString);
            _started = true;
        }
        finally
        {
            _gate.Release();
        }
    }

    private static async Task CreateTemplateAsync(string adminConnectionString)
    {
        await using (var admin = new NpgsqlConnection(adminConnectionString))
        {
            await admin.OpenAsync();
            await using var create = new NpgsqlCommand(
                $"CREATE DATABASE \"{TemplateDatabase}\"", admin);
            await create.ExecuteNonQueryAsync();
        }

        var templateConnectionString =
            new NpgsqlConnectionStringBuilder(adminConnectionString)
            {
                Database = TemplateDatabase,
                Pooling = false
            }.ConnectionString;

        await using (var connection = new NpgsqlConnection(templateConnectionString))
        {
            await connection.OpenAsync();

            foreach (var script in Scripts)
            {
                var path = Path.Combine(AppContext.BaseDirectory, "Sql", script);
                if (!File.Exists(path))
                {
                    throw new FileNotFoundException(
                        $"No se encontró el script SQL enlazado en el proyecto de tests: {path}", path);
                }

                var sql = await File.ReadAllTextAsync(path);
                await using var transaction = await connection.BeginTransactionAsync();
                await using var command = new NpgsqlCommand(sql, connection, transaction)
                {
                    CommandTimeout = 120
                };
                await command.ExecuteNonQueryAsync();
                await transaction.CommitAsync();
            }
        }

        // Defensa extra: aunque la conexión de plantilla no usa pool, se limpia
        // cualquier pool residual antes de clonar.
        NpgsqlConnection.ClearAllPools();

        await VerifySchemaAsync(adminConnectionString);
    }

    private static async Task VerifySchemaAsync(string adminConnectionString)
    {
        var templateConnectionString =
            new NpgsqlConnectionStringBuilder(adminConnectionString)
            {
                Database = TemplateDatabase,
                Pooling = false
            }.ConnectionString;

        var expected = new[]
        {
            "trg_assets_version", "trg_assets_soft_delete",
            "trg_app_users_version", "trg_app_users_soft_delete",
            "trg_roles_version", "trg_roles_soft_delete",
            "trg_locations_version", "trg_locations_soft_delete",
            "trg_suppliers_version", "trg_suppliers_soft_delete",
            "trg_asset_warranties_version", "trg_asset_warranties_soft_delete",
            "trg_documents_version", "trg_documents_soft_delete",
            "trg_documents_validate_owner"
        };

        await using var connection = new NpgsqlConnection(templateConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            "select t.tgname from pg_trigger t " +
            "join pg_class c on c.oid = t.tgrelid " +
            "join pg_namespace n on n.oid = c.relnamespace " +
            "where n.nspname = 'inventario' and not t.tgisinternal", connection);

        var found = new HashSet<string>(StringComparer.Ordinal);
        await using (var reader = await command.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                found.Add(reader.GetString(0));
            }
        }

        var missing = expected.Where(e => !found.Contains(e)).ToArray();
        if (missing.Length > 0)
        {
            throw new InvalidOperationException(
                "La plantilla de integración no quedó completa. Faltan triggers: " +
                string.Join(", ", missing));
        }
    }

    private static string BuildDatabaseName(string label)
    {
        var sanitized = new string(label
            .ToLowerInvariant()
            .Select(c => char.IsLetterOrDigit(c) ? c : '_')
            .ToArray());

        // Identificador PostgreSQL: máximo 63 bytes. Se recorta y se añade un
        // sufijo único para permitir clonados repetidos del mismo test.
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var maxPrefix = 63 - "test_".Length - 1 - suffix.Length;
        if (sanitized.Length > maxPrefix)
        {
            sanitized = sanitized[..maxPrefix];
        }

        return $"test_{sanitized}_{suffix}";
    }

    private string BuildConnectionString(string database)
        => new NpgsqlConnectionStringBuilder(_adminConnectionString)
        {
            Database = database
        }.ConnectionString;
}

/// <summary>
/// Base de datos propia de un test, clonada de la plantilla. Se destruye al
/// terminar (DROP ... WITH FORCE) y no se comparte entre tests.
/// </summary>
public sealed class PostgresDatabase : IAsyncDisposable
{
    private readonly string _adminConnectionString;
    private readonly string _label;

    internal PostgresDatabase(string name, string connectionString, string adminConnectionString)
    {
        Name = name;
        ConnectionString = connectionString;
        _adminConnectionString = adminConnectionString;
        _label = name;
    }

    public string Name { get; }

    public string ConnectionString { get; }

    public string Label => _label;

    public DbContextOptions<TecnmInventarioDbContext> CreateOptions()
        => new DbContextOptionsBuilder<TecnmInventarioDbContext>()
            .UseNpgsql(ConnectionString)
            .Options;

    public TecnmInventarioDbContext CreateContext()
        => new(CreateOptions());

    public async Task<NpgsqlConnection> OpenConnectionAsync()
    {
        var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        return connection;
    }

    public async Task ExecuteAsync(string sql)
    {
        await using var connection = await OpenConnectionAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }

    public async Task<T> ScalarAsync<T>(string sql)
    {
        await using var connection = await OpenConnectionAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        var value = await command.ExecuteScalarAsync();
        return (T)Convert.ChangeType(value!, typeof(T));
    }

    public Task<long> CountAsync(string tableName, string whereClause = "true")
        => ScalarAsync<long>($"select count(*) from inventario.{tableName} where {whereClause}");

    public async ValueTask DisposeAsync()
    {
        NpgsqlConnection.ClearAllPools();

        await using var connection = new NpgsqlConnection(_adminConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            $"DROP DATABASE IF EXISTS \"{Name}\" WITH (FORCE)", connection);
        await command.ExecuteNonQueryAsync();
    }
}

/// <summary>
/// Base de los tests de integración: crea una BD nueva por test. El guard de
/// Docker se evalúa dentro del cuerpo del test, de forma que sin Docker el
/// resultado sea "Omitido" (o fallo si REQUIRE_DOCKER_TESTS=1).
/// </summary>
[Collection(PostgresCollection.Name)]
public abstract class PostgresTestBase
{
    protected PostgresTestBase(PostgresContainerFixture fixture)
    {
        Fixture = fixture;
    }

    protected PostgresContainerFixture Fixture { get; }

    protected Task<PostgresDatabase> CreateDatabaseAsync()
        => Fixture.CreateDatabaseAsync(GetType().Name);
}
