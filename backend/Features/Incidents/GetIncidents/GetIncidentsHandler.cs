using Mediator;
using Microsoft.EntityFrameworkCore;
using TecnmInventario.Api.Common.Domain;
using TecnmInventario.Api.Common.Pagination;
using TecnmInventario.Api.Data;

namespace TecnmInventario.Api.Features.Incidents.GetIncidents;

public sealed class GetIncidentsHandler
    : IQueryHandler<GetIncidentsQuery, PagedResult<GetIncidentsResponse>>
{
    private readonly TecnmInventarioDbContext _context;

    public GetIncidentsHandler(TecnmInventarioDbContext context)
    {
        _context = context;
    }

    public async ValueTask<PagedResult<GetIncidentsResponse>> Handle(
        GetIncidentsQuery query, CancellationToken cancellationToken)
    {
        if (query.AssetId.HasValue)
        {
            var assetId = query.AssetId.Value;
            var assetExists = await _context.Assets
                .AnyAsync(a => a.AssetId == assetId && !a.IsDeleted, cancellationToken);
            if (!assetExists)
            {
                throw new KeyNotFoundException("El activo solicitado no existe.");
            }
        }

        // El filtro global de Incident ya excluye los eliminados lógicamente.
        var incidents = _context.Incidents.AsQueryable();

        if (query.AssetId.HasValue)
        {
            var assetId = query.AssetId.Value;
            incidents = incidents.Where(i => i.AssetId == assetId);
        }

        if (!string.IsNullOrWhiteSpace(query.StatusCode))
        {
            var statusCode = query.StatusCode.Trim().ToUpperInvariant();
            incidents = incidents.Where(i => i.IncidentStatus.Code == statusCode);
        }

        if (query.OnlyOpen == true)
        {
            var openCodes = IncidentStatusCodes.Open.ToArray();
            incidents = incidents.Where(i => openCodes.Contains(i.IncidentStatus.Code));
        }

        if (!string.IsNullOrWhiteSpace(query.PriorityCode))
        {
            var priorityCode = query.PriorityCode.Trim().ToUpperInvariant();
            incidents = incidents.Where(i => i.IncidentPriority.Code == priorityCode);
        }

        if (query.IncidentTypeId.HasValue)
        {
            var typeId = query.IncidentTypeId.Value;
            incidents = incidents.Where(i => i.IncidentTypeId == typeId);
        }

        if (query.AssignedTo.HasValue)
        {
            var assignedTo = query.AssignedTo.Value;
            incidents = incidents.Where(i => i.AssignedTo == assignedTo);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim().ToLower();
            incidents = incidents.Where(i => i.Title.ToLower().Contains(term));
        }

        var totalCount = await incidents.CountAsync(cancellationToken);

        var items = await incidents
            .OrderByDescending(i => i.ReportedAt)
            .ThenByDescending(i => i.IncidentId)
            .Skip((query.PageNumber - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(i => new GetIncidentsResponse(
                i.IncidentId,
                i.AssetId,
                i.Asset.InventoryCode,
                i.Title,
                i.IncidentType.Code,
                i.IncidentType.Name,
                i.IncidentPriority.Code,
                i.IncidentPriority.Name,
                i.IncidentStatus.Code,
                i.IncidentStatus.Name,
                i.ReportedByNavigation.Username,
                i.AssignedToNavigation != null ? i.AssignedToNavigation.Username : null,
                i.ReportedAt,
                i.ResolvedAt,
                i.Version))
            .ToListAsync(cancellationToken);

        return new PagedResult<GetIncidentsResponse>(items, query.PageNumber, query.PageSize, totalCount);
    }
}
