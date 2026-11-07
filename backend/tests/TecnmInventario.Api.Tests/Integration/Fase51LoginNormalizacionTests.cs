using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using TecnmInventario.Api.Common.Audit;
using TecnmInventario.Api.Common.Security;
using TecnmInventario.Api.Core.Entities;
using TecnmInventario.Api.Features.Auth.ExternalLogin;
using TecnmInventario.Api.Features.Auth.Login;
using TecnmInventario.Api.Features.Auth.Register;
using TecnmInventario.Api.Features.Auth.Shared;

namespace TecnmInventario.Api.Tests.Integration;

// ═══════════════════════════════════════════════════════════════════════════
// Subcheckpoint 5.1: login externo + normalización de usuario/correo.
//
// Decisiones cubiertas: N1 (Register normaliza y compara en minúsculas),
// N2 (login por email SOLO si hay '@'; si no, SOLO por username), N3 (username
// sin '@'), N4 (más de un candidato -> rechazo), N5 (dominios permitidos para
// Microsoft, comparación exacta).
//
// Los casos que el validador ya no permite (duplicados por mayúsculas, username
// con '@') se insertan por SQL para simular datos históricos.
//
// Los fakes de autenticación son copia de los de Fase4d1UsuariosRolesReproTests
// (allí son privados); ninguno firma JWT.
// ═══════════════════════════════════════════════════════════════════════════

[Trait("Category", "Integration")]
public sealed class Fase51LoginNormalizacionTests : PostgresTestBase
{
    private const string AllowedDomain = "ejemplo.edu.mx";
    private const string Password = "Sd51Correcta!";
    private const string OtherPassword = "Sd51Otra!";

    public Fase51LoginNormalizacionTests(PostgresContainerFixture fixture)
        : base(fixture) { }

    // ── Register ───────────────────────────────────────────────────────────

    // Un validador inválido se traduce en 400 por ValidationBehavior
    // (cubierto en ValidationBehaviorTests); aquí se prueba la regla.
    [DockerFact]
    public async Task Register_UsernameQueSoloDifiereEnMayusculas_Rechaza400()
    {
        await using var db = await CreateDatabaseAsync();
        await InsertUserAsync(db, "sd51_pedro", "sd51-pedro@test.local", "NO_LOGIN");

        await using var context = db.CreateContext();
        var result = await new RegisterValidator(context).ValidateAsync(
            NewRegister("SD51_Pedro", "sd51-otro@test.local"));

        Assert.Contains(result.Errors, e => e.PropertyName == nameof(RegisterCommand.Username));
    }

    [DockerFact]
    public async Task Register_EmailQueSoloDifiereEnMayusculas_Rechaza400()
    {
        await using var db = await CreateDatabaseAsync();
        await InsertUserAsync(db, "sd51_luisa", "sd51-luisa@test.local", "NO_LOGIN");

        await using var context = db.CreateContext();
        var result = await new RegisterValidator(context).ValidateAsync(
            NewRegister("sd51_luisa2", "SD51-Luisa@Test.Local"));

        Assert.Contains(result.Errors, e => e.PropertyName == nameof(RegisterCommand.Email));
    }

    [DockerFact]
    public async Task Register_UsernameConArroba_Rechaza400()
    {
        await using var db = await CreateDatabaseAsync();

        await using var context = db.CreateContext();
        var result = await new RegisterValidator(context).ValidateAsync(
            NewRegister("sd51@arroba", "sd51-arroba@test.local"));

        Assert.Contains(result.Errors, e => e.PropertyName == nameof(RegisterCommand.Username));
    }

    [DockerFact]
    public async Task Register_GuardaUsernameYEmailEnMinusculas()
    {
        await using var db = await CreateDatabaseAsync();

        await using var context = db.CreateContext();
        var handler = new RegisterHandler(
            context, new FakeCurrentUserWithId(1), new AuditLogService(context, new FakeCurrentUserWithId(1)));

        var response = await handler.Handle(
            NewRegister("  SD51_Nuevo ", " SD51-Nuevo@Test.LOCAL  "), CancellationToken.None);

        var stored = await db.ScalarAsync<string>(
            $"SELECT username || '|' || email FROM inventario.app_users WHERE user_id = {response.UserId}");
        Assert.Equal("sd51_nuevo|sd51-nuevo@test.local", stored);
    }

    // ── Login ──────────────────────────────────────────────────────────────

    [DockerFact]
    public async Task Login_UsernameConOtrasMayusculas_Autentica()
    {
        await using var db = await CreateDatabaseAsync();
        var userId = await InsertUserAsync(db, "sd51_maria", "sd51-maria@test.local", Hash(Password));

        var profile = await LoginAsync(db, "SD51_Maria", Password);

        Assert.Equal(userId, profile.UserId);
    }

    [DockerFact]
    public async Task Login_EmailConOtrasMayusculas_Autentica()
    {
        await using var db = await CreateDatabaseAsync();
        var userId = await InsertUserAsync(db, "sd51_jorge", "sd51-jorge@test.local", Hash(Password));

        var profile = await LoginAsync(db, "SD51-Jorge@Test.Local", Password);

        Assert.Equal(userId, profile.UserId);
    }

    [DockerFact]
    public async Task Login_UsernameIgualAlEmailDeOtroUsuario_NoAutenticaComoEseOtro()
    {
        await using var db = await CreateDatabaseAsync();
        const string victimEmail = "sd51-victima@test.local";

        // El atacante se inserta PRIMERO (user_id menor) y su username es el
        // correo de la víctima: caso cruzado que el validador ya no permite.
        await InsertUserAsync(db, victimEmail, "sd51-atacante@test.local", Hash(OtherPassword));
        var victimId = await InsertUserAsync(db, "sd51_victima", victimEmail, Hash(Password));

        // Con '@' solo se busca por email: el valor identifica a la víctima.
        var victim = await LoginAsync(db, victimEmail, Password);
        Assert.Equal(victimId, victim.UserId);

        // La contraseña del atacante con ese mismo valor no abre su cuenta.
        var ex = await Assert.ThrowsAsync<UnauthorizedAccessException>(() => LoginAsync(db, victimEmail, OtherPassword));
        Assert.Equal("Credenciales inválidas.", ex.Message);
    }

    [DockerFact]
    public async Task Login_DosCuentasQueSoloDifierenEnMayusculas_Rechaza401()
    {
        await using var db = await CreateDatabaseAsync();
        var lowerId = await InsertUserAsync(db, "sd51_dup", "sd51-dup1@test.local", Hash(Password));
        var upperId = await InsertUserAsync(db, "SD51_Dup", "sd51-dup2@test.local", Hash(Password));

        var logger = new ListLogger<LoginHandler>();
        var ex = await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => LoginAsync(db, "sd51_dup", Password, logger));

        Assert.Equal("Credenciales inválidas.", ex.Message);

        // N4: warning con los user_id afectados y sin datos personales.
        var warning = Assert.Single(logger.Entries, e => e.Level == LogLevel.Warning);
        Assert.Contains(lowerId.ToString(), warning.Message);
        Assert.Contains(upperId.ToString(), warning.Message);
        Assert.DoesNotContain("@", warning.Message);
        Assert.DoesNotContain("sd51_dup", warning.Message, StringComparison.OrdinalIgnoreCase);
    }

    // ── Login externo ──────────────────────────────────────────────────────

    [DockerFact]
    public async Task ExternalLogin_Microsoft_DominioNoPermitido_RedirigeDominioNoPermitidoYNoVincula()
    {
        await using var db = await CreateDatabaseAsync();
        const string email = "sd51-ext@ajeno.example.com";
        await InsertUserAsync(db, "sd51_ext_ajeno", email, "NO_LOGIN");

        var linksBefore = await db.CountAsync("user_external_logins");

        var redirect = await ExternalCallbackAsync(db, AuthCookies.MicrosoftScheme, email);

        Assert.Contains("error=dominio_no_permitido", redirect);
        Assert.Equal(linksBefore, await db.CountAsync("user_external_logins"));
    }

    [DockerFact]
    public async Task ExternalLogin_Microsoft_DominioParecidoPeroDistinto_NoVincula()
    {
        await using var db = await CreateDatabaseAsync();
        var email = $"sd51-evil@evil-{AllowedDomain}";
        await InsertUserAsync(db, "sd51_ext_evil", email, "NO_LOGIN");

        var linksBefore = await db.CountAsync("user_external_logins");

        var redirect = await ExternalCallbackAsync(db, AuthCookies.MicrosoftScheme, email);

        Assert.Contains("error=dominio_no_permitido", redirect);
        Assert.Equal(linksBefore, await db.CountAsync("user_external_logins"));
    }

    [DockerFact]
    public async Task ExternalLogin_DosCandidatosPorMayusculas_NoVincula()
    {
        await using var db = await CreateDatabaseAsync();
        var firstId = await InsertUserAsync(db, "sd51_dos_a", $"sd51-dos@{AllowedDomain}", "NO_LOGIN");
        var secondId = await InsertUserAsync(db, "sd51_dos_b", $"SD51-Dos@{AllowedDomain}", "NO_LOGIN");

        var linksBefore = await db.CountAsync("user_external_logins");
        var logger = new ListLogger<ExternalLoginCallbackHandler>();

        var redirect = await ExternalCallbackAsync(
            db, AuthCookies.MicrosoftScheme, $"sd51-dos@{AllowedDomain}", logger);

        Assert.Contains("error=sin_cuenta", redirect);
        Assert.Equal(linksBefore, await db.CountAsync("user_external_logins"));

        var warning = Assert.Single(logger.Entries, e => e.Level == LogLevel.Warning);
        Assert.Contains(firstId.ToString(), warning.Message);
        Assert.Contains(secondId.ToString(), warning.Message);
        Assert.DoesNotContain("@", warning.Message);
    }

    // ── Helpers ────────────────────────────────────────────────────────────

    private static RegisterCommand NewRegister(string username, string email)
        => new(username, email, "ClaveSegura123!", "Nombre", "Apellido", "ADMIN");

    // Coste 4: los tests no miden bcrypt, solo necesitan un hash válido.
    private static string Hash(string password) => BCrypt.Net.BCrypt.HashPassword(password, 4);

    private static async Task<long> InsertUserAsync(PostgresDatabase db, string username, string email, string passwordHash)
    {
        var roleId = await db.ScalarAsync<long>("SELECT role_id FROM inventario.roles WHERE code = 'ADMIN'");
        return await db.ScalarAsync<long>($"""
            INSERT INTO inventario.app_users
                (username, email, password_hash, first_name, last_name, role_id, status)
            VALUES
                ('{username}', '{email}', '{passwordHash}', 'Usuario', 'Prueba', {roleId}, 'activo')
            RETURNING user_id
            """);
    }

    private static async Task<AuthUserResponse> LoginAsync(
        PostgresDatabase db, string usernameOrEmail, string password, ILogger<LoginHandler>? logger = null)
    {
        await using var context = db.CreateContext();
        var handler = new LoginHandler(
            context, new FakeAuthSessionIssuer(), logger ?? NullLogger<LoginHandler>.Instance);
        return await handler.Handle(new LoginCommand(usernameOrEmail, password), CancellationToken.None);
    }

    private static async Task<string> ExternalCallbackAsync(
        PostgresDatabase db, string provider, string email,
        ILogger<ExternalLoginCallbackHandler>? logger = null)
    {
        await using var context = db.CreateContext();
        var httpContext = BuildExternalLoginHttpContext($"sd51-sub-{Guid.NewGuid():N}", email, provider);
        var options = new AuthOptions
        {
            SpaBaseUrl = "http://spa.test.local",
            AllowedEmailDomains = [AllowedDomain]
        };

        var handler = new ExternalLoginCallbackHandler(
            context,
            new HttpContextAccessorStub(httpContext),
            new FakeAuthSessionIssuer(),
            new AuditLogService(context, new FakeCurrentUserWithId(1)),
            Options.Create(options),
            logger ?? NullLogger<ExternalLoginCallbackHandler>.Instance);

        var result = await handler.Handle(new ExternalLoginCallbackCommand(null), CancellationToken.None);
        return result.RedirectUrl;
    }

    private static HttpContext BuildExternalLoginHttpContext(string subject, string email, string provider)
    {
        var claims = new List<Claim>
        {
            new("sub", subject),
            new("oid", subject),
            new("email", email),
            new("email_verified", "true"),
        };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "test"));

        var properties = new AuthenticationProperties();
        properties.Items[".AuthScheme"] = provider;
        var ticket = new AuthenticationTicket(principal, properties, AuthCookies.ExternalScheme);

        var services = new ServiceCollection();
        services.AddSingleton<IAuthenticationService>(
            new FakeAuthenticationService(AuthenticateResult.Success(ticket)));

        return new DefaultHttpContext { RequestServices = services.BuildServiceProvider() };
    }

    // Emite una sesión ficticia (sin JWT ni cookies) para que un login
    // aceptado devuelva el perfil del usuario autenticado.
    private sealed class FakeAuthSessionIssuer : IAuthSessionIssuer
    {
        public Task<IssuedSession> IssueAsync(AppUser user, Guid? familyId, CancellationToken cancellationToken)
            => Task.FromResult(new IssuedSession(new RefreshToken { UserId = user.UserId }, Profile(user)));

        public Task<AuthUserResponse> BuildProfileAsync(AppUser user, CancellationToken cancellationToken)
            => Task.FromResult(Profile(user));

        private static AuthUserResponse Profile(AppUser user) => new(
            user.UserId, user.Username, user.Email, user.FirstName, user.LastName,
            user.Role.Code, user.Role.Name, []);
    }

    private sealed class HttpContextAccessorStub : IHttpContextAccessor
    {
        public HttpContextAccessorStub(HttpContext httpContext) => HttpContext = httpContext;

        public HttpContext? HttpContext { get; set; }
    }

    private sealed class FakeAuthenticationService : IAuthenticationService
    {
        private readonly AuthenticateResult _result;

        public FakeAuthenticationService(AuthenticateResult result) => _result = result;

        public Task<AuthenticateResult> AuthenticateAsync(HttpContext context, string? scheme)
            => Task.FromResult(_result);

        public Task ChallengeAsync(HttpContext context, string? scheme, AuthenticationProperties? properties)
            => throw new NotSupportedException();

        public Task ForbidAsync(HttpContext context, string? scheme, AuthenticationProperties? properties)
            => throw new NotSupportedException();

        public Task SignInAsync(HttpContext context, string? scheme, ClaimsPrincipal principal, AuthenticationProperties? properties)
            => throw new NotSupportedException();

        public Task SignOutAsync(HttpContext context, string? scheme, AuthenticationProperties? properties)
            => Task.CompletedTask;
    }

    private sealed class FakeCurrentUserWithId : ICurrentUserService
    {
        public FakeCurrentUserWithId(long userId) => UserId = userId;

        public long? UserId { get; }
        public string? Username => "sd51";
        public string? RoleCode => "ADMIN";
        public System.Net.IPAddress? IpAddress => null;
    }

    private sealed class ListLogger<T> : ILogger<T>
    {
        public List<(LogLevel Level, string Message)> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
            => Entries.Add((logLevel, formatter(state, exception)));
    }
}
