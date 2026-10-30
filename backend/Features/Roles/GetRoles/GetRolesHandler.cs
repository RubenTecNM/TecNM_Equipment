using Mediator;
using Microsoft.EntityFrameworkCore;
using TecnmInventario.Api.Data;

namespace TecnmInventario.Api.Features.Roles.GetRoles;

public sealed class GetRolesHandler : IQueryHandler<GetRolesQuery, IReadOnlyList<GetRolesResponse>>
{
    private readonly TecnmInventarioDbContext _context;

    public GetRolesHandler(TecnmInventarioDbContext context)
    {
        _context = context;
    }

    public async ValueTask<IReadOnlyList<GetRolesResponse>> Handle(
        GetRolesQuery query, CancellationToken cancellationToken)
    {
        return await _context.Roles
            .Where(r => !r.IsDeleted)
            .OrderBy(r => r.RoleId)
            .Select(r => new GetRolesResponse(
                r.RoleId,
                r.Code,
                r.Name,
                r.Description,
                r.Permissions
                    .Where(p => !p.IsDeleted)
                    .OrderBy(p => p.Code)
                    .Select(p => p.Code)
                    .ToList()))
            .ToListAsync(cancellationToken);
    }
}
