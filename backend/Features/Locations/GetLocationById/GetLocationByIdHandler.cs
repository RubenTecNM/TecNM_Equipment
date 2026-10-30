using Mediator;
using Microsoft.EntityFrameworkCore;
using TecnmInventario.Api.Core.Entities;
using TecnmInventario.Api.Data;

namespace TecnmInventario.Api.Features.Locations.GetLocationById;

public sealed class GetLocationByIdHandler : IQueryHandler<GetLocationByIdQuery, GetLocationByIdResponse>
{
    private readonly TecnmInventarioDbContext _context;

    public GetLocationByIdHandler(TecnmInventarioDbContext context)
    {
        _context = context;
    }

    public async ValueTask<GetLocationByIdResponse> Handle(
        GetLocationByIdQuery query, CancellationToken cancellationToken)
    {
        var location = await _context.Locations
            .Include(l => l.LocationType)
            .FirstOrDefaultAsync(l => l.LocationId == query.LocationId, cancellationToken);

        if (location is null)
        {
            throw new KeyNotFoundException("La ubicación solicitada no existe.");
        }

        var breadcrumb = await BuildBreadcrumbAsync(location, cancellationToken);

        return new GetLocationByIdResponse(
            location.LocationId, location.Code, location.Name,
            location.LocationType.Code, location.ParentLocationId, breadcrumb, location.Version);
    }

    // Recorre hacia arriba hasta la raíz. La jerarquía institucional es poco
    // profunda (Edificio->Planta->Área->Rack, 4-5 niveles como máximo), así
    // que un loop simple es más claro que un CTE recursivo en SQL para este
    // alcance; se revisará si el volumen real algún día lo justifica.
    private async Task<IReadOnlyList<string>> BuildBreadcrumbAsync(
        Location location, CancellationToken ct)
    {
        var names = new List<string> { location.Name };
        var currentParentId = location.ParentLocationId;
        var guard = 0;

        while (currentParentId.HasValue && guard++ < 20)
        {
            var parent = await _context.Locations
                .FirstOrDefaultAsync(l => l.LocationId == currentParentId, ct);

            if (parent is null)
            {
                break;
            }

            names.Insert(0, parent.Name);
            currentParentId = parent.ParentLocationId;
        }

        return names;
    }
}
