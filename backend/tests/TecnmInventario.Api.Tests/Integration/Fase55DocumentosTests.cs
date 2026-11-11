using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using System.Data.Common;
using TecnmInventario.Api.Data;

namespace TecnmInventario.Api.Tests.Integration;

// ═══════════════════════════════════════════════════════════════════════════
// Subcheckpoint 5.5: storage de documentos. Todo por HTTP contra la API real
// (ApiFactory) y el PostgreSQL efímero. Cada test usa su PROPIA carpeta de
// storage temporal (nunca la real) y la borra al terminar. Los JWT salen del
// login real; los datos son ficticios.
// ═══════════════════════════════════════════════════════════════════════════

[Trait("Category", "Integration")]
public sealed class Fase55DocumentosTests : PostgresTestBase
{
    private const string Password = "Sd55Correcta!";

    private static readonly byte[] Pdf = Encoding.ASCII.GetBytes("%PDF-1.4\n% evidencia ficticia 5.5\n");
    private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x0D];
    private static readonly byte[] Jpeg = [0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46];
    private static readonly byte[] Exe = [0x4D, 0x5A, 0x90, 0x00, 0x03, 0x00, 0x00, 0x00];

    public Fase55DocumentosTests(PostgresContainerFixture fixture)
        : base(fixture) { }

    // ── Upload ─────────────────────────────────────────────────────────────

    [DockerFact]
    public async Task Upload_PdfReal_201YSeGuardaConGuidYExtensionPdf()
    {
        await using var env = await Env.CreateAsync(this);

        var response = await env.UploadAsync(Pdf, "informe.pdf", "application/pdf");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var (key, mime, name) = await env.LastDocumentAsync();
        Assert.Matches("^[0-9a-f]{32}\\.pdf$", key);
        Assert.Equal("application/pdf", mime);
        Assert.Equal("informe.pdf", name);
        Assert.Equal(Pdf, await File.ReadAllBytesAsync(Path.Combine(env.Root, key)));
    }

    [DockerFact]
    public async Task Upload_ExtensionPdfPeroContenidoEjecutable_Rechaza400()
    {
        await using var env = await Env.CreateAsync(this);

        // Extensión y Content-Type de PDF, pero los bytes son "MZ" (ejecutable).
        var response = await env.UploadAsync(Exe, "factura.pdf", "application/pdf");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(Directory.GetFiles(env.Root));
        Assert.Equal(0, await env.Db.CountAsync("documents"));
    }

    [DockerFact]
    public async Task Upload_ExtensionJpgPeroContenidoPng_SeGuardaComoPng()
    {
        await using var env = await Env.CreateAsync(this);

        // Decide el contenido: ni la extensión .jpg ni el Content-Type image/jpeg del cliente.
        var response = await env.UploadAsync(Png, "foto.jpg", "image/jpeg");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var (key, mime, _) = await env.LastDocumentAsync();
        Assert.EndsWith(".png", key);
        Assert.Equal("image/png", mime);
    }

    // 5 MB + 1 byte cabe en el límite de la petición (con su margen) y lo
    // rechaza la validación del archivo: 400. El 413 es para lo que supera el
    // límite de la petición, y solo lo aplica un servidor real (ver Kestrel).
    [DockerFact]
    public async Task Upload_MayorQueElMaximo_Rechaza400()
    {
        await using var env = await Env.CreateAsync(this);
        var big = new byte[5 * 1024 * 1024 + 1];
        Pdf.CopyTo(big, 0);

        var response = await env.UploadAsync(big, "grande.pdf", "application/pdf");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(Directory.GetFiles(env.Root));
    }

    // Lo que supera el límite de la PETICIÓN (5 MB + 64 KB de margen) se corta
    // con 413 antes de leer el cuerpo; con 5 MB + 1 byte la petición cabe y lo
    // rechaza la validación del archivo (400, test anterior).
    [DockerFact]
    public async Task Upload_MuchoMayorQueElMaximo_Rechaza413()
    {
        await using var env = await Env.CreateAsync(this);
        var huge = new byte[5 * 1024 * 1024 + 256 * 1024];
        Pdf.CopyTo(huge, 0);

        var response = await env.UploadAsync(huge, "enorme.pdf", "application/pdf");
        var body = await response.Content.ReadAsStringAsync();

        Assert.True(response.StatusCode == HttpStatusCode.RequestEntityTooLarge, $"{(int)response.StatusCode}: {body}");
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(413, JsonDocument.Parse(body).RootElement.GetProperty("status").GetInt32());
        Assert.Empty(Directory.GetFiles(env.Root));
        Assert.Equal(0, await env.Db.CountAsync("documents"));
    }

    [DockerFact]
    public async Task Upload_ArchivoVacio_Rechaza400()
    {
        await using var env = await Env.CreateAsync(this);

        var response = await env.UploadAsync([], "vacio.pdf", "application/pdf");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0, await env.Db.CountAsync("documents"));
    }

    [DockerTheory]
    [InlineData("../../x.pdf")]
    [InlineData("..\\x.pdf")]
    public async Task Upload_NombreConSeparadoresYTraversal_NoSaleDeLaRaizYSeSanitiza(string clientName)
    {
        await using var env = await Env.CreateAsync(this);

        var response = await env.UploadAsync(Pdf, clientName, "application/pdf");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var (key, _, storedName) = await env.LastDocumentAsync();

        // En disco: un único archivo dentro de la raíz, y nada fuera de ella.
        Assert.Single(Directory.GetFiles(env.Root));
        Assert.Matches("^[0-9a-f]{32}\\.pdf$", key);
        Assert.Empty(Directory.GetFiles(Directory.GetParent(env.Root)!.FullName));

        // El nombre original solo es un dato: sin separadores ni traversal.
        Assert.DoesNotContain('/', storedName);
        Assert.DoesNotContain('\\', storedName);
        Assert.DoesNotContain("..", storedName);
        Assert.Equal("x.pdf", storedName);
    }

    [DockerTheory]
    [InlineData("INCIDENT")]
    [InlineData("MAINTENANCE")]
    [InlineData("ASSET_DISPOSAL")]
    [InlineData("ACQUISITION")]
    public async Task Upload_DuenoInexistente_Rechaza400(string ownerType)
    {
        await using var env = await Env.CreateAsync(this);

        var response = await env.UploadAsync(Pdf, "informe.pdf", "application/pdf", ownerType, ownerId: 987_654);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        // Mensaje funcional de la regla de negocio, no el texto del trigger de la BD.
        Assert.DoesNotContain("owner_id", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("documents.", body, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(Directory.GetFiles(env.Root));
    }

    [DockerTheory]
    [InlineData("INCIDENT")]
    [InlineData("ACQUISITION")]
    public async Task Upload_DuenoBorradoLogicamente_Rechaza400(string ownerType)
    {
        await using var env = await Env.CreateAsync(this);
        var ownerId = ownerType == "INCIDENT" ? env.IncidentId : env.AcquisitionId;
        var table = ownerType == "INCIDENT" ? "incidents" : "acquisitions";
        var idColumn = ownerType == "INCIDENT" ? "incident_id" : "acquisition_id";
        await env.Db.ExecuteAsync($"UPDATE inventario.{table} SET is_deleted = true WHERE {idColumn} = {ownerId}");

        var response = await env.UploadAsync(Pdf, "informe.pdf", "application/pdf", ownerType, ownerId);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0, await env.Db.CountAsync("documents"));
        Assert.Empty(Directory.GetFiles(env.Root));
    }

    [DockerFact]
    public async Task Upload_FallaElCommit_NoDejaArchivoHuerfano()
    {
        var failure = new FailingCommitInterceptor();
        await using var env = await Env.CreateAsync(this, services =>
            services.ConfigureDbContext<TecnmInventarioDbContext>(o => o.AddInterceptors(failure)));

        failure.Armed = true;
        var response = await env.UploadAsync(Pdf, "informe.pdf", "application/pdf");
        failure.Armed = false;

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal(0, await env.Db.CountAsync("documents"));
        Assert.Empty(Directory.GetFiles(env.Root));
    }

    // ── Download ───────────────────────────────────────────────────────────

    [DockerFact]
    public async Task Download_ArchivoFisicoAusente_Devuelve404SinRutaEnElCuerpo()
    {
        await using var env = await Env.CreateAsync(this);
        await env.UploadAsync(Pdf, "informe.pdf", "application/pdf");
        var (key, _, _) = await env.LastDocumentAsync();
        File.Delete(Path.Combine(env.Root, key));

        var response = await env.DownloadAsync(await env.LastDocumentIdAsync());
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("El archivo no está disponible.", JsonDocument.Parse(body).RootElement.GetProperty("detail").GetString());
        Assert.DoesNotContain(env.Root, body);
        Assert.DoesNotContain(key, body);
    }

    [DockerFact]
    public async Task Download_EnlaceSimbolicoDentroDeLaRaiz_NoSeSirve()
    {
        await using var env = await Env.CreateAsync(this);
        await env.UploadAsync(Pdf, "informe.pdf", "application/pdf");
        var (key, _, _) = await env.LastDocumentAsync();

        // Un archivo fuera de la raíz, y el archivo del documento reemplazado
        // por un enlace simbólico (dentro de la raíz) que apunta a él.
        var secret = Path.Combine(Directory.GetParent(env.Root)!.FullName, "secreto-ficticio.txt");
        await File.WriteAllTextAsync(secret, "CONTENIDO-FUERA-DE-LA-RAIZ");
        var path = Path.Combine(env.Root, key);
        File.Delete(path);
        File.CreateSymbolicLink(path, secret);

        var response = await env.DownloadAsync(await env.LastDocumentIdAsync());
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.DoesNotContain("CONTENIDO-FUERA-DE-LA-RAIZ", body);
    }

    [DockerFact]
    public async Task Download_EnviaContentTypeDetectadoYContentDispositionAttachment()
    {
        await using var env = await Env.CreateAsync(this);

        // El cliente declara image/jpeg y nombre .jpg, pero el contenido es PNG.
        await env.UploadAsync(Png, "foto.jpg", "image/jpeg");
        var id = await env.LastDocumentIdAsync();
        await env.Db.ExecuteAsync($"UPDATE inventario.documents SET file_name = 'foto-ñ.png' WHERE document_id = {id}");

        var response = await env.DownloadAsync(id);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("image/png", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("attachment", response.Content.Headers.ContentDisposition?.DispositionType);
        Assert.Equal("foto-ñ.png", response.Content.Headers.ContentDisposition?.FileNameStar);
    }

    // ── Delete ─────────────────────────────────────────────────────────────

    [DockerFact]
    public async Task Delete_ConservaElArchivoFisico()
    {
        await using var env = await Env.CreateAsync(this);
        await env.UploadAsync(Pdf, "informe.pdf", "application/pdf");
        var (key, _, _) = await env.LastDocumentAsync();
        var id = await env.LastDocumentIdAsync();

        using var request = new HttpRequestMessage(HttpMethod.Delete, $"/api/documents/{id}?expectedVersion=1");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", env.Token);
        var response = await env.Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(await env.Db.ScalarAsync<bool>($"SELECT is_deleted FROM inventario.documents WHERE document_id = {id}"));
        Assert.True(File.Exists(Path.Combine(env.Root, key)), "El borrado lógico no debe borrar el archivo físico (regla 20).");
    }

    // ── Entorno de cada test ───────────────────────────────────────────────

    private sealed class FailingCommitInterceptor : DbTransactionInterceptor
    {
        public volatile bool Armed;

        public override ValueTask<InterceptionResult> TransactionCommittingAsync(
            DbTransaction transaction, TransactionEventData eventData, InterceptionResult result,
            CancellationToken cancellationToken = default)
            => Armed
                ? throw new InvalidOperationException("Fallo de commit simulado (test 5.5).")
                : ValueTask.FromResult(result);
    }

    private sealed class Env : IAsyncDisposable
    {
        private readonly string _tempDir;
        private readonly PostgresDatabase _db;
        private readonly ApiFactory _factory;

        private Env(string tempDir, PostgresDatabase db, ApiFactory factory, HttpClient client)
        {
            _tempDir = tempDir;
            _db = db;
            _factory = factory;
            Client = client;
        }

        public PostgresDatabase Db => _db;
        public HttpClient Client { get; }

        // tempDir/storage: la raíz del storage; tempDir (su padre) es el
        // "fuera de la raíz" donde se comprueba que no aparece nada.
        public string Root => Path.Combine(_tempDir, "storage");
        public string Token { get; private set; } = "";
        public long IncidentId { get; private set; }
        public long AcquisitionId { get; private set; }

        public static async Task<Env> CreateAsync(
            Fase55DocumentosTests test, Action<IServiceCollection>? configureTestServices = null)
        {
            var db = await test.CreateDatabaseAsync();
            var tempDir = Path.Combine(Path.GetTempPath(), $"tecnm-5-5-{Guid.NewGuid():N}");
            Directory.CreateDirectory(Path.Combine(tempDir, "storage"));

            var factory = new ApiFactory(
                db.ConnectionString,
                new Dictionary<string, string?> { ["Storage:LocalPath"] = Path.Combine(tempDir, "storage") },
                configureTestServices: configureTestServices);
            var env = new Env(tempDir, db, factory, Fase52PerimetroTests.NewClient(factory));
            await env.SeedAsync();
            return env;
        }

        private async Task SeedAsync()
        {
            var hash = BCrypt.Net.BCrypt.HashPassword(Password, 4);
            var adminId = await _db.ScalarAsync<long>($"""
                INSERT INTO inventario.app_users
                    (username, email, password_hash, first_name, last_name, role_id, status)
                SELECT 'sd55_admin', 'sd55_admin@test.local', '{hash}', 'Usuario', 'Docs', role_id, 'activo'
                FROM inventario.roles WHERE code = 'ADMIN'
                RETURNING user_id
                """);
            var assetId = await _db.ScalarAsync<long>("""
                INSERT INTO inventario.assets (inventory_code, asset_type_id, asset_status_id)
                SELECT 'SD55-A1', at.asset_type_id, s.asset_status_id
                FROM inventario.asset_types at
                CROSS JOIN inventario.asset_statuses s
                WHERE at.code = 'PC' AND s.code = 'CON_FALLA'
                RETURNING asset_id
                """);
            IncidentId = await _db.ScalarAsync<long>($"""
                INSERT INTO inventario.incidents
                    (asset_id, incident_type_id, incident_priority_id, incident_status_id,
                     reported_by, title, created_by)
                SELECT {assetId}, it.incident_type_id, ip.incident_priority_id,
                       ist.incident_status_id, {adminId}, 'Falla ficticia 5.5', {adminId}
                FROM inventario.incident_types it, inventario.incident_priorities ip,
                     inventario.incident_statuses ist
                WHERE it.code = 'HARDWARE' AND ip.code = 'ALTA' AND ist.code = 'ABIERTO'
                RETURNING incident_id
                """);
            AcquisitionId = await _db.ScalarAsync<long>(
                "INSERT INTO inventario.acquisitions (currency_code) VALUES ('MXN') RETURNING acquisition_id");

            var login = await Client.PostAsJsonAsync("/api/auth/login",
                new { usernameOrEmail = "sd55_admin", password = Password });
            Assert.Equal(HttpStatusCode.OK, login.StatusCode);
            var cookie = login.Headers.GetValues("Set-Cookie").First(c => c.StartsWith("tecnm_at=", StringComparison.Ordinal));
            Token = cookie["tecnm_at=".Length..].Split(';')[0];
        }

        public Task<HttpResponseMessage> UploadAsync(
            byte[] bytes, string fileName, string contentType, string ownerType = "INCIDENT", long? ownerId = null)
        {
            var form = new MultipartFormDataContent();
            form.Add(new StringContent(ownerType), "ownerType");
            form.Add(new StringContent((ownerId ?? IncidentId).ToString()), "ownerId");
            var file = new ByteArrayContent(bytes);
            file.Headers.ContentType = MediaTypeHeaderValue.Parse(contentType);
            form.Add(file, "file", fileName);

            var request = new HttpRequestMessage(HttpMethod.Post, "/api/documents") { Content = form };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", Token);
            return Client.SendAsync(request);
        }

        public Task<HttpResponseMessage> DownloadAsync(long documentId)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, $"/api/documents/{documentId}/download");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", Token);
            return Client.SendAsync(request);
        }

        public async Task<long> LastDocumentIdAsync()
            => await _db.ScalarAsync<long>("SELECT max(document_id) FROM inventario.documents");

        public async Task<(string Key, string Mime, string Name)> LastDocumentAsync()
        {
            await using var connection = await _db.OpenConnectionAsync();
            await using var command = new Npgsql.NpgsqlCommand(
                "SELECT storage_key, mime_type, file_name FROM inventario.documents ORDER BY document_id DESC LIMIT 1",
                connection);
            await using var reader = await command.ExecuteReaderAsync();
            Assert.True(await reader.ReadAsync(), "No hay ningún documento guardado.");
            return (reader.GetString(0), reader.GetString(1), reader.GetString(2));
        }

        public async ValueTask DisposeAsync()
        {
            Client.Dispose();
            await _factory.DisposeAsync();
            await _db.DisposeAsync();
            if (Directory.Exists(_tempDir))
            {
                Directory.Delete(_tempDir, recursive: true);
            }
        }
    }
}
