using Mediator;
using Microsoft.EntityFrameworkCore;
using TecnmInventario.Api.Common.Security;
using TecnmInventario.Api.Data;
using TecnmInventario.Api.Features.Auth.Shared;

namespace TecnmInventario.Api.Features.Auth.Refresh;

public sealed class RefreshSessionHandler : ICommandHandler<RefreshSessionCommand, AuthUserResponse>
{
    // Dos pestañas pueden refrescar a la vez con el mismo token: la segunda
    // llega con uno recién rotado. Dentro de esta ventana no se trata como robo.
    private static readonly TimeSpan RotationGrace = TimeSpan.FromSeconds(30);

    private readonly TecnmInventarioDbContext _context;
    private readonly IAuthSessionIssuer _sessions;
    private readonly IAuthCookieWriter _cookies;
    private readonly ILogger<RefreshSessionHandler> _logger;

    public RefreshSessionHandler(
        TecnmInventarioDbContext context,
        IAuthSessionIssuer sessions,
        IAuthCookieWriter cookies,
        ILogger<RefreshSessionHandler> logger)
    {
        _context = context;
        _sessions = sessions;
        _cookies = cookies;
        _logger = logger;
    }

    public async ValueTask<AuthUserResponse> Handle(RefreshSessionCommand command, CancellationToken cancellationToken)
    {
        var raw = _cookies.ReadRefreshToken();
        if (string.IsNullOrEmpty(raw))
        {
            throw new UnauthorizedAccessException("La sesión expiró. Inicia sesión de nuevo.");
        }

        var hash = AuthSessionIssuer.HashToken(raw);
        var now = DateTime.UtcNow;

        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

        var current = await _context.RefreshTokens
            .Include(t => t.User).ThenInclude(u => u.Role)
            .FirstOrDefaultAsync(t => t.TokenHash == hash, cancellationToken);

        if (current is null || current.ExpiresAt <= now)
        {
            _cookies.Clear();
            throw new UnauthorizedAccessException("La sesión expiró. Inicia sesión de nuevo.");
        }

        if (current.RevokedAt is not null)
        {
            var benignRace = current.ReplacedById is not null && now - current.RevokedAt.Value < RotationGrace;
            if (!benignRace)
            {
                // Reutilización de un token ya rotado: posible robo. Se cierra
                // toda la cadena de sesiones derivadas de ese login.
                _logger.LogWarning(
                    "Reutilización de refresh token revocado (usuario {UserId}, familia {FamilyId}); se revoca la familia",
                    current.UserId, current.FamilyId);

                await _context.RefreshTokens
                    .Where(t => t.FamilyId == current.FamilyId && t.RevokedAt == null)
                    .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAt, now), cancellationToken);
                await transaction.CommitAsync(cancellationToken);

                _cookies.Clear();
            }

            // En la carrera benigna no se borran cookies: la otra pestaña ya
            // dejó las nuevas y el cliente reintenta con ellas.
            throw new UnauthorizedAccessException("La sesión ya fue renovada o revocada.");
        }

        var user = current.User;
        if (user.IsDeleted || user.Status != "activo")
        {
            current.RevokedAt = now;
            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            _cookies.Clear();
            throw new UnauthorizedAccessException("La cuenta no está activa.");
        }

        var issued = await _sessions.IssueAsync(user, current.FamilyId, cancellationToken);

        current.RevokedAt = now;
        current.ReplacedById = issued.RefreshToken.RefreshTokenId;
        await _context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return issued.Profile;
    }
}
