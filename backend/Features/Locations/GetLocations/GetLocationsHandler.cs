using Mediator;
using Microsoft.EntityFrameworkCore;
using TecnmInventario.Api.Data;

namespace TecnmInventario.Api.Features.Locations.GetLocations;

public sealed class GetLocationsHandler : IQueryHandler<GetLocationsQuery, IReadOnlyList<GetLocationsResponse>>
{
    private readonly TecnmInventarioDbContext _context;

    public GetLocationsHandler(TecnmInventarioDbContext context)
    {
        _context = context;
    }

    public async ValueTask<IReadOnlyList<GetLocationsResponse>> Handle(
        GetLocationsQuery query, CancellationToken cancellationToken)
    {
        // Sin paginar a propósito: el catálogo de ubicaciones de un plantel
        // es acotado (decenas/pocos cientos de filas); el frontend arma el
        // árbol/breadcrumb del lado del cliente a partir de ParentLocationId.
        return await _context.Locations
            .OrderBy(l => l.Code)
            .Select(l => new GetLocationsResponse(
                l.LocationId, l.Code, l.Name, l.LocationType.Code, l.ParentLocationId, l.Version))
            .ToListAsync(cancellationToken);
    }
}
