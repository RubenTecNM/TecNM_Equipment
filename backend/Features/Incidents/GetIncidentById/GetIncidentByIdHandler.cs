using Mediator;
using Microsoft.EntityFrameworkCore;
using TecnmInventario.Api.Data;

namespace TecnmInventario.Api.Features.Incidents.GetIncidentById;

public sealed class GetIncidentByIdHandler : IQueryHandler<GetIncidentByIdQuery, GetIncidentByIdResponse>
{
    private readonly TecnmInventarioDbContext _context;

    public GetIncidentByIdHandler(TecnmInventarioDbContext context)
    {
        _context = context;
    }

    public async ValueTask<GetIncidentByIdResponse> Handle(
        GetIncidentByIdQuery query, CancellationToken cancellationToken)
    {
        var result = await _context.Incidents
            .Where(i => i.IncidentId == query.IncidentId)
            .Select(i => new GetIncidentByIdResponse(
                i.IncidentId,
                i.AssetId,
                i.Asset.InventoryCode,
                i.Asset.AssetStatus.Code,
                i.Title,
                i.Description,
                i.Diagnosis,
                i.Solution,
                i.IncidentTypeId,
                i.IncidentType.Code,
                i.IncidentType.Name,
                i.IncidentPriorityId,
                i.IncidentPriority.Code,
                i.IncidentPriority.Name,
                i.IncidentStatus.Code,
                i.IncidentStatus.Name,
                i.ReportedBy,
                i.ReportedByNavigation.Username,
                i.ReportedByNavigation.FirstName + " " + i.ReportedByNavigation.LastName,
                i.AssignedTo,
                i.AssignedToNavigation != null ? i.AssignedToNavigation.Username : null,
                i.AssignedToNavigation != null
                    ? i.AssignedToNavigation.FirstName + " " + i.AssignedToNavigation.LastName
                    : null,
                i.ReportedAt,
                i.ResolvedAt,
                i.CreatedAt,
                i.UpdatedAt,
                i.Version))
            .FirstOrDefaultAsync(cancellationToken);

        return result ?? throw new KeyNotFoundException("El incidente solicitado no existe.");
    }
}
