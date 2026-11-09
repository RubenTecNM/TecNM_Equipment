using Mediator;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TecnmInventario.Api.Common.Security;
using TecnmInventario.Api.Data;
using TecnmInventario.Api.Features.Auth.Shared;

namespace TecnmInventario.Api.Features.Auth.Login;

public sealed class LoginHandler : ICommandHandler<LoginCommand, AuthUserResponse>
{
    private const string InvalidCredentials = "Credenciales inválidas.";

    // P3: hash ficticio para igualar el tiempo de las ramas de rechazo con el
    // de una contraseña incorrecta. Se genera una vez por proceso con el work
    // factor por defecto de BCrypt.Net, el mismo que usan RegisterHandler y
    // tools/PasswordHasher para los hashes reales.
    private static readonly Lazy<string> DummyHash =
        new(() => BCrypt.Net.BCrypt.HashPassword(Guid.NewGuid().ToString("N")));

    private readonly TecnmInventarioDbContext _context;
    private readonly IAuthSessionIssuer _sessions;
    private readonly LoginAttemptLimiter _attempts;
    private readonly ILogger<LoginHandler> _logger;

    public LoginHandler(
        TecnmInventarioDbContext context,
        IAuthSessionIssuer sessions,
        LoginAttemptLimiter attempts,
        ILogger<LoginHandler> logger)
    {
        _context = context;
        _sessions = sessions;
        _attempts = attempts;
        _logger = logger;
    }

    public async ValueTask<AuthUserResponse> Handle(LoginCommand command, CancellationToken cancellationToken)
    {
        // N2: con '@' se busca SOLO por email; sin '@', SOLO por username. Sin
        // OR entre campos: el username de uno no puede coincidir con el email
        // de otro. Comparación en minúsculas (lower(col) en PostgreSQL).
        var login = command.UsernameOrEmail.Trim().ToLowerInvariant();

        // P2: el bloqueo se aplica al texto escrito, exista o no la cuenta, y
        // antes de consultar la BD: misma respuesta para cualquier identificador.
        if (_attempts.IsLockedOut(login, out var retryAfter))
        {
            throw new TooManyRequestsException(retryAfter);
        }

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
            throw Reject(login, command.Password);
        }

        var user = candidates.SingleOrDefault();

        if (user is null || user.Status != "activo")
        {
            throw Reject(login, command.Password);
        }

        if (string.IsNullOrWhiteSpace(user.PasswordHash) ||
            !user.PasswordHash.StartsWith("$2", StringComparison.Ordinal) ||
            user.PasswordHash.Length != 60)
        {
            _logger.LogWarning("Invalid password hash format for user {UserId}", user.UserId);
            throw Reject(login, command.Password);
        }

        var passwordValid = BCrypt.Net.BCrypt.Verify(command.Password, user.PasswordHash);
        if (!passwordValid)
        {
            throw Fail(login);
        }

        _attempts.Reset(login);

        var session = await _sessions.IssueAsync(user, familyId: null, cancellationToken);

        return session.Profile;
    }

    // P3: toda rama de rechazo sin hash real paga el mismo bcrypt que una
    // contraseña incorrecta, para no revelar por tiempo si la cuenta existe.
    private UnauthorizedAccessException Reject(string login, string password)
    {
        BCrypt.Net.BCrypt.Verify(password, DummyHash.Value);
        return Fail(login);
    }

    private UnauthorizedAccessException Fail(string login)
    {
        if (_attempts.RegisterFailure(login))
        {
            // Sin el identificador: puede ser un correo (dato personal).
            _logger.LogWarning("Identificador de login bloqueado temporalmente por intentos fallidos");
        }

        return new UnauthorizedAccessException(InvalidCredentials);
    }
}
