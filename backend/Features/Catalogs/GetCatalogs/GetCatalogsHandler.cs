using Mediator;
using Microsoft.EntityFrameworkCore;
using TecnmInventario.Api.Data;

namespace TecnmInventario.Api.Features.Catalogs.GetCatalogs;

public sealed class GetCatalogsHandler : IQueryHandler<GetCatalogsQuery, GetCatalogsResponse>
{
    private readonly TecnmInventarioDbContext _context;

    public GetCatalogsHandler(TecnmInventarioDbContext context)
    {
        _context = context;
    }

    public async ValueTask<GetCatalogsResponse> Handle(
        GetCatalogsQuery query, CancellationToken cancellationToken)
    {
        // Catálogos pequeños y sin filtro global de borrado lógico: se excluyen a mano.
        // Consultas secuenciales a propósito: un DbContext no admite operaciones concurrentes.
        var assetTypes = await _context.AssetTypes.AsNoTracking()
            .Where(x => !x.IsDeleted).OrderBy(x => x.Name)
            .Select(x => new CatalogItem(x.AssetTypeId, x.Code, x.Name))
            .ToListAsync(cancellationToken);

        // Estados de activo e incidente: orden del ciclo de vida, no alfabético.
        var assetStatuses = await _context.AssetStatuses.AsNoTracking()
            .Where(x => !x.IsDeleted).OrderBy(x => x.AssetStatusId)
            .Select(x => new CatalogItem(x.AssetStatusId, x.Code, x.Name))
            .ToListAsync(cancellationToken);

        var locationTypes = await _context.LocationTypes.AsNoTracking()
            .Where(x => !x.IsDeleted).OrderBy(x => x.Name)
            .Select(x => new CatalogItem(x.LocationTypeId, x.Code, x.Name))
            .ToListAsync(cancellationToken);

        var incidentTypes = await _context.IncidentTypes.AsNoTracking()
            .Where(x => !x.IsDeleted).OrderBy(x => x.Name)
            .Select(x => new CatalogItem(x.IncidentTypeId, x.Code, x.Name))
            .ToListAsync(cancellationToken);

        var incidentPriorities = await _context.IncidentPriorities.AsNoTracking()
            .Where(x => !x.IsDeleted).OrderBy(x => x.IncidentPriorityId)
            .Select(x => new CatalogItem(x.IncidentPriorityId, x.Code, x.Name))
            .ToListAsync(cancellationToken);

        var incidentStatuses = await _context.IncidentStatuses.AsNoTracking()
            .Where(x => !x.IsDeleted).OrderBy(x => x.IncidentStatusId)
            .Select(x => new CatalogItem(x.IncidentStatusId, x.Code, x.Name))
            .ToListAsync(cancellationToken);

        var maintenanceTypes = await _context.MaintenanceTypes.AsNoTracking()
            .Where(x => !x.IsDeleted).OrderBy(x => x.Name)
            .Select(x => new CatalogItem(x.MaintenanceTypeId, x.Code, x.Name))
            .ToListAsync(cancellationToken);

        var movementReasons = await _context.MovementReasons.AsNoTracking()
            .Where(x => !x.IsDeleted).OrderBy(x => x.Name)
            .Select(x => new CatalogItem(x.MovementReasonId, x.Code, x.Name))
            .ToListAsync(cancellationToken);

        var assignmentTypes = await _context.AssignmentTypes.AsNoTracking()
            .Where(x => !x.IsDeleted).OrderBy(x => x.Name)
            .Select(x => new CatalogItem(x.AssignmentTypeId, x.Code, x.Name))
            .ToListAsync(cancellationToken);

        var componentTypes = await _context.ComponentTypes.AsNoTracking()
            .Where(x => !x.IsDeleted).OrderBy(x => x.Name)
            .Select(x => new CatalogItem(x.ComponentTypeId, x.Code, x.Name))
            .ToListAsync(cancellationToken);

        var disposalReasons = await _context.DisposalReasons.AsNoTracking()
            .Where(x => !x.IsDeleted).OrderBy(x => x.Name)
            .Select(x => new CatalogItem(x.DisposalReasonId, x.Code, x.Name))
            .ToListAsync(cancellationToken);

        var softwareTypes = await _context.SoftwareTypes.AsNoTracking()
            .Where(x => !x.IsDeleted).OrderBy(x => x.Name)
            .Select(x => new CatalogItem(x.SoftwareTypeId, x.Code, x.Name))
            .ToListAsync(cancellationToken);

        var licenseTypes = await _context.LicenseTypes.AsNoTracking()
            .Where(x => !x.IsDeleted).OrderBy(x => x.Name)
            .Select(x => new CatalogItem(x.LicenseTypeId, x.Code, x.Name))
            .ToListAsync(cancellationToken);

        return new GetCatalogsResponse(
            assetTypes, assetStatuses, locationTypes, incidentTypes, incidentPriorities,
            incidentStatuses, maintenanceTypes, movementReasons, assignmentTypes,
            componentTypes, disposalReasons, softwareTypes, licenseTypes);
    }
}
