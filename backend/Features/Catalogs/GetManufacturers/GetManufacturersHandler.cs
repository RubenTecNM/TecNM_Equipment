using Mediator;
using Microsoft.EntityFrameworkCore;
using TecnmInventario.Api.Data;

namespace TecnmInventario.Api.Features.Catalogs.GetManufacturers;

public sealed class GetManufacturersHandler
    : IQueryHandler<GetManufacturersQuery, IReadOnlyList<GetManufacturersResponse>>
{
    private readonly TecnmInventarioDbContext _context;

    public GetManufacturersHandler(TecnmInventarioDbContext context)
    {
        _context = context;
    }

    public async ValueTask<IReadOnlyList<GetManufacturersResponse>> Handle(
        GetManufacturersQuery query, CancellationToken cancellationToken)
    {
        return await _context.Manufacturers.AsNoTracking()
            .Where(m => !m.IsDeleted)
            .OrderBy(m => m.Name)
            .Select(m => new GetManufacturersResponse(m.ManufacturerId, m.Name))
            .ToListAsync(cancellationToken);
    }
}
