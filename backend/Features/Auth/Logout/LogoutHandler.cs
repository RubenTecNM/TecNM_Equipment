using Mediator;
using Microsoft.EntityFrameworkCore;
using TecnmInventario.Api.Common.Security;
using TecnmInventario.Api.Data;
using TecnmInventario.Api.Features.Auth.Shared;

namespace TecnmInventario.Api.Features.Auth.Logout;

public sealed class LogoutHandler : ICommandHandler<LogoutCommand>
{
    private readonly TecnmInventarioDbContext _context;
    private readonly IAuthCookieWriter _cookies;

    public LogoutHandler(TecnmInventarioDbContext context, IAuthCookieWriter cookies)
    {
        _context = context;
        _cookies = cookies;
    }

    public async ValueTask<Unit> Handle(LogoutCommand command, CancellationToken cancellationToken)
    {
        var raw = _cookies.ReadRefreshToken();
        if (!string.IsNullOrEmpty(raw))
        {
            var hash = AuthSessionIssuer.HashToken(raw);
            await _context.RefreshTokens
                .Where(t => t.TokenHash == hash && t.RevokedAt == null)
                .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAt, DateTime.UtcNow), cancellationToken);
        }

        // Idempotente: sin cookie (o ya revocada) solo se limpian las cookies.
        _cookies.Clear();
        return Unit.Value;
    }
}
