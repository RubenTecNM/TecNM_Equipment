using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;
using TecnmInventario.Api.Common.Middleware;
using TecnmInventario.Api.Core.Entities;
using TecnmInventario.Api.Data;

namespace TecnmInventario.Api.Tests.Integration;

// ═══════════════════════════════════════════════════════════════════════════
// Subcheckpoint 5.6: restos de seguridad. HTTP contra la API real (ApiFactory)
// y el PostgreSQL efímero. Cada test usa su propia carpeta de storage
// temporal; los usuarios, roles y archivos son ficticios.
// ═══════════════════════════════════════════════════════════════════════════

[Trait("Category", "Integration")]
public sealed class Fase56SeguridadTests : PostgresTestBase
{
    private const string Password = "Sd56Correcta!";
    private const string GenericForbidden = "No tienes permiso para realizar esta acción.";
    private const string GenericRaise = "La operación no cumple las reglas del sistema.";
    private const string FunctionalMessage = "Mensaje funcional de prueba 5.6.";

    private static readonly byte[] Pdf = Encoding.ASCII.GetBytes("%PDF-1.4\n% evidencia ficticia 5.6\n");

    public Fase56SeguridadTests(PostgresContainerFixture fixture)
        : base(fixture) { }

    // ── R1: 403 genérico ───────────────────────────────────────────────────

    [DockerFact]
    public async Task Forbidden_NoIncluyeRolNiOwnerType()
    {
        await using var env = await Env.CreateAsync(this);
        var token = await env.CreateRestrictedUserTokenAsync("SD56_SINPERM", "sd56_sinperm");

        var response = await env.UploadAsync(token, Pdf, "informe.pdf", "INCIDENT", env.IncidentId);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(GenericForbidden, JsonDocument.Parse(body).RootElement.GetProperty("detail").GetString());
        Assert.DoesNotContain("SD56_SINPERM", body);
        Assert.DoesNotContain("INCIDENT", body);
        Assert.DoesNotContain("incidents.", body);
    }

    // El 403 de una política de permiso lo genera ASP.NET antes de llegar a
    // ningún handler: tampoco debe revelar el rol ni el permiso exigido.
    [DockerFact]
    public async Task Forbidden_PorPoliticaDePermiso_NoRevelaRolNiPermiso()
    {
        await using var env = await Env.CreateAsync(this);
        var token = await env.CreateRestrictedUserTokenAsync("SD56_SINPERM", "sd56_sinperm");

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/register")
        {
            Content = JsonContent.Create(new
            {
                username = "sd56_nuevo",
                email = "sd56_nuevo@test.local",
                password = "ClaveSegura123!",
                firstName = "Nombre",
                lastName = "Apellido",
                roleCode = "TECNICO"
            })
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await env.Client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.DoesNotContain("SD56_SINPERM", body);
        Assert.DoesNotContain("users.manage", body);
        Assert.True(body.Length == 0, $"cuerpo inesperado ({response.Content.Headers.ContentType}): {body}");
    }

    // ── R2: P0001 ──────────────────────────────────────────────────────────

    // Trigger INTERNO real (validate_document_owner): el dueño desaparece entre
    // la comprobación del handler y el INSERT, así que el trigger lanza P0001
    // con su texto técnico ("documents.owner_id N no existe en la tabla ...").
    [DockerFact]
    public async Task P0001_NoListado_SeSustituyePorMensajeGenerico()
    {
        var race = new PreInsertSqlInterceptor(null);
        var logs = new CapturingLoggerProvider();
        await using var env = await Env.CreateAsync(this, services =>
        {
            services.ConfigureDbContext<TecnmInventarioDbContext>(o => o.AddInterceptors(race));
            services.AddSingleton<ILoggerProvider>(logs);
        });
        race.Arm($"DELETE FROM inventario.incidents WHERE incident_id = {env.IncidentId}", env.Db.ConnectionString);

        var response = await env.UploadAsync(env.AdminToken, Pdf, "informe.pdf", "INCIDENT", env.IncidentId);
        var body = await response.Content.ReadAsStringAsync();

        Assert.True(race.Fired, "El interceptor no llegó a quitar el incidente.");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(GenericRaise, JsonDocument.Parse(body).RootElement.GetProperty("detail").GetString());
        Assert.DoesNotContain("owner_id", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("owner_type", body, StringComparison.OrdinalIgnoreCase);
        // El texto original queda en el log (LogWarning), no en la respuesta.
        Assert.Contains(logs.Entries, e => e.Level == LogLevel.Warning && e.Message.Contains("documents.owner_id"));
    }

    // Mismo mensaje en la lista blanca: sale tal cual. Se crea un trigger de
    // prueba SOLO en la BD clonada de este test.
    [DockerFact]
    public async Task P0001_Funcional_SaleTalCual()
    {
        await using var env = await Env.CreateAsync(this, services =>
            services.AddSingleton(new FunctionalRaiseMessages([FunctionalMessage])));
        await env.CreateRaiseTriggerAsync(FunctionalMessage);

        var response = await env.UploadAsync(env.AdminToken, Pdf, "informe.pdf", "INCIDENT", env.IncidentId);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(FunctionalMessage, JsonDocument.Parse(body).RootElement.GetProperty("detail").GetString());
    }

    // La coincidencia es exacta: una variante del mismo mensaje NO sale.
    [DockerFact]
    public async Task P0001_VarianteDelMensajeFuncional_SeSustituyePorMensajeGenerico()
    {
        await using var env = await Env.CreateAsync(this, services =>
            services.AddSingleton(new FunctionalRaiseMessages([FunctionalMessage])));
        await env.CreateRaiseTriggerAsync(FunctionalMessage + " (variante)");

        var response = await env.UploadAsync(env.AdminToken, Pdf, "informe.pdf", "INCIDENT", env.IncidentId);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(GenericRaise, JsonDocument.Parse(body).RootElement.GetProperty("detail").GetString());
    }

    // ── R3a: 413 también sin Content-Length ────────────────────────────────

    // TestServer no aplica límites de cuerpo (no implementa
    // IHttpMaxRequestBodySizeFeature): se prueba con Kestrel real
    // (WebApplicationFactory.UseKestrel, .NET 10) y un cuerpo "chunked".
    [DockerFact]
    public async Task Upload_ChunkedMayorQueElLimite_Devuelve413EnEspanol()
    {
        await using var env = await Env.CreateAsync(this, kestrel: true);
        var huge = new byte[5 * 1024 * 1024 + 256 * 1024];
        Pdf.CopyTo(huge, 0);

        var form = new MultipartFormDataContent
        {
            { new StringContent("INCIDENT"), "ownerType" },
            { new StringContent(env.IncidentId.ToString()), "ownerId" }
        };
        var file = new ByteArrayContent(huge);
        file.Headers.ContentType = MediaTypeHeaderValue.Parse("application/pdf");
        form.Add(file, "file", "enorme.pdf");
        var bytes = await form.ReadAsByteArrayAsync();

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/documents")
        {
            Content = new UnknownLengthContent(bytes, form.Headers.ContentType!)
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", env.AdminToken);
        request.Headers.ExpectContinue = true;

        var response = await env.RawClient!.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        Assert.False(request.Content.Headers.ContentLength.HasValue, "El cuerpo debe ir sin Content-Length (chunked).");
        Assert.True(response.StatusCode == HttpStatusCode.RequestEntityTooLarge, $"{(int)response.StatusCode}: {body}");
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = JsonDocument.Parse(body).RootElement;
        Assert.Equal(413, problem.GetProperty("status").GetInt32());
        Assert.Equal("El archivo supera el tamaño máximo permitido.", problem.GetProperty("detail").GetString());
        Assert.Empty(Directory.GetFiles(env.Root));
        Assert.Equal(0, await env.Db.CountAsync("documents"));
    }

    // ── R3b: nombre de descarga siempre sanitizado ─────────────────────────

    [DockerFact]
    public async Task Download_DocumentoAntiguoConTraversalEnFileName_ContentDispositionSanitizado()
    {
        await using var env = await Env.CreateAsync(this);
        // Documento "antiguo": registro insertado por SQL con el nombre crudo del cliente.
        var key = $"{Guid.NewGuid():N}.pdf";
        await File.WriteAllBytesAsync(Path.Combine(env.Root, key), Pdf);
        var documentId = await env.Db.ScalarAsync<long>($"""
            INSERT INTO inventario.documents (owner_type, owner_id, file_name, storage_key, mime_type, size_bytes)
            VALUES ('INCIDENT', {env.IncidentId}, '../../x.pdf', '{key}', 'application/pdf', {Pdf.Length})
            RETURNING document_id
            """);

        using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/documents/{documentId}/download");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", env.AdminToken);
        var response = await env.Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var disposition = string.Join(";", response.Content.Headers.GetValues("Content-Disposition"));
        Assert.DoesNotContain("..", disposition);
        Assert.DoesNotContain("/", disposition);
        Assert.Equal("x.pdf", response.Content.Headers.ContentDisposition?.FileName?.Trim('"'));
    }

    // ── R3c: dueño con el activo borrado lógicamente ───────────────────────

    [DockerTheory]
    [InlineData("INCIDENT")]
    [InlineData("MAINTENANCE")]
    public async Task Upload_DuenoDeActivoBorrado_Rechaza400(string ownerType)
    {
        await using var env = await Env.CreateAsync(this);
        var ownerId = ownerType == "INCIDENT" ? env.IncidentId : env.MaintenanceId;
        await env.Db.ExecuteAsync($"UPDATE inventario.assets SET is_deleted = true WHERE asset_id = {env.AssetId}");

        var response = await env.UploadAsync(env.AdminToken, Pdf, "informe.pdf", ownerType, ownerId);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(
            "El elemento al que se quiere adjuntar el documento no existe o fue eliminado.",
            JsonDocument.Parse(body).RootElement.GetProperty("detail").GetString());
        Assert.Equal(0, await env.Db.CountAsync("documents"));
        Assert.Empty(Directory.GetFiles(env.Root));
    }

    // ── Soportes ───────────────────────────────────────────────────────────

    private sealed class UnknownLengthContent : HttpContent
    {
        private readonly byte[] _bytes;

        public UnknownLengthContent(byte[] bytes, MediaTypeHeaderValue contentType)
        {
            _bytes = bytes;
            Headers.ContentType = contentType;
        }

        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context)
            => stream.WriteAsync(_bytes).AsTask();

        protected override bool TryComputeLength(out long length)
        {
            length = 0;
            return false;
        }
    }

    // Ejecuta SQL por OTRA conexión justo antes del primer INSERT de un
    // Document (simula que el dueño cambia entre la comprobación y el INSERT).
    private sealed class PreInsertSqlInterceptor : SaveChangesInterceptor
    {
        private readonly object _gate = new();
        private string? _sql;
        private string? _connectionString;

        public PreInsertSqlInterceptor(string? sql) => _sql = sql;

        public bool Fired { get; private set; }

        public void Arm(string sql, string connectionString)
        {
            lock (_gate)
            {
                _sql = sql;
                _connectionString = connectionString;
            }
        }

        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            string? sql = null;
            string? connectionString = null;
            lock (_gate)
            {
                if (_sql is not null && eventData.Context!.ChangeTracker.Entries<Document>().Any(e => e.State == EntityState.Added))
                {
                    (sql, connectionString) = (_sql, _connectionString);
                    _sql = null;
                }
            }

            if (sql is not null)
            {
                await using var connection = new NpgsqlConnection(connectionString);
                await connection.OpenAsync(cancellationToken);
                await using var command = new NpgsqlCommand(sql, connection);
                await command.ExecuteNonQueryAsync(cancellationToken);
                Fired = true;
            }

            return result;
        }
    }

    private sealed class CapturingLoggerProvider : ILoggerProvider
    {
        private readonly List<(LogLevel Level, string Message)> _entries = [];

        public IReadOnlyList<(LogLevel Level, string Message)> Entries
        {
            get
            {
                lock (_entries) return _entries.ToArray();
            }
        }

        public ILogger CreateLogger(string categoryName) => new CapturingLogger(this);

        public void Dispose() { }

        private sealed class CapturingLogger(CapturingLoggerProvider owner) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
                Func<TState, Exception?, string> formatter)
            {
                lock (owner._entries) owner._entries.Add((logLevel, formatter(state, exception)));
            }
        }
    }

    private sealed class Env : IAsyncDisposable
    {
        private readonly string _tempDir;
        private readonly ApiFactory _factory;

        private Env(string tempDir, PostgresDatabase db, ApiFactory factory, HttpClient client)
        {
            _tempDir = tempDir;
            Db = db;
            _factory = factory;
            Client = client;
        }

        public PostgresDatabase Db { get; }
        public HttpClient Client { get; }

        // Solo con Kestrel: cliente SIN los handlers de WebApplicationFactory
        // (que cargan el cuerpo en memoria y le ponen Content-Length), para
        // poder enviar un cuerpo "chunked" de verdad.
        public HttpClient? RawClient { get; private set; }
        public string Root => Path.Combine(_tempDir, "storage");
        public string AdminToken { get; private set; } = "";
        public long AssetId { get; private set; }
        public long IncidentId { get; private set; }
        public long MaintenanceId { get; private set; }

        public static async Task<Env> CreateAsync(
            Fase56SeguridadTests test, Action<IServiceCollection>? configureTestServices = null, bool kestrel = false)
        {
            var db = await test.CreateDatabaseAsync();
            var tempDir = Path.Combine(Path.GetTempPath(), $"tecnm-5-6-{Guid.NewGuid():N}");
            Directory.CreateDirectory(Path.Combine(tempDir, "storage"));

            var factory = new ApiFactory(
                db.ConnectionString,
                new Dictionary<string, string?> { ["Storage:LocalPath"] = Path.Combine(tempDir, "storage") },
                configureTestServices: configureTestServices);
            if (kestrel)
            {
                factory.UseKestrel();
            }

            var client = kestrel
                ? factory.CreateClient()
                : Fase52PerimetroTests.NewClient(factory);
            var env = new Env(tempDir, db, factory, client);
            if (kestrel)
            {
                var address = factory.Services.GetRequiredService<Microsoft.AspNetCore.Hosting.Server.IServer>()
                    .Features.Get<Microsoft.AspNetCore.Hosting.Server.Features.IServerAddressesFeature>()!
                    .Addresses.First();
                env.RawClient = new HttpClient { BaseAddress = new Uri(address), Timeout = TimeSpan.FromSeconds(60) };
            }

            await env.SeedAsync();
            return env;
        }

        private async Task SeedAsync()
        {
            var adminId = await InsertUserAsync("sd56_admin", "ADMIN");
            AssetId = await Db.ScalarAsync<long>("""
                INSERT INTO inventario.assets (inventory_code, asset_type_id, asset_status_id)
                SELECT 'SD56-A1', at.asset_type_id, s.asset_status_id
                FROM inventario.asset_types at
                CROSS JOIN inventario.asset_statuses s
                WHERE at.code = 'PC' AND s.code = 'CON_FALLA'
                RETURNING asset_id
                """);
            IncidentId = await Db.ScalarAsync<long>($"""
                INSERT INTO inventario.incidents
                    (asset_id, incident_type_id, incident_priority_id, incident_status_id,
                     reported_by, title, created_by)
                SELECT {AssetId}, it.incident_type_id, ip.incident_priority_id,
                       ist.incident_status_id, {adminId}, 'Falla ficticia 5.6', {adminId}
                FROM inventario.incident_types it, inventario.incident_priorities ip,
                     inventario.incident_statuses ist
                WHERE it.code = 'HARDWARE' AND ip.code = 'ALTA' AND ist.code = 'ABIERTO'
                RETURNING incident_id
                """);
            // M.1: sin status explícito el registro queda PROGRAMADO, y el CHECK
            // ck_maintenance_records_programado exige scheduled_at.
            MaintenanceId = await Db.ScalarAsync<long>($"""
                INSERT INTO inventario.maintenance_records (asset_id, maintenance_type_id, created_by, scheduled_at)
                SELECT {AssetId}, maintenance_type_id, {adminId}, now()
                FROM inventario.maintenance_types WHERE code = 'PREVENTIVO'
                RETURNING maintenance_record_id
                """);
            AdminToken = await LoginAsync("sd56_admin");
        }

        public async Task<string> CreateRestrictedUserTokenAsync(string roleCode, string username)
        {
            await Db.ExecuteAsync($"INSERT INTO inventario.roles (code, name) VALUES ('{roleCode}', 'Rol sin permisos 5.6')");
            await InsertUserAsync(username, roleCode);
            return await LoginAsync(username);
        }

        public Task CreateRaiseTriggerAsync(string message) => Db.ExecuteAsync($$"""
            CREATE FUNCTION inventario.sd56_raise() RETURNS trigger LANGUAGE plpgsql AS $f$
            BEGIN
              RAISE EXCEPTION '{{message}}';
            END;
            $f$;
            CREATE TRIGGER sd56_raise_trg BEFORE INSERT ON inventario.documents
            FOR EACH ROW EXECUTE FUNCTION inventario.sd56_raise();
            """);

        private async Task<long> InsertUserAsync(string username, string roleCode)
        {
            var hash = BCrypt.Net.BCrypt.HashPassword(Password, 4);
            return await Db.ScalarAsync<long>($"""
                INSERT INTO inventario.app_users
                    (username, email, password_hash, first_name, last_name, role_id, status)
                SELECT '{username}', '{username}@test.local', '{hash}', 'Usuario', 'Seguridad', role_id, 'activo'
                FROM inventario.roles WHERE code = '{roleCode}'
                RETURNING user_id
                """);
        }

        private async Task<string> LoginAsync(string username)
        {
            var login = await Client.PostAsJsonAsync("/api/auth/login", new { usernameOrEmail = username, password = Password });
            Assert.Equal(HttpStatusCode.OK, login.StatusCode);
            var cookie = login.Headers.GetValues("Set-Cookie").First(c => c.StartsWith("tecnm_at=", StringComparison.Ordinal));
            return cookie["tecnm_at=".Length..].Split(';')[0];
        }

        public Task<HttpResponseMessage> UploadAsync(string token, byte[] bytes, string fileName, string ownerType, long ownerId)
        {
            var form = new MultipartFormDataContent();
            form.Add(new StringContent(ownerType), "ownerType");
            form.Add(new StringContent(ownerId.ToString()), "ownerId");
            var file = new ByteArrayContent(bytes);
            file.Headers.ContentType = MediaTypeHeaderValue.Parse("application/pdf");
            form.Add(file, "file", fileName);

            var request = new HttpRequestMessage(HttpMethod.Post, "/api/documents") { Content = form };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            return Client.SendAsync(request);
        }

        public async ValueTask DisposeAsync()
        {
            Client.Dispose();
            RawClient?.Dispose();
            await _factory.DisposeAsync();
            await Db.DisposeAsync();
            if (Directory.Exists(_tempDir))
            {
                Directory.Delete(_tempDir, recursive: true);
            }
        }
    }
}
