using Mediator;
using Microsoft.EntityFrameworkCore;
using TecnmInventario.Api.Data;
using TecnmInventario.Api.Features.Maintenance.Shared;

namespace TecnmInventario.Api.Features.Maintenance.GetMaintenanceById;

public sealed class GetMaintenanceByIdHandler
    : IQueryHandler<GetMaintenanceByIdQuery, GetMaintenanceByIdResponse>
{
    private readonly TecnmInventarioDbContext _context;

    public GetMaintenanceByIdHandler(TecnmInventarioDbContext context)
    {
        _context = context;
    }

    public async ValueTask<GetMaintenanceByIdResponse> Handle(
        GetMaintenanceByIdQuery query, CancellationToken cancellationToken)
    {
        // Regla 20: el detalle de un registro de un activo borrado se ve igual
        // (historial); por eso IgnoreQueryFilters + !IsDeleted explícito de la raíz.
        var result = await _context.MaintenanceRecords
            .IgnoreQueryFilters(["SoftDelete"])
            .Where(m => !m.IsDeleted && m.MaintenanceRecordId == query.MaintenanceRecordId)
            .Select(m => new GetMaintenanceByIdResponse(
                m.MaintenanceRecordId,
                m.Status,
                m.AssetId,
                m.Asset.InventoryCode,
                m.Asset.Hostname,
                m.Asset.IsDeleted,
                m.MaintenanceType.Code,
                m.MaintenanceType.Name,
                m.PerformedByNavigation != null
                    ? new MaintenanceUserRef(
                        m.PerformedByNavigation.UserId,
                        m.PerformedByNavigation.FirstName + " " + m.PerformedByNavigation.LastName)
                    : null,
                m.ScheduledAt,
                m.StartedAt,
                m.PerformedAt,
                m.NextMaintenanceAt,
                m.IncidentId,
                m.Version,
                m.Description,
                m.PartsUsed,
                m.Cost,
                m.StatusBefore != null ? new MaintenanceStatusRef(m.StatusBefore.Code, m.StatusBefore.Name) : null,
                m.StatusAfter != null ? new MaintenanceStatusRef(m.StatusAfter.Code, m.StatusAfter.Name) : null,
                m.Incident != null
                    ? new MaintenanceIncidentRef(m.Incident.IncidentId, m.Incident.IncidentStatus.Code, m.Incident.Title)
                    : null,
                m.CancelledAt,
                m.CancellationReason,
                m.CreatedAt,
                m.CreatedByNavigation != null
                    ? new MaintenanceUserRef(
                        m.CreatedByNavigation.UserId,
                        m.CreatedByNavigation.FirstName + " " + m.CreatedByNavigation.LastName)
                    : null))
            .FirstOrDefaultAsync(cancellationToken);

        return result ?? throw new KeyNotFoundException("El mantenimiento solicitado no existe.");
    }
}
