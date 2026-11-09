using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace TecnmInventario.Api.Tests.Integration;

// ═══════════════════════════════════════════════════════════════════════════
// Subcheckpoint 5.2: perímetro. Todo pasa por la API real (ApiFactory sobre
// Program.cs) contra el PostgreSQL efímero de Testcontainers.
//
// IPs de documentación (RFC 5737), nunca reales:
//   198.51.100.10 = proxy inverso; 203.0.113.7 = cliente detrás del proxy.
// ═══════════════════════════════════════════════════════════════════════════

[Trait("Category", "Integration")]
public sealed class Fase52PerimetroTests : PostgresTestBase
{
    private const string ProxyIp = "198.51.100.10";
    private const string ClientIp = "203.0.113.7";
    private const string Password = "Sd52Correcta!";

    public Fase52PerimetroTests(PostgresContainerFixture fixture)
        : base(fixture) { }

    [DockerFact]
    public async Task Login_SuperaLimitePorIp_Devuelve429ConRetryAfter()
    {
        await using var db = await CreateDatabaseAsync();
        await using var factory = new ApiFactory(db.ConnectionString, new Dictionary<string, string?>
        {
            ["Security:RateLimiting:LoginPermitLimit"] = "3"
        });
        using var client = NewClient(factory);

        // Identificadores distintos: solo cuenta el límite por IP.
        for (var i = 0; i < 3; i++)
        {
            var allowed = await PostLoginAsync(client, $"sd52_ip_{i}", "incorrecta");
            Assert.Equal(HttpStatusCode.Unauthorized, allowed.StatusCode);
        }

        var rejected = await PostLoginAsync(client, "sd52_ip_x", "incorrecta");

        Assert.Equal(HttpStatusCode.TooManyRequests, rejected.StatusCode);
        Assert.True(rejected.Headers.RetryAfter?.Delta > TimeSpan.Zero, "Falta Retry-After");
        Assert.Equal("application/problem+json", rejected.Content.Headers.ContentType?.MediaType);
        var problem = await ReadProblemAsync(rejected);
        Assert.Equal(429, problem.GetProperty("status").GetInt32());
    }

    [DockerFact]
    public async Task Login_CincoFallosMismoIdentificador_SextoIntentoDevuelve429AunqueLaContraseñaSeaCorrecta()
    {
        await using var db = await CreateDatabaseAsync();
        await InsertUserAsync(db, "sd52_cuenta");
        await using var factory = new ApiFactory(db.ConnectionString, AccountLimit(5));
        using var client = NewClient(factory);

        for (var i = 0; i < 5; i++)
        {
            var failed = await PostLoginAsync(client, "sd52_cuenta", "incorrecta");
            Assert.Equal(HttpStatusCode.Unauthorized, failed.StatusCode);
        }

        var locked = await PostLoginAsync(client, "sd52_cuenta", Password);

        Assert.Equal(HttpStatusCode.TooManyRequests, locked.StatusCode);
        Assert.True(locked.Headers.RetryAfter?.Delta > TimeSpan.Zero, "Falta Retry-After");
    }

    [DockerFact]
    public async Task Login_IdentificadorInexistente_TambienSeBloquea()
    {
        await using var db = await CreateDatabaseAsync();
        await InsertUserAsync(db, "sd52_existe");
        await using var factory = new ApiFactory(db.ConnectionString, AccountLimit(5));
        using var client = NewClient(factory);

        for (var i = 0; i < 5; i++)
        {
            await PostLoginAsync(client, "sd52_no_existe", "incorrecta");
            await PostLoginAsync(client, "sd52_existe", "incorrecta");
        }

        var lockedMissing = await PostLoginAsync(client, "sd52_no_existe", "incorrecta");
        var lockedExisting = await PostLoginAsync(client, "sd52_existe", Password);

        // Sin enumeración: misma respuesta exista o no la cuenta.
        Assert.Equal(HttpStatusCode.TooManyRequests, lockedMissing.StatusCode);
        Assert.Equal(lockedExisting.StatusCode, lockedMissing.StatusCode);
        var missingDetail = (await ReadProblemAsync(lockedMissing)).GetProperty("detail").GetString();
        var existingDetail = (await ReadProblemAsync(lockedExisting)).GetProperty("detail").GetString();
        Assert.False(string.IsNullOrEmpty(missingDetail));
        Assert.Equal(existingDetail, missingDetail);
    }

    [DockerFact]
    public async Task Login_ExitoReiniciaElContador()
    {
        await using var db = await CreateDatabaseAsync();
        await InsertUserAsync(db, "sd52_reinicio");
        await using var factory = new ApiFactory(db.ConnectionString, AccountLimit(5));
        using var client = NewClient(factory);

        for (var i = 0; i < 4; i++)
            await PostLoginAsync(client, "sd52_reinicio", "incorrecta");

        Assert.Equal(HttpStatusCode.OK, (await PostLoginAsync(client, "sd52_reinicio", Password)).StatusCode);

        // Sin reinicio serían 8 fallos acumulados y el siguiente login daría 429.
        for (var i = 0; i < 4; i++)
            await PostLoginAsync(client, "sd52_reinicio", "incorrecta");

        Assert.Equal(HttpStatusCode.OK, (await PostLoginAsync(client, "sd52_reinicio", Password)).StatusCode);
    }

    [DockerFact]
    public async Task ForwardedHeaders_ProxyConfigurado_UsaLaIpDelCliente()
    {
        await using var db = await CreateDatabaseAsync();
        var userId = await InsertUserAsync(db, "sd52_proxy");
        await using var factory = new ApiFactory(db.ConnectionString, new Dictionary<string, string?>
        {
            ["ForwardedHeaders:KnownProxies:0"] = ProxyIp
        }, clientIp: ProxyIp);
        using var client = NewClient(factory);

        var response = await PostLoginAsync(client, "sd52_proxy", Password, forwardedFor: ClientIp);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(ClientIp, await CreatedIpAsync(db, userId));
    }

    [DockerFact]
    public async Task ForwardedHeaders_ProxyNoConfigurado_IgnoraXForwardedFor()
    {
        await using var db = await CreateDatabaseAsync();
        var userId = await InsertUserAsync(db, "sd52_sinproxy");
        await using var factory = new ApiFactory(db.ConnectionString, clientIp: ProxyIp);
        using var client = NewClient(factory);

        var response = await PostLoginAsync(client, "sd52_sinproxy", Password, forwardedFor: ClientIp);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(ProxyIp, await CreatedIpAsync(db, userId));
    }

    // ── Helpers ────────────────────────────────────────────────────────────

    private static Dictionary<string, string?> AccountLimit(int maxFailures) => new()
    {
        ["Security:RateLimiting:AccountMaxFailures"] = maxFailures.ToString()
    };

    internal static HttpClient NewClient(ApiFactory factory) => factory.CreateClient(
        new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = false });

    private static async Task<HttpResponseMessage> PostLoginAsync(
        HttpClient client, string usernameOrEmail, string password, string? forwardedFor = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/login")
        {
            Content = JsonContent.Create(new { usernameOrEmail, password })
        };
        if (forwardedFor is not null)
            request.Headers.Add("X-Forwarded-For", forwardedFor);

        return await client.SendAsync(request);
    }

    private static async Task<JsonElement> ReadProblemAsync(HttpResponseMessage response)
        => JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;

    private static async Task<long> InsertUserAsync(PostgresDatabase db, string username)
    {
        var hash = BCrypt.Net.BCrypt.HashPassword(Password, 4);
        return await db.ScalarAsync<long>($"""
            INSERT INTO inventario.app_users
                (username, email, password_hash, first_name, last_name, role_id, status)
            SELECT '{username}', '{username}@test.local', '{hash}', 'Usuario', 'Perimetro', role_id, 'activo'
            FROM inventario.roles WHERE code = 'ADMIN'
            RETURNING user_id
            """);
    }

    private static Task<string> CreatedIpAsync(PostgresDatabase db, long userId)
        => db.ScalarAsync<string>(
            $"SELECT host(created_ip) FROM inventario.refresh_tokens WHERE user_id = {userId} ORDER BY refresh_token_id DESC LIMIT 1");
}

/// <summary>Casos del perímetro que no tocan la base de datos.</summary>
public sealed class Fase52PerimetroSinBdTests
{
    [Fact]
    public async Task Refresh_SuperaLimitePorIp_Devuelve429()
    {
        await using var factory = new ApiFactory(overrides: new Dictionary<string, string?>
        {
            ["Security:RateLimiting:RefreshPermitLimit"] = "3"
        });
        using var client = Fase52PerimetroTests.NewClient(factory);

        // Sin cookie: el handler responde 401 sin consultar la BD.
        for (var i = 0; i < 3; i++)
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsync("/api/auth/refresh", null)).StatusCode);

        var rejected = await client.PostAsync("/api/auth/refresh", null);

        Assert.Equal(HttpStatusCode.TooManyRequests, rejected.StatusCode);
        Assert.True(rejected.Headers.RetryAfter?.Delta > TimeSpan.Zero, "Falta Retry-After");
    }

    [Theory]
    [InlineData("/api/auth/providers")] // 200, anónimo
    [InlineData("/api/assets")]         // 401, protegido
    public async Task Respuestas_IncluyenCabecerasDeSeguridad(string path)
    {
        await using var factory = new ApiFactory();
        using var client = Fase52PerimetroTests.NewClient(factory);

        var response = await client.GetAsync(path);

        Assert.Equal("nosniff", Header(response, "X-Content-Type-Options"));
        Assert.Equal("DENY", Header(response, "X-Frame-Options"));
        Assert.Equal("no-referrer", Header(response, "Referrer-Policy"));
    }

    [Fact]
    public async Task EndpointsAuth_IncluyenCacheControlNoStore()
    {
        await using var factory = new ApiFactory();
        using var client = Fase52PerimetroTests.NewClient(factory);

        var providers = await client.GetAsync("/api/auth/providers");
        var refresh = await client.PostAsync("/api/auth/refresh", null); // 401

        Assert.True(providers.Headers.CacheControl?.NoStore, "providers sin no-store");
        Assert.True(refresh.Headers.CacheControl?.NoStore, "refresh (401) sin no-store");
    }

    private static string? Header(HttpResponseMessage response, string name)
        => response.Headers.TryGetValues(name, out var values) ? string.Join(",", values) : null;
}
