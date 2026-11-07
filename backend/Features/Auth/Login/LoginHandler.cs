using Mediator;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TecnmInventario.Api.Data;
using TecnmInventario.Api.Features.Auth.Shared;

namespace TecnmInventario.Api.Features.Auth.Login;

public sealed class LoginHandler : ICommandHandler<LoginCommand, AuthUserResponse>
{
    private readonly TecnmInventarioDbContext _context;
    private readonly IAuthSessionIssuer _sessions;
    private readonly ILogger<LoginHandler> _logger;

    public LoginHandler(TecnmInventarioDbContext context, IAuthSessionIssuer sessions, ILogger<LoginHandler> logger)
    {
        _context = context;
        _sessions = sessions;
        _logger = logger;
    }

    public async ValueTask<AuthUserResponse> Handle(LoginCommand command, CancellationToken cancellationToken)
    {
        // N2: con '@' se busca SOLO por email; sin '@', SOLO por username. Sin
        // OR entre campos: el username de uno no puede coincidir con el email
        // de otro. Comparación en minúsculas (lower(col) en PostgreSQL).
        var login = command.UsernameOrEmail.Trim().ToLowerInvariant();
        var query = _context.AppUsers.Include(u => u.Role).Where(u => !u.IsDeleted);
        query = login.Contains('@')
            ? query.Where(u => u.Email.ToLower() == login)
            : query.Where(u => u.Username.ToLower() == login);

        var candidates = await query.OrderBy(u => u.UserId).Take(10).ToListAsync(cancellationToken);

        // N4: datos históricos que solo difieren en mayúsculas. Nunca se elige
        // uno al azar: se rechaza con el mensaje genérico.
        if (candidates.Count > 1)
        {
            _logger.LogWarning(
                "Login rechazado: varias cuentas coinciden sin distinguir mayúsculas (user_id {UserIds})",
                string.Join(", ", candidates.Select(u => u.UserId)));
            throw new UnauthorizedAccessException("Credenciales inválidas.");
        }

        var user = candidates.SingleOrDefault();

        if (user is null || user.Status != "activo")
        {
            throw new UnauthorizedAccessException("Credenciales inválidas.");
        }

        if (string.IsNullOrWhiteSpace(user.PasswordHash) ||
            !user.PasswordHash.StartsWith("$2", StringComparison.Ordinal) ||
            user.PasswordHash.Length != 60)
        {
            _logger.LogWarning("Invalid password hash format for user {UserId}", user.UserId);
            throw new UnauthorizedAccessException("Credenciales inválidas.");
        }

        var passwordValid = BCrypt.Net.BCrypt.Verify(command.Password, user.PasswordHash);
        if (!passwordValid)
        {
            throw new UnauthorizedAccessException("Credenciales inválidas.");
        }

        var session = await _sessions.IssueAsync(user, familyId: null, cancellationToken);

        return session.Profile;
    }
}
