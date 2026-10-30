using Mediator;
using Microsoft.EntityFrameworkCore;
using TecnmInventario.Api.Data;

namespace TecnmInventario.Api.Features.Users.GetUsers;

public sealed class GetUsersHandler : IQueryHandler<GetUsersQuery, IReadOnlyList<GetUsersResponse>>
{
    private readonly TecnmInventarioDbContext _context;

    public GetUsersHandler(TecnmInventarioDbContext context)
    {
        _context = context;
    }

    public async ValueTask<IReadOnlyList<GetUsersResponse>> Handle(
        GetUsersQuery query, CancellationToken cancellationToken)
    {
        var search = (query.Search ?? "").Trim();

        // Selector para asignaciones: solo usuarios activos, top 50.
        // ILike para búsqueda insensible a mayúsculas en PostgreSQL.
        return await _context.AppUsers
            .Where(u => !u.IsDeleted && u.Status == "activo")
            .Where(u => search == "" ||
                EF.Functions.ILike(u.Username, $"%{search}%") ||
                EF.Functions.ILike(u.FirstName, $"%{search}%") ||
                EF.Functions.ILike(u.LastName, $"%{search}%"))
            .OrderBy(u => u.Username)
            .Take(50)
            .Select(u => new GetUsersResponse(
                u.UserId,
                u.Username,
                u.FirstName,
                u.LastName,
                u.Role.Code,
                u.Status))
            .ToListAsync(cancellationToken);
    }
}
