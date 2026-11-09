using System.Security.Claims;
using Mediator;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TecnmInventario.Api.Common.Audit;
using TecnmInventario.Api.Common.Security;
using TecnmInventario.Api.Core.Entities;
using TecnmInventario.Api.Data;
using TecnmInventario.Api.Features.Auth.Shared;

namespace TecnmInventario.Api.Features.Auth.ExternalLogin;

public sealed class ExternalLoginCallbackHandler
    : ICommandHandler<ExternalLoginCallbackCommand, ExternalLoginCallbackResponse>
{
    // Códigos que la SPA traduce a mensajes en /login?error=...
    private const string ErrorFailed = "federado_fallido";
    private const string ErrorNoAccount = "sin_cuenta";
    private const string ErrorDomain = "dominio_no_permitido";
    private const string ErrorInactive = "cuenta_inactiva";

    private readonly TecnmInventarioDbContext _context;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IAuthSessionIssuer _sessions;
    private readonly IAuditLogService _auditLog;
    private readonly AuthOptions _options;
    private readonly ILogger<ExternalLoginCallbackHandler> _logger;

    public ExternalLoginCallbackHandler(
        TecnmInventarioDbContext context,
        IHttpContextAccessor httpContextAccessor,
        IAuthSessionIssuer sessions,
        IAuditLogService auditLog,
        IOptions<AuthOptions> options,
        ILogger<ExternalLoginCallbackHandler> logger)
    {
        _context = context;
        _httpContextAccessor = httpContextAccessor;
        _sessions = sessions;
        _auditLog = auditLog;
        _options = options.Value;
        _logger = logger;
    }

    public async ValueTask<ExternalLoginCallbackResponse> Handle(
        ExternalLoginCallbackCommand command, CancellationToken cancellationToken)
    {
        var httpContext = _httpContextAccessor.HttpContext
            ?? throw new InvalidOperationException("No hay una petición HTTP activa.");

        var result = await httpContext.AuthenticateAsync(AuthCookies.ExternalScheme);
        // La cookie externa es de un solo uso.
        await httpContext.SignOutAsync(AuthCookies.ExternalScheme);

        if (!result.Succeeded || result.Principal is null ||
            result.Properties?.Items.TryGetValue(".AuthScheme", out var provider) != true || provider is null)
        {
            return Fail(ErrorFailed);
        }

        var identity = ReadIdentity(provider, result.Principal);
        if (identity is null)
        {
            _logger.LogWarning("Identidad externa sin subject o email verificado ({Provider})", provider);
            return Fail(ErrorFailed);
        }

        if (provider == AuthCookies.GoogleScheme && !string.IsNullOrEmpty(_options.Google.HostedDomain) &&
            !string.Equals(result.Principal.FindFirstValue("hd"), _options.Google.HostedDomain, StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning("Login de Google rechazado: dominio fuera de {Domain}", _options.Google.HostedDomain);
            return Fail(ErrorDomain);
        }

        // N5: Entra no garantiza que "email"/"preferred_username" estén
        // verificados; solo se aceptan dominios de la lista, por igualdad exacta.
        if (provider == AuthCookies.MicrosoftScheme && !IsAllowedEmailDomain(identity.Email))
        {
            _logger.LogWarning("Login de Microsoft rechazado: dominio de correo fuera de Auth:AllowedEmailDomains");
            return Fail(ErrorDomain);
        }

        // H5: UserExternalLogin.UserId es una FK requerida; sin esto, un
        // usuario borrado lógicamente haría desaparecer la fila del enlace
        // entera, cayendo por error en la rama "sin cuenta vinculable" en vez
        // de la comprobación explícita de abajo (línea ~101) que responde
        // "cuenta_inactiva". UserExternalLogin no tiene columna is_deleted,
        // así que no hace falta un Where sobre la raíz.
        var link = await _context.UserExternalLogins
            .IgnoreQueryFilters(["SoftDelete"])
            .Include(l => l.User).ThenInclude(u => u.Role)
            .FirstOrDefaultAsync(l => l.Provider == provider && l.Subject == identity.Subject, cancellationToken);

        var user = link?.User;
        if (user is null)
        {
            // Primer login con este proveedor: se vincula por email a una cuenta
            // existente. No hay auto-alta; las cuentas las crea un ADMIN.
            var email = identity.Email.Trim().ToLowerInvariant();
            var candidates = await _context.AppUsers
                .Include(u => u.Role)
                .Where(u => !u.IsDeleted && u.Email.ToLower() == email)
                .OrderBy(u => u.UserId)
                .Take(10)
                .ToListAsync(cancellationToken);

            // N4: cuentas históricas que solo difieren en mayúsculas. No se
            // vincula ninguna: elegir una al azar podría entregar otra cuenta.
            if (candidates.Count > 1)
            {
                _logger.LogWarning(
                    "Vinculación federada rechazada ({Provider}): varias cuentas coinciden sin distinguir mayúsculas (user_id {UserIds})",
                    provider, string.Join(", ", candidates.Select(u => u.UserId)));
                return Fail(ErrorNoAccount);
            }

            user = candidates.SingleOrDefault();

            if (user is null)
            {
                _logger.LogInformation("Login federado sin cuenta vinculable ({Provider})", provider);
                return Fail(ErrorNoAccount);
            }
        }

        if (user.IsDeleted || user.Status != "activo")
        {
            return Fail(ErrorInactive);
        }

        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

        if (link is null)
        {
            var newLink = new UserExternalLogin
            {
                UserId = user.UserId,
                Provider = provider,
                Subject = identity.Subject,
                Email = identity.Email,
                LinkedAt = DateTime.UtcNow
            };
            _context.UserExternalLogins.Add(newLink);
            await _context.SaveChangesAsync(cancellationToken);

            await _auditLog.LogAsync(
                "user_external_logins",
                newLink.UserExternalLoginId,
                "INSERT",
                null,
                new { newLink.UserId, newLink.Provider, newLink.Subject, newLink.Email },
                cancellationToken);
        }

        var session = await _sessions.IssueAsync(user, familyId: null, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        // S1: cookies solo con la sesión ya confirmada.
        _sessions.WriteCookies(session);

        return new ExternalLoginCallbackResponse(SpaUrl(ExternalLoginReturnUrl.Sanitize(command.ReturnUrl)));
    }

    private static ExternalIdentity? ReadIdentity(string provider, ClaimsPrincipal principal)
    {
        // Entra: "oid" es estable por tenant; "sub" varía por aplicación.
        var subject = provider == AuthCookies.MicrosoftScheme
            ? principal.FindFirstValue("oid")
            : principal.FindFirstValue("sub");

        var email = principal.FindFirstValue("email")
            ?? (provider == AuthCookies.MicrosoftScheme ? principal.FindFirstValue("preferred_username") : null);

        // Google declara la verificación; en Entra el tenant ya está restringido
        // por la Authority, y sus cuentas son administradas por la institución.
        var verified = provider == AuthCookies.MicrosoftScheme ||
                       string.Equals(principal.FindFirstValue("email_verified"), "true", StringComparison.OrdinalIgnoreCase);

        if (string.IsNullOrEmpty(subject) || string.IsNullOrEmpty(email) || !verified)
            return null;

        return new ExternalIdentity(subject, email);
    }

    // Igualdad exacta del dominio completo: "evil-ejemplo.edu.mx" no pasa por
    // "ejemplo.edu.mx" (nada de EndsWith). Lista vacía = ningún dominio.
    private bool IsAllowedEmailDomain(string email)
    {
        var at = email.LastIndexOf('@');
        if (at < 0 || at == email.Length - 1)
            return false;

        var domain = email[(at + 1)..].Trim().ToLowerInvariant();
        return _options.AllowedEmailDomains.Any(d =>
            string.Equals(d?.Trim().TrimStart('@').ToLowerInvariant(), domain, StringComparison.Ordinal));
    }

    private ExternalLoginCallbackResponse Fail(string code) =>
        new(SpaUrl($"/login?error={code}"));

    private string SpaUrl(string path) => $"{_options.SpaBaseUrl.TrimEnd('/')}{path}";

    private sealed record ExternalIdentity(string Subject, string Email);
}
