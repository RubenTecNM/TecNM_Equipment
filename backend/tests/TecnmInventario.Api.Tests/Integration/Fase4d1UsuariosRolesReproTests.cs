using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using TecnmInventario.Api.Common.Audit;
using TecnmInventario.Api.Common.Authorization;
using TecnmInventario.Api.Common.Errors;
using TecnmInventario.Api.Common.Security;
using TecnmInventario.Api.Core.Entities;
using TecnmInventario.Api.Data;
using TecnmInventario.Api.Features.Auth.ExternalLogin;
using TecnmInventario.Api.Features.Auth.GetMe;
using TecnmInventario.Api.Features.Auth.Login;
using TecnmInventario.Api.Features.Incidents.AssignIncident;
using TecnmInventario.Api.Features.Auth.Refresh;
using TecnmInventario.Api.Features.Auth.Shared;
using TecnmInventario.Api.Features.Roles.AssignPermissions;
using TecnmInventario.Api.Features.Users.GetUsers;

namespace TecnmInventario.Api.Tests.Integration;

// ═══════════════════════════════════════════════════════════════════════════
// Subcheckpoint 4d.1 — análisis + tests, SIN tocar código de producción.
//
// (g) No hay ningún test de HISTORIAL nuevo: las 4 consultas de historial que
//   navegan AppUser como FK requerida (GetAssetMovements, GetAssetAssignments,
//   GetIncidents, GetIncidentById) YA usan IgnoreQueryFilters(["SoftDelete"])
//   desde las fases 4a/4b. Ver informe, tarea 3.
//
// Helpers de autenticación: FakeAuthSessionIssuer y FakeAuthCookieWriter se
// definen aquí (no existe un helper compartido para Refresh/GetMe todavía).
// Ninguno firma JWT: los 3 caminos de usuario borrado lanzan ANTES de llamar
// a IAuthSessionIssuer (confirmado leyendo LoginHandler/RefreshSessionHandler/
// GetMeHandler), así que el fake nunca necesita emitir un token real.
// ═══════════════════════════════════════════════════════════════════════════

[Trait("Category", "Integration")]
public sealed class Fase4d1UsuariosRolesReproTests : PostgresTestBase
{
    public Fase4d1UsuariosRolesReproTests(PostgresContainerFixture fixture)
        : base(fixture) { }

    [DockerFact]
    public async Task Login_UsuarioBorradoLogicamente_Devuelve401ConMensajeGenerico()
    {
        await using var db = await CreateDatabaseAsync();
        var roleId = await db.ScalarAsync<long>(
            "SELECT role_id FROM inventario.roles WHERE code = 'ADMIN'");

        var passwordHash = BCrypt.Net.BCrypt.HashPassword("Sd4d1Correcta!", 4);

        var deletedUserId = await db.ScalarAsync<long>($"""
            INSERT INTO inventario.app_users
                (username, email, password_hash, first_name, last_name, role_id, status)
            VALUES
                ('sd4d1_borrado', 'sd4d1-borrado@test.local', '{passwordHash}', 'Usuario', 'Borrado', {roleId}, 'activo')
            RETURNING user_id
            """);
        await db.ExecuteAsync(
            $"UPDATE inventario.app_users SET is_deleted = true WHERE user_id = {deletedUserId}");

        await db.ExecuteAsync($"""
            INSERT INTO inventario.app_users
                (username, email, password_hash, first_name, last_name, role_id, status)
            VALUES
                ('sd4d1_activo', 'sd4d1-activo@test.local', '{passwordHash}', 'Usuario', 'Activo', {roleId}, 'activo')
            """);

        await using var contextBorrado = db.CreateContext();
        var handlerBorrado = new LoginHandler(
            contextBorrado, new FakeAuthSessionIssuer(), TestLoginAttempts.Create(), NullLogger<LoginHandler>.Instance);

        var exBorrado = await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            handlerBorrado.Handle(
                new LoginCommand("sd4d1_borrado", "Sd4d1Correcta!"), CancellationToken.None).AsTask());

        await using var contextContrasenaIncorrecta = db.CreateContext();
        var handlerContrasenaIncorrecta = new LoginHandler(
            contextContrasenaIncorrecta, new FakeAuthSessionIssuer(), TestLoginAttempts.Create(), NullLogger<LoginHandler>.Instance);

        var exContrasenaIncorrecta = await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            handlerContrasenaIncorrecta.Handle(
                new LoginCommand("sd4d1_activo", "contrasena-incorrecta"), CancellationToken.None).AsTask());

        // G1: mismo mensaje y misma forma (el mismo tipo de excepción) que
        // credenciales inválidas, para no revelar si el usuario existe o fue borrado.
        Assert.Equal(exContrasenaIncorrecta.Message, exBorrado.Message);
        Assert.Equal("Credenciales inválidas.", exBorrado.Message);
    }

    [DockerFact]
    public async Task Refresh_UsuarioBorradoLogicamente_Devuelve401()
    {
        await using var db = await CreateDatabaseAsync();
        var roleId = await db.ScalarAsync<long>(
            "SELECT role_id FROM inventario.roles WHERE code = 'ADMIN'");

        var userId = await db.ScalarAsync<long>($"""
            INSERT INTO inventario.app_users
                (username, email, password_hash, first_name, last_name, role_id, status)
            VALUES
                ('sd4d1_refresh', 'sd4d1-refresh@test.local', 'NO_LOGIN', 'Usuario', 'Refresh', {roleId}, 'activo')
            RETURNING user_id
            """);

        var rawToken = $"sd4d1-raw-{Guid.NewGuid():N}";
        var tokenHash = AuthSessionIssuer.HashToken(rawToken);
        var familyId = Guid.NewGuid();
        var refreshTokenId = await db.ScalarAsync<long>($"""
            INSERT INTO inventario.refresh_tokens (user_id, token_hash, family_id, expires_at)
            VALUES ({userId}, '{tokenHash}', '{familyId}', now() + interval '7 days')
            RETURNING refresh_token_id
            """);

        await db.ExecuteAsync(
            $"UPDATE inventario.app_users SET is_deleted = true WHERE user_id = {userId}");

        await using var context = db.CreateContext();
        var handler = new RefreshSessionHandler(
            context,
            new FakeAuthSessionIssuer(),
            new FakeAuthCookieWriter(rawToken),
            Options.Create(new AuthOptions()),
            NullLogger<RefreshSessionHandler>.Instance);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            handler.Handle(new RefreshSessionCommand(), CancellationToken.None).AsTask());

        // A6: la comprobación explícita de RefreshSessionHandler.cs (línea ~80)
        // revoca el token ANTES de lanzar. Si en cambio la fila de refresh_token
        // desapareciera por un filtro sin IgnoreQueryFilters, este revoked_at
        // seguiría NULL porque el código nunca llegaría a ejecutarse.
        var isRevoked = await db.ScalarAsync<bool>(
            $"SELECT revoked_at IS NOT NULL FROM inventario.refresh_tokens WHERE refresh_token_id = {refreshTokenId}");
        Assert.True(isRevoked);
    }

    [DockerFact]
    public async Task GetUsers_UsuarioActivoConRolBorrado_SigueApareciendo()
    {
        await using var db = await CreateDatabaseAsync();
        var roleId = await db.ScalarAsync<long>($"""
            INSERT INTO inventario.roles (code, name) VALUES ('SD4D2{Guid.NewGuid():N}'::varchar(40), 'Rol SD4D2')
            RETURNING role_id
            """);

        var username = $"sd4d2gu{Guid.NewGuid():N}"[..14];
        var userId = await db.ScalarAsync<long>($"""
            INSERT INTO inventario.app_users
                (username, email, password_hash, first_name, last_name, role_id, status)
            VALUES
                ('{username}', '{username}@test.local', 'NO_LOGIN', 'Usuario', 'RolBorrado', {roleId}, 'activo')
            RETURNING user_id
            """);

        await db.ExecuteAsync($"UPDATE inventario.roles SET is_deleted = true WHERE role_id = {roleId}");

        await using var context = db.CreateContext();
        var handler = new GetUsersHandler(context);

        // H1: Role queda exento del filtro global -> el usuario activo
        // (no borrado) sigue apareciendo aunque su rol esté borrado lógicamente.
        var result = await handler.Handle(new GetUsersQuery(null), CancellationToken.None);

        Assert.Contains(result, u => u.UserId == userId);
    }

    [DockerFact]
    public async Task Login_UsuarioActivoConRolBorrado_AutenticaPeroSinPermisos()
    {
        await using var db = await CreateDatabaseAsync();
        var roleCode = $"SD4D2{Guid.NewGuid():N}"[..14].ToUpperInvariant();
        var roleId = await db.ScalarAsync<long>($"""
            INSERT INTO inventario.roles (code, name) VALUES ('{roleCode}', 'Rol SD4D2 Login')
            RETURNING role_id
            """);

        var passwordHash = BCrypt.Net.BCrypt.HashPassword("Sd4d2Correcta!", 4);
        await db.ExecuteAsync($"""
            INSERT INTO inventario.app_users
                (username, email, password_hash, first_name, last_name, role_id, status)
            VALUES
                ('sd4d2_login', 'sd4d2-login@test.local', '{passwordHash}', 'Usuario', 'LoginRolBorrado', {roleId}, 'activo')
            """);

        await db.ExecuteAsync($"UPDATE inventario.roles SET is_deleted = true WHERE role_id = {roleId}");

        await using var context = db.CreateContext();
        var handler = new LoginHandler(
            context, new FakeAuthSessionIssuer(allowIssue: true), TestLoginAttempts.Create(), NullLogger<LoginHandler>.Instance);

        // H1: el login no comprueba el rol -> autentica con credenciales correctas
        // aunque su rol esté borrado lógicamente (consecuencia aceptada de H1).
        await handler.Handle(new LoginCommand("sd4d2_login", "Sd4d2Correcta!"), CancellationToken.None);

        using var cache = new MemoryCache(new MemoryCacheOptions());
        var rolePermissions = new RolePermissionService(context, cache);

        // G3: RolePermissionService ya excluye roles borrados -> ningún
        // permiso se otorga, aunque el usuario sí pudo autenticarse.
        var hasPermission = await rolePermissions.HasPermissionAsync(
            roleCode, "assets.view", CancellationToken.None);

        Assert.False(hasPermission);
    }

    [DockerFact]
    public async Task ExternalLogin_UsuarioBorradoLogicamente_Devuelve401YNoCreaNiVinculaCuenta()
    {
        await using var db = await CreateDatabaseAsync();
        var roleId = await db.ScalarAsync<long>(
            "SELECT role_id FROM inventario.roles WHERE code = 'ADMIN'");

        var userId = await db.ScalarAsync<long>($"""
            INSERT INTO inventario.app_users
                (username, email, password_hash, first_name, last_name, role_id, status)
            VALUES
                ('sd4d2_ext', 'sd4d2-ext@test.local', 'NO_LOGIN', 'Usuario', 'Externo', {roleId}, 'activo')
            RETURNING user_id
            """);

        var subject = $"sd4d2-sub-{Guid.NewGuid():N}";
        await db.ExecuteAsync($"""
            INSERT INTO inventario.user_external_logins (user_id, provider, subject, email)
            VALUES ({userId}, '{AuthCookies.GoogleScheme}', '{subject}', 'sd4d2-ext@test.local')
            """);

        await db.ExecuteAsync(
            $"UPDATE inventario.app_users SET is_deleted = true WHERE user_id = {userId}");

        var usersBefore = await db.ScalarAsync<long>("SELECT COUNT(*) FROM inventario.app_users");
        var linksBefore = await db.ScalarAsync<long>("SELECT COUNT(*) FROM inventario.user_external_logins");

        await using var context = db.CreateContext();
        var httpContext = BuildExternalLoginHttpContext(subject, "sd4d2-ext@test.local", AuthCookies.GoogleScheme);
        var httpContextAccessor = new HttpContextAccessorStub(httpContext);
        var auditLog = new AuditLogService(context, new FakeCurrentUserWithId(userId));

        var handler = new ExternalLoginCallbackHandler(
            context,
            httpContextAccessor,
            new FakeAuthSessionIssuer(),
            auditLog,
            Options.Create(new AuthOptions()),
            NullLogger<ExternalLoginCallbackHandler>.Instance);

        var result = await handler.Handle(new ExternalLoginCallbackCommand(null), CancellationToken.None);

        // Con el fix (H5): el enlace existente + usuario borrado -> error
        // "cuenta_inactiva". HOY ya pasa (comprobación explícita en la línea 97),
        // pero sin IgnoreQueryFilters dejaría de pasar por ahí en 4d.2 (ver PASO D-ii).
        Assert.Contains("error=cuenta_inactiva", result.RedirectUrl);

        var usersAfter = await db.ScalarAsync<long>("SELECT COUNT(*) FROM inventario.app_users");
        var linksAfter = await db.ScalarAsync<long>("SELECT COUNT(*) FROM inventario.user_external_logins");

        Assert.Equal(usersBefore, usersAfter);
        Assert.Equal(linksBefore, linksAfter);
    }

    [DockerFact]
    public async Task AssignPermissions_ConPermisoBorradoLogicamente_Rechaza400()
    {
        await using var db = await CreateDatabaseAsync();
        var roleId = await db.ScalarAsync<long>($"""
            INSERT INTO inventario.roles (code, name) VALUES ('SD4D2{Guid.NewGuid():N}'::varchar(40), 'Rol SD4D2 AP')
            RETURNING role_id
            """);
        var permissionCode = $"sd4d2.perm.{Guid.NewGuid():N}"[..24];
        await db.ExecuteAsync($"""
            INSERT INTO inventario.permissions (code, name) VALUES ('{permissionCode}', 'Permiso SD4D2 AP')
            """);
        await db.ExecuteAsync(
            $"UPDATE inventario.permissions SET is_deleted = true WHERE code = '{permissionCode}'");

        await using var context = db.CreateContext();
        var validator = new AssignPermissionsValidator(context);

        // H6: ya existe (AllPermissionCodesExist, línea 45-46) -> solo se cubre con test.
        var result = await validator.ValidateAsync(
            new AssignPermissionsCommand(roleId, 1, [permissionCode]));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(AssignPermissionsCommand.PermissionCodes));
    }

    [DockerFact]
    public async Task AssignIncident_TecnicoBorradoLogicamente_Rechaza400()
    {
        await using var db = await CreateDatabaseAsync();
        var roleId = await db.ScalarAsync<long>(
            "SELECT role_id FROM inventario.roles WHERE code = 'TECNICO'");

        var technicianId = await db.ScalarAsync<long>($"""
            INSERT INTO inventario.app_users
                (username, email, password_hash, first_name, last_name, role_id, status)
            VALUES
                ('sd4d2_tec', 'sd4d2-tec@test.local', 'NO_LOGIN', 'Tecnico', 'Borrado', {roleId}, 'activo')
            RETURNING user_id
            """);
        await db.ExecuteAsync(
            $"UPDATE inventario.app_users SET is_deleted = true WHERE user_id = {technicianId}");

        var assetId = await db.ScalarAsync<long>("""
            INSERT INTO inventario.assets (inventory_code, asset_type_id, asset_status_id)
            SELECT 'SD4D2-TEC-A1', at.asset_type_id, s.asset_status_id
            FROM inventario.asset_types at
            CROSS JOIN inventario.asset_statuses s
            WHERE at.code = 'PC' AND s.code = 'CON_FALLA'
            RETURNING asset_id
            """);
        var incidentId = await db.ScalarAsync<long>($"""
            INSERT INTO inventario.incidents
                (asset_id, incident_type_id, incident_priority_id, incident_status_id,
                 reported_by, title, created_by)
            SELECT {assetId}, it.incident_type_id, ip.incident_priority_id,
                   ist.incident_status_id, 1, 'Falla SD4D2 asignación', 1
            FROM inventario.incident_types it, inventario.incident_priorities ip,
                 inventario.incident_statuses ist
            WHERE it.code = 'HARDWARE' AND ip.code = 'ALTA' AND ist.code = 'ABIERTO'
            RETURNING incident_id
            """);

        await using var context = db.CreateContext();
        var currentUser = new FakeCurrentUserWithId(1);
        var auditLog = new AuditLogService(context, currentUser);
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var rolePermissions = new RolePermissionService(context, cache);
        var handler = new AssignIncidentHandler(context, currentUser, auditLog, rolePermissions);

        // H6: ya existe (AssignIncidentHandler.cs, "técnico ... no está activo")
        // -> solo se cubre con test.
        await Assert.ThrowsAsync<BusinessRuleViolationException>(() =>
            handler.Handle(
                new AssignIncidentCommand(incidentId, 1, technicianId), CancellationToken.None).AsTask());
    }

    [DockerFact]
    public async Task GetMe_UsuarioBorradoLogicamente_Devuelve401()
    {
        await using var db = await CreateDatabaseAsync();
        var roleId = await db.ScalarAsync<long>(
            "SELECT role_id FROM inventario.roles WHERE code = 'ADMIN'");

        var userId = await db.ScalarAsync<long>($"""
            INSERT INTO inventario.app_users
                (username, email, password_hash, first_name, last_name, role_id, status)
            VALUES
                ('sd4d1_getme', 'sd4d1-getme@test.local', 'NO_LOGIN', 'Usuario', 'GetMe', {roleId}, 'activo')
            RETURNING user_id
            """);

        await db.ExecuteAsync(
            $"UPDATE inventario.app_users SET is_deleted = true WHERE user_id = {userId}");

        await using var context = db.CreateContext();
        var handler = new GetMeHandler(
            context, new FakeCurrentUserWithId(userId), new FakeAuthSessionIssuer());

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            handler.Handle(new GetMeQuery(), CancellationToken.None).AsTask());
    }

    [DockerFact]
    public async Task Autorizacion_RolBorradoLogicamente_NoOtorgaPermiso()
    {
        await using var db = await CreateDatabaseAsync();
        var roleCode = $"SD4D1R{Guid.NewGuid():N}"[..12].ToUpperInvariant();
        var permissionCode = $"sd4d1.rol.{Guid.NewGuid():N}"[..24];

        var roleId = await db.ScalarAsync<long>($"""
            INSERT INTO inventario.roles (code, name) VALUES ('{roleCode}', 'Rol SD4D1')
            RETURNING role_id
            """);
        var permissionId = await db.ScalarAsync<long>($"""
            INSERT INTO inventario.permissions (code, name) VALUES ('{permissionCode}', 'Permiso SD4D1')
            RETURNING permission_id
            """);
        await db.ExecuteAsync($"""
            INSERT INTO inventario.role_permissions (role_id, permission_id)
            VALUES ({roleId}, {permissionId})
            """);

        await db.ExecuteAsync($"UPDATE inventario.roles SET is_deleted = true WHERE role_id = {roleId}");

        await using var context = db.CreateContext();
        // Instancia de caché nueva y propia del test: aísla por completo de
        // la caché de 5 min de RolePermissionService (ninguna otra prueba ni
        // ejecución previa puede dejar una entrada ya resuelta en ESTA caché).
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var service = new RolePermissionService(context, cache);

        var hasPermission = await service.HasPermissionAsync(
            roleCode, permissionCode, CancellationToken.None);

        Assert.False(hasPermission);
    }

    [DockerFact]
    public async Task Autorizacion_PermisoBorradoLogicamente_NoOtorgaPermiso()
    {
        await using var db = await CreateDatabaseAsync();
        var roleCode = $"SD4D1P{Guid.NewGuid():N}"[..12].ToUpperInvariant();
        var permissionCode = $"sd4d1.perm.{Guid.NewGuid():N}"[..24];

        var roleId = await db.ScalarAsync<long>($"""
            INSERT INTO inventario.roles (code, name) VALUES ('{roleCode}', 'Rol SD4D1 P')
            RETURNING role_id
            """);
        var permissionId = await db.ScalarAsync<long>($"""
            INSERT INTO inventario.permissions (code, name) VALUES ('{permissionCode}', 'Permiso SD4D1 P')
            RETURNING permission_id
            """);
        await db.ExecuteAsync($"""
            INSERT INTO inventario.role_permissions (role_id, permission_id)
            VALUES ({roleId}, {permissionId})
            """);

        await db.ExecuteAsync(
            $"UPDATE inventario.permissions SET is_deleted = true WHERE permission_id = {permissionId}");

        await using var context = db.CreateContext();
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var service = new RolePermissionService(context, cache);

        // Con el fix (4d.2): el permiso borrado lógicamente no se devuelve.
        // HOY: RolePermissionService solo excluye el Rol borrado
        // (!r.IsDeleted); no filtra los Permission de r.Permissions.
        var hasPermission = await service.HasPermissionAsync(
            roleCode, permissionCode, CancellationToken.None);

        Assert.False(hasPermission);
    }

    [DockerFact]
    public async Task GetUsers_UsuarioBorradoLogicamente_NoAparece()
    {
        await using var db = await CreateDatabaseAsync();
        var roleId = await db.ScalarAsync<long>(
            "SELECT role_id FROM inventario.roles WHERE code = 'ADMIN'");

        var username = $"sd4d1gu{Guid.NewGuid():N}"[..14];
        var userId = await db.ScalarAsync<long>($"""
            INSERT INTO inventario.app_users
                (username, email, password_hash, first_name, last_name, role_id, status)
            VALUES
                ('{username}', '{username}@test.local', 'NO_LOGIN', 'Usuario', 'GetUsers', {roleId}, 'activo')
            RETURNING user_id
            """);

        await db.ExecuteAsync(
            $"UPDATE inventario.app_users SET is_deleted = true WHERE user_id = {userId}");

        await using var context = db.CreateContext();
        var handler = new GetUsersHandler(context);

        var result = await handler.Handle(new GetUsersQuery(null), CancellationToken.None);

        Assert.DoesNotContain(result, u => u.UserId == userId);
    }

    // allowIssue=false (default): usado en los caminos de rechazo (usuario
    // borrado), donde IssueAsync/BuildProfileAsync NUNCA deben llamarse.
    // allowIssue=true: usado en A2, donde el login SÍ debe completarse
    // (rol borrado, no usuario borrado) sin firmar un JWT real.
    private sealed class FakeAuthSessionIssuer : IAuthSessionIssuer
    {
        private readonly bool _allowIssue;

        public FakeAuthSessionIssuer(bool allowIssue = false) => _allowIssue = allowIssue;

        public Task<IssuedSession> IssueAsync(AppUser user, Guid? familyId, CancellationToken cancellationToken)
        {
            if (!_allowIssue)
            {
                throw new InvalidOperationException(
                    "No debería llamarse: el usuario borrado lógicamente debe rechazarse antes de emitir sesión.");
            }

            var profile = new AuthUserResponse(
                user.UserId, user.Username, user.Email, user.FirstName, user.LastName,
                user.Role.Code, user.Role.Name, []);
            return Task.FromResult(new IssuedSession(new RefreshToken { UserId = user.UserId }, profile, new SessionTokens("", DateTime.UtcNow, "", DateTime.UtcNow)));
        }

        public Task<AuthUserResponse> BuildProfileAsync(AppUser user, CancellationToken cancellationToken)
        {
            if (!_allowIssue)
            {
                throw new InvalidOperationException(
                    "No debería llamarse: el usuario borrado lógicamente debe rechazarse antes de construir el perfil.");
            }

            return Task.FromResult(new AuthUserResponse(
                user.UserId, user.Username, user.Email, user.FirstName, user.LastName,
                user.Role.Code, user.Role.Name, []));
        }

        // Sin cookies reales: los tests no firman ni envían tokens.
        public void WriteCookies(IssuedSession session) { }
    }

    private sealed class FakeAuthCookieWriter : IAuthCookieWriter
    {
        private readonly string? _refreshToken;

        public FakeAuthCookieWriter(string? refreshToken) => _refreshToken = refreshToken;

        public void Write(string accessToken, DateTime accessExpiresAt, string refreshToken, DateTime refreshExpiresAt)
            => throw new InvalidOperationException("No debería escribir cookies para un usuario rechazado.");

        public void Clear() { }

        public string? ReadRefreshToken() => _refreshToken;
    }

    // Simula lo que ASP.NET Core deja tras un callback OIDC/OAuth exitoso en
    // el esquema "External": un ClaimsPrincipal autenticado con los claims
    // del proveedor, accesible vía HttpContext.AuthenticateAsync(scheme).
    private static HttpContext BuildExternalLoginHttpContext(string subject, string email, string provider)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, subject),
            new("sub", subject),
            new("oid", subject),
            new("email", email),
            new("email_verified", "true"),
        };
        var identity = new ClaimsIdentity(claims, "test");
        var principal = new ClaimsPrincipal(identity);

        var properties = new AuthenticationProperties();
        properties.Items[".AuthScheme"] = provider;
        var ticket = new AuthenticationTicket(principal, properties, AuthCookies.ExternalScheme);

        var services = new ServiceCollection();
        services.AddSingleton<IAuthenticationService>(
            new FakeAuthenticationService(AuthenticateResult.Success(ticket)));
        var serviceProvider = services.BuildServiceProvider();

        var httpContext = new DefaultHttpContext { RequestServices = serviceProvider };
        return httpContext;
    }

    private sealed class HttpContextAccessorStub : IHttpContextAccessor
    {
        public HttpContextAccessorStub(HttpContext httpContext) => HttpContext = httpContext;

        public HttpContext? HttpContext { get; set; }
    }

    // Solo implementa lo que ExternalLoginCallbackHandler usa: AuthenticateAsync
    // (devuelve el resultado fijado en el test) y SignOutAsync (no-op, la
    // cookie externa es de un solo uso y el handler la cierra siempre).
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
        public string? Username => "sd4d1";
        public string? RoleCode => "ADMIN";
        public System.Net.IPAddress? IpAddress => null;
    }
}
