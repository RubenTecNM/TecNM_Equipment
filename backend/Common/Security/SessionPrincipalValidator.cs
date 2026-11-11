using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using TecnmInventario.Api.Data;

namespace TecnmInventario.Api.Common.Security;

/// <summary>Sección "Security:SessionValidation".</summary>
public sealed class SessionValidationOptions
{
    public const string SectionName = "Security:SessionValidation";

    /// <summary>
    /// Segundos que se reutiliza el estado leído de un usuario. Es la ventana
    /// máxima en la que un JWT vigente sigue abriendo peticiones tras desactivar,
    /// borrar o cambiar de rol al usuario. 0 = consulta en cada petición.
    /// </summary>
    public int CacheSeconds { get; set; } = 30;
}

/// <summary>
/// S4: JwtBearer valida firma y caducidad, pero no si el usuario sigue activo.
/// En cada petición autenticada (cabecera Bearer o cookie tecnm_at) se compara
/// el token con el estado actual del usuario: inexistente, borrado lógicamente,
/// no activo o con un rol distinto al del claim -> 401.
/// </summary>
public static class SessionPrincipalValidator
{
    public const string ActiveStatus = "activo";

    public static async Task ValidateAsync(TokenValidatedContext context)
    {
        var principal = context.Principal;
        var sub = principal?.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        var roleClaim = principal?.FindFirst(ClaimTypes.Role)?.Value;

        if (!long.TryParse(sub, out var userId) || string.IsNullOrEmpty(roleClaim))
        {
            context.Fail("El token no identifica al usuario.");
            return;
        }

        var services = context.HttpContext.RequestServices;
        var cacheSeconds = services.GetRequiredService<IOptions<SessionValidationOptions>>().Value.CacheSeconds;

        var state = cacheSeconds > 0
            ? await services.GetRequiredService<IMemoryCache>().GetOrCreateAsync(
                $"session-user:{userId}",
                entry =>
                {
                    entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(cacheSeconds);
                    return LoadAsync(services, userId, context.HttpContext.RequestAborted);
                })
            : await LoadAsync(services, userId, context.HttpContext.RequestAborted);

        if (state is null || state.IsDeleted || state.Status != ActiveStatus ||
            !string.Equals(state.RoleCode, roleClaim, StringComparison.Ordinal))
        {
            // Sin datos personales en el log: solo el user_id.
            services.GetRequiredService<ILoggerFactory>()
                .CreateLogger(typeof(SessionPrincipalValidator))
                .LogInformation("Token rechazado: el usuario {UserId} ya no está activo o cambió de rol", userId);
            context.Fail("La sesión ya no es válida.");
        }
    }

    // IgnoreQueryFilters: sin él, un usuario borrado lógicamente se vería como
    // "no existe"; así se distinguen ambos casos (y se rechazan los dos).
    private static Task<UserSessionState?> LoadAsync(IServiceProvider services, long userId, CancellationToken cancellationToken)
        => services.GetRequiredService<TecnmInventarioDbContext>().AppUsers
            .IgnoreQueryFilters(["SoftDelete"])
            .AsNoTracking()
            .Where(u => u.UserId == userId)
            .Select(u => new UserSessionState(u.Status, u.IsDeleted, u.Role.Code))
            .FirstOrDefaultAsync(cancellationToken);

    private sealed record UserSessionState(string Status, bool IsDeleted, string RoleCode);
}
