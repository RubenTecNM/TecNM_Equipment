using Mediator;
using Microsoft.EntityFrameworkCore;
using TecnmInventario.Api.Common.Security;
using TecnmInventario.Api.Data;
using TecnmInventario.Api.Features.Auth.Shared;

namespace TecnmInventario.Api.Features.Auth.GetMe;

public sealed class GetMeHandler : IQueryHandler<GetMeQuery, AuthUserResponse>
{
    private readonly TecnmInventarioDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IAuthSessionIssuer _sessions;

    public GetMeHandler(TecnmInventarioDbContext context, ICurrentUserService currentUser, IAuthSessionIssuer sessions)
    {
        _context = context;
        _currentUser = currentUser;
        _sessions = sessions;
    }

    public async ValueTask<AuthUserResponse> Handle(GetMeQuery query, CancellationToken cancellationToken)
    {
        var user = await _context.AppUsers
            .AsNoTracking()
            .Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.UserId == _currentUser.UserId && !u.IsDeleted, cancellationToken);

        // Un JWT vigente de una cuenta desactivada no debe seguir abriendo la UI.
        if (user is null || user.Status != "activo")
        {
            throw new UnauthorizedAccessException("La cuenta no está activa.");
        }

        return await _sessions.BuildProfileAsync(user, cancellationToken);
    }
}
