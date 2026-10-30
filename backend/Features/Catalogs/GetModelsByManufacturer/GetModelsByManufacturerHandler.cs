using Mediator;
using Microsoft.EntityFrameworkCore;
using TecnmInventario.Api.Data;

namespace TecnmInventario.Api.Features.Catalogs.GetModelsByManufacturer;

public sealed class GetModelsByManufacturerHandler
    : IQueryHandler<GetModelsByManufacturerQuery, IReadOnlyList<GetModelsByManufacturerResponse>>
{
    private readonly TecnmInventarioDbContext _context;

    public GetModelsByManufacturerHandler(TecnmInventarioDbContext context)
    {
        _context = context;
    }

    public async ValueTask<IReadOnlyList<GetModelsByManufacturerResponse>> Handle(
        GetModelsByManufacturerQuery query, CancellationToken cancellationToken)
    {
        var manufacturerExists = await _context.Manufacturers
            .AnyAsync(m => m.ManufacturerId == query.ManufacturerId && !m.IsDeleted, cancellationToken);

        if (!manufacturerExists)
        {
            throw new KeyNotFoundException("El fabricante solicitado no existe.");
        }

        var models = _context.Models.AsNoTracking()
            .Where(m => m.ManufacturerId == query.ManufacturerId && !m.IsDeleted);

        // Con filtro por tipo de activo solo salen los modelos de ese tipo (comparación estricta).
        if (query.AssetTypeId.HasValue)
        {
            var assetTypeId = query.AssetTypeId.Value;
            models = models.Where(m => m.AssetTypeId == assetTypeId);
        }

        return await models
            .OrderBy(m => m.ModelName)
            .Select(m => new GetModelsByManufacturerResponse(m.ModelId, m.ModelName, m.AssetTypeId))
            .ToListAsync(cancellationToken);
    }
}
