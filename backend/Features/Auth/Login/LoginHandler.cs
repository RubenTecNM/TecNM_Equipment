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
        var user = await _context.AppUsers
            .Include(u => u.Role)
            .FirstOrDefaultAsync(
                u => !u.IsDeleted &&
                     (u.Username == command.UsernameOrEmail || u.Email == command.UsernameOrEmail),
                cancellationToken);

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
