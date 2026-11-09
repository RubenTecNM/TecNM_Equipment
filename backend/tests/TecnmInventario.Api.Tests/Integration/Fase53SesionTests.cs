using System.Data.Common;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using TecnmInventario.Api.Common.Security;
using TecnmInventario.Api.Core.Entities;
using TecnmInventario.Api.Data;
using TecnmInventario.Api.Features.Auth.Refresh;
using TecnmInventario.Api.Features.Auth.Shared;

namespace TecnmInventario.Api.Tests.Integration;

// ═══════════════════════════════════════════════════════════════════════════
// Subcheckpoint 5.3: sesión. Todo por HTTP contra la API real (ApiFactory) y
// el PostgreSQL efímero. Los JWT salen del login real de la API: ningún test
// firma tokens a mano.
//
// Las cookies son Secure (entorno Testing): HttpClient no las reenviaría por
// http, así que se leen de Set-Cookie y se mandan a mano en la cabecera Cookie.
// ═══════════════════════════════════════════════════════════════════════════

[Trait("Category", "Integration")]
public sealed class Fase53SesionTests : PostgresTestBase
{
    private const string Password = "Sd53Correcta!";

    public Fase53SesionTests(PostgresContainerFixture fixture)
        : base(fixture) { }

    // ── S0: login ──────────────────────────────────────────────────────────

    [DockerFact]
    public async Task Login_PasswordDeMasDe72BytesDeUsuarioExistente_Autentica()
    {
        await using var db = await CreateDatabaseAsync();
        var longPassword = "Sd53-" + new string('x', 75); // 80 bytes
        await InsertUserAsync(db, "sd53_larga", password: longPassword);
        await using var factory = new ApiFactory(db.ConnectionString);
        using var client = Fase52PerimetroTests.NewClient(factory);

        var response = await PostLoginAsync(client, "sd53_larga", longPassword);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [DockerFact]
    public async Task Login_MismoIdentificadorConDistintasMayusculas_CompartenContador()
    {
        await using var db = await CreateDatabaseAsync();
        await InsertUserAsync(db, "sd53_juan");
        await using var factory = new ApiFactory(db.ConnectionString, new Dictionary<string, string?>
        {
            ["Security:RateLimiting:AccountMaxFailures"] = "5"
        });
        using var client = Fase52PerimetroTests.NewClient(factory);

        foreach (var variant in new[] { "SD53_JUAN", "Sd53_Juan", "sd53_juan", " SD53_juan ", "sD53_JuAn" })
            Assert.Equal(HttpStatusCode.Unauthorized, (await PostLoginAsync(client, variant, "incorrecta")).StatusCode);

        var locked = await PostLoginAsync(client, "sd53_juan", Password);

        Assert.Equal(HttpStatusCode.TooManyRequests, locked.StatusCode);
    }

    // ── S1: cookies solo después de confirmar ──────────────────────────────

    [DockerFact]
    public async Task Refresh_FallaElCommit_NoEnviaSetCookieYElTokenAnteriorSigueValido()
    {
        await using var db = await CreateDatabaseAsync();
        await InsertUserAsync(db, "sd53_commit");
        var failure = new FailingCommitInterceptor();
        await using var factory = new ApiFactory(db.ConnectionString, configureTestServices: services =>
            services.ConfigureDbContext<TecnmInventarioDbContext>(o => o.AddInterceptors(failure)));
        using var client = Fase52PerimetroTests.NewClient(factory);
        var session = await LoginAsync(client, "sd53_commit");

        failure.Armed = true;
        var failed = await PostRefreshAsync(client, session.RefreshToken);
        failure.Armed = false;

        Assert.Equal(HttpStatusCode.InternalServerError, failed.StatusCode);
        Assert.False(failed.Headers.Contains("Set-Cookie"),
            "Un 500 no debe dejar cookies: " + string.Join(" | ", SetCookies(failed)));

        // El rollback dejó el token anterior sin revocar: el cliente puede reintentar.
        var retry = await PostRefreshAsync(client, session.RefreshToken);
        Assert.Equal(HttpStatusCode.OK, retry.StatusCode);
    }

    // A1b: sin pasar por HTTP. ExceptionHandlingMiddleware borra Set-Cookie en
    // los 500 (defensa en profundidad) y eso tapa en A1 unas cookies escritas
    // antes del commit; aquí se comprueba el orden del handler por sí solo.
    [DockerFact]
    public async Task Refresh_FallaElCommit_ElHandlerNoEscribeCookies()
    {
        await using var db = await CreateDatabaseAsync();
        var userId = await InsertUserAsync(db, "sd53_orden");
        var rawToken = $"sd53-raw-{Guid.NewGuid():N}";
        await db.ExecuteAsync($"""
            INSERT INTO inventario.refresh_tokens (user_id, token_hash, family_id, expires_at)
            VALUES ({userId}, '{AuthSessionIssuer.HashToken(rawToken)}', '{Guid.NewGuid()}', now() + interval '7 days')
            """);

        var failure = new FailingCommitInterceptor { Armed = true };
        await using var context = new TecnmInventarioDbContext(
            new DbContextOptionsBuilder<TecnmInventarioDbContext>()
                .UseNpgsql(db.ConnectionString).AddInterceptors(failure).Options);
        var cookies = new RecordingCookieWriter(rawToken);
        var issuer = new AuthSessionIssuer(
            context, new FakeTokenGenerator(), cookies, new NoCurrentUser(),
            new HttpContextAccessor { HttpContext = new DefaultHttpContext() }, Options.Create(new AuthOptions()));
        var handler = new RefreshSessionHandler(
            context, issuer, cookies, Options.Create(new AuthOptions()), NullLogger<RefreshSessionHandler>.Instance);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => handler.Handle(new RefreshSessionCommand(), CancellationToken.None).AsTask());

        Assert.Equal(0, cookies.Writes);
    }

    [DockerFact]
    public async Task Login_FallaElCommit_NoEnviaSetCookie()
    {
        await using var db = await CreateDatabaseAsync();
        await InsertUserAsync(db, "sd53_logincommit");
        // El login no abre transacción: el fallo se provoca en su SaveChanges.
        var failure = new FailingSaveChangesInterceptor { Armed = true };
        await using var factory = new ApiFactory(db.ConnectionString, configureTestServices: services =>
            services.ConfigureDbContext<TecnmInventarioDbContext>(o => o.AddInterceptors(failure)));
        using var client = Fase52PerimetroTests.NewClient(factory);

        var response = await PostLoginAsync(client, "sd53_logincommit", Password);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.False(response.Headers.Contains("Set-Cookie"),
            "Un 500 no debe dejar cookies: " + string.Join(" | ", SetCookies(response)));
    }

    // ── S2: CSRF ───────────────────────────────────────────────────────────

    [DockerFact]
    public async Task Csrf_AuthorizationNoBearerConCookieDeSesionSinToken_Rechaza()
    {
        await using var db = await CreateDatabaseAsync();
        await InsertUserAsync(db, "sd53_csrf");
        await using var factory = new ApiFactory(db.ConnectionString);
        using var client = Fase52PerimetroTests.NewClient(factory);
        var session = await LoginAsync(client, "sd53_csrf");

        // Cookie de sesión + Authorization que no es Bearer + sin token CSRF.
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/refresh");
        request.Headers.TryAddWithoutValidation("Authorization", "Basic c2Q1Mzp4");
        request.Headers.Add("Cookie", $"tecnm_rt={session.RefreshToken}");

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [DockerFact]
    public async Task Csrf_BearerSinCookiesDeSesion_NoExigeToken()
    {
        await using var db = await CreateDatabaseAsync();
        await InsertUserAsync(db, "sd53_bearer");
        await using var factory = new ApiFactory(db.ConnectionString);
        using var client = Fase52PerimetroTests.NewClient(factory);
        var session = await LoginAsync(client, "sd53_bearer");

        // POST autenticado solo con Bearer (curl, integraciones): sin CSRF.
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/logout");
        request.Headers.Authorization = new AuthenticationHeaderValue("bearer", session.AccessToken);

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    // ── S3: gracia de reúso del refresh ────────────────────────────────────

    [DockerFact]
    public async Task Refresh_ReusoFueraDeLaGracia_RevocaLaFamilia()
    {
        await using var db = await CreateDatabaseAsync();
        var userId = await InsertUserAsync(db, "sd53_reuso");
        await using var factory = new ApiFactory(db.ConnectionString, new Dictionary<string, string?>
        {
            ["Auth:RefreshReuseGraceSeconds"] = "1"
        });
        using var client = Fase52PerimetroTests.NewClient(factory);
        var first = await LoginAsync(client, "sd53_reuso");

        var rotated = await PostRefreshAsync(client, first.RefreshToken);
        Assert.Equal(HttpStatusCode.OK, rotated.StatusCode);
        var second = ReadCookie(rotated, "tecnm_rt");

        await Task.Delay(TimeSpan.FromMilliseconds(1500));

        // Reúso del token ya rotado fuera de la gracia: posible robo.
        Assert.Equal(HttpStatusCode.Unauthorized, (await PostRefreshAsync(client, first.RefreshToken)).StatusCode);

        // Toda la familia queda revocada, incluido el token vigente.
        Assert.Equal(HttpStatusCode.Unauthorized, (await PostRefreshAsync(client, second)).StatusCode);
        Assert.Equal(0, await db.CountAsync("refresh_tokens", $"user_id = {userId} AND revoked_at IS NULL"));
    }

    // ── S4: el estado del usuario se comprueba en cada petición ────────────

    [DockerFact]
    public async Task TokenVigente_UsuarioDesactivado_Devuelve401()
    {
        await using var db = await CreateDatabaseAsync();
        var userId = await InsertUserAsync(db, "sd53_desactivado");
        await using var factory = NoSessionCache(db);
        using var client = Fase52PerimetroTests.NewClient(factory);
        var session = await LoginAsync(client, "sd53_desactivado");

        await db.ExecuteAsync($"UPDATE inventario.app_users SET status = 'inactivo' WHERE user_id = {userId}");

        Assert.Equal(HttpStatusCode.Unauthorized, (await GetAssetsWithBearerAsync(client, session)).StatusCode);
    }

    [DockerFact]
    public async Task TokenVigente_UsuarioBorradoLogicamente_Devuelve401()
    {
        await using var db = await CreateDatabaseAsync();
        var userId = await InsertUserAsync(db, "sd53_borrado");
        await using var factory = NoSessionCache(db);
        using var client = Fase52PerimetroTests.NewClient(factory);
        var session = await LoginAsync(client, "sd53_borrado");

        await db.ExecuteAsync($"UPDATE inventario.app_users SET is_deleted = true WHERE user_id = {userId}");

        Assert.Equal(HttpStatusCode.Unauthorized, (await GetAssetsWithBearerAsync(client, session)).StatusCode);
    }

    [DockerFact]
    public async Task TokenVigente_RolCambiado_Devuelve401()
    {
        await using var db = await CreateDatabaseAsync();
        var userId = await InsertUserAsync(db, "sd53_rol");
        await using var factory = NoSessionCache(db);
        using var client = Fase52PerimetroTests.NewClient(factory);
        var session = await LoginAsync(client, "sd53_rol");

        // TECNICO también tiene assets.view: sin la comprobación, el claim
        // ADMIN viejo seguiría abriendo la petición.
        await db.ExecuteAsync($"""
            UPDATE inventario.app_users
            SET role_id = (SELECT role_id FROM inventario.roles WHERE code = 'TECNICO')
            WHERE user_id = {userId}
            """);

        // Esta vez por la cookie tecnm_at (S4 aplica igual que con Bearer).
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/assets");
        request.Headers.Add("Cookie", $"tecnm_at={session.AccessToken}");

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.SendAsync(request)).StatusCode);
    }

    [DockerFact]
    public async Task TokenVigente_UsuarioSinCambios_Devuelve200()
    {
        await using var db = await CreateDatabaseAsync();
        await InsertUserAsync(db, "sd53_igual");
        await using var factory = NoSessionCache(db);
        using var client = Fase52PerimetroTests.NewClient(factory);
        var session = await LoginAsync(client, "sd53_igual");

        Assert.Equal(HttpStatusCode.OK, (await GetAssetsWithBearerAsync(client, session)).StatusCode);

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/assets");
        request.Headers.Add("Cookie", $"tecnm_at={session.AccessToken}");
        Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(request)).StatusCode);
    }

    // ── S5: Register exige users.manage, no el rol ADMIN ───────────────────

    [DockerFact]
    public async Task Register_UsuarioSinPermisoUsersManage_Devuelve403()
    {
        await using var db = await CreateDatabaseAsync();
        var roleId = await InsertRoleAsync(db, "SD53_SINPERM");
        await InsertUserAsync(db, "sd53_sinperm", roleId: roleId);
        await using var factory = new ApiFactory(db.ConnectionString);
        using var client = Fase52PerimetroTests.NewClient(factory);
        var session = await LoginAsync(client, "sd53_sinperm");

        var response = await PostRegisterAsync(client, session, "sd53_nuevo_a");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // Register responde 200 (Ok), no 201: el nombre refleja el código real.
    [DockerFact]
    public async Task Register_RolNoAdminConPermisoUsersManage_Devuelve200()
    {
        await using var db = await CreateDatabaseAsync();
        var roleId = await InsertRoleAsync(db, "SD53_GESTOR");
        var granted = await db.ScalarAsync<long>($"""
            WITH ins AS (
                INSERT INTO inventario.role_permissions (role_id, permission_id)
                SELECT {roleId}, permission_id FROM inventario.permissions WHERE code = 'users.manage'
                RETURNING 1)
            SELECT count(*) FROM ins
            """);
        Assert.True(granted == 1, "El permiso users.manage no existe en la plantilla de tests.");
        await InsertUserAsync(db, "sd53_gestor", roleId: roleId);
        await using var factory = new ApiFactory(db.ConnectionString);
        using var client = Fase52PerimetroTests.NewClient(factory);
        var session = await LoginAsync(client, "sd53_gestor");

        var response = await PostRegisterAsync(client, session, "sd53_nuevo_b");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [DockerFact]
    public async Task Register_AdminSinPermisoUsersManage_Devuelve403()
    {
        await using var db = await CreateDatabaseAsync();
        await db.ExecuteAsync("""
            DELETE FROM inventario.role_permissions
            WHERE role_id = (SELECT role_id FROM inventario.roles WHERE code = 'ADMIN')
              AND permission_id IN (SELECT permission_id FROM inventario.permissions WHERE code = 'users.manage')
            """);
        await InsertUserAsync(db, "sd53_admin");
        await using var factory = new ApiFactory(db.ConnectionString);
        using var client = Fase52PerimetroTests.NewClient(factory);
        var session = await LoginAsync(client, "sd53_admin");

        var response = await PostRegisterAsync(client, session, "sd53_nuevo_c");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // ── Helpers ────────────────────────────────────────────────────────────

    private sealed record Session(string AccessToken, string RefreshToken);

    private static ApiFactory NoSessionCache(PostgresDatabase db)
        => new(db.ConnectionString, new Dictionary<string, string?>
        {
            ["Security:SessionValidation:CacheSeconds"] = "0"
        });

    private static async Task<long> InsertRoleAsync(PostgresDatabase db, string code)
        => await db.ScalarAsync<long>(
            $"INSERT INTO inventario.roles (code, name) VALUES ('{code}', 'Rol {code}') RETURNING role_id");

    private static async Task<long> InsertUserAsync(
        PostgresDatabase db, string username, long? roleId = null, string password = Password)
    {
        var hash = BCrypt.Net.BCrypt.HashPassword(password, 4);
        var role = roleId?.ToString() ?? "(SELECT role_id FROM inventario.roles WHERE code = 'ADMIN')";
        return await db.ScalarAsync<long>($"""
            INSERT INTO inventario.app_users
                (username, email, password_hash, first_name, last_name, role_id, status)
            VALUES ('{username}', '{username}@test.local', '{hash}', 'Usuario', 'Sesion', {role}, 'activo')
            RETURNING user_id
            """);
    }

    private static Task<HttpResponseMessage> PostLoginAsync(HttpClient client, string usernameOrEmail, string password)
        => client.PostAsJsonAsync("/api/auth/login", new { usernameOrEmail, password });

    private static async Task<Session> LoginAsync(HttpClient client, string username)
    {
        var response = await PostLoginAsync(client, username, Password);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return new Session(ReadCookie(response, "tecnm_at"), ReadCookie(response, "tecnm_rt"));
    }

    // Double-submit: basta con que cookie y cabecera CSRF coincidan.
    private static Task<HttpResponseMessage> PostRefreshAsync(HttpClient client, string refreshToken)
    {
        var csrf = Guid.NewGuid().ToString("N");
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/refresh");
        request.Headers.Add("Cookie", $"tecnm_rt={refreshToken}; tecnm_csrf={csrf}");
        request.Headers.Add("X-CSRF-Token", csrf);
        return client.SendAsync(request);
    }

    private static Task<HttpResponseMessage> GetAssetsWithBearerAsync(HttpClient client, Session session)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/assets");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", session.AccessToken);
        return client.SendAsync(request);
    }

    private static Task<HttpResponseMessage> PostRegisterAsync(HttpClient client, Session session, string username)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/register")
        {
            Content = JsonContent.Create(new
            {
                username,
                email = $"{username}@test.local",
                password = "ClaveSegura123!",
                firstName = "Nombre",
                lastName = "Apellido",
                roleCode = "TECNICO"
            })
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", session.AccessToken);
        return client.SendAsync(request);
    }

    private static IEnumerable<string> SetCookies(HttpResponseMessage response)
        => response.Headers.TryGetValues("Set-Cookie", out var values) ? values : [];

    private static string ReadCookie(HttpResponseMessage response, string name)
    {
        var header = SetCookies(response).FirstOrDefault(c => c.StartsWith(name + "=", StringComparison.Ordinal))
            ?? throw new InvalidOperationException($"La respuesta no trae la cookie {name}.");
        return header[(name.Length + 1)..].Split(';')[0];
    }

    private sealed class RecordingCookieWriter : IAuthCookieWriter
    {
        private readonly string _refreshToken;

        public RecordingCookieWriter(string refreshToken) => _refreshToken = refreshToken;

        public int Writes { get; private set; }

        public void Write(string accessToken, DateTime accessExpiresAt, string refreshToken, DateTime refreshExpiresAt)
            => Writes++;

        public void Clear() { }

        public string? ReadRefreshToken() => _refreshToken;
    }

    // No firma nada: el orden de las cookies no depende del contenido del JWT.
    private sealed class FakeTokenGenerator : IJwtTokenGenerator
    {
        public (string Token, DateTime ExpiresAt) GenerateToken(AppUser user)
            => ("token-de-prueba-sin-firma", DateTime.UtcNow.AddMinutes(15));
    }

    private sealed class NoCurrentUser : ICurrentUserService
    {
        public long? UserId => null;
        public string? Username => null;
        public string? RoleCode => null;
        public System.Net.IPAddress? IpAddress => null;
    }

    // Fallos de BD simulados SOLO en tests (se registran desde ApiFactory).
    private sealed class FailingCommitInterceptor : DbTransactionInterceptor
    {
        public volatile bool Armed;

        public override ValueTask<InterceptionResult> TransactionCommittingAsync(
            DbTransaction transaction, TransactionEventData eventData, InterceptionResult result,
            CancellationToken cancellationToken = default)
            => Armed
                ? throw new InvalidOperationException("Fallo de commit simulado (test 5.3).")
                : ValueTask.FromResult(result);
    }

    private sealed class FailingSaveChangesInterceptor : SaveChangesInterceptor
    {
        public volatile bool Armed;

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
            => Armed
                ? throw new InvalidOperationException("Fallo de SaveChanges simulado (test 5.3).")
                : ValueTask.FromResult(result);
    }
}
