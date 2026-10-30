using Mediator;
using Microsoft.EntityFrameworkCore;
using TecnmInventario.Api.Common.Deletion;
using TecnmInventario.Api.Common.Domain;
using TecnmInventario.Api.Common.Errors;
using TecnmInventario.Api.Data;

namespace TecnmInventario.Api.Features.Incidents.DeleteIncident;

public sealed class DeleteIncidentHandler : ICommandHandler<DeleteIncidentCommand, DeleteIncidentResponse>
{
    private readonly TecnmInventarioDbContext _context;
    private readonly ISoftDeleteAuditor _softDeleteAuditor;

    public DeleteIncidentHandler(TecnmInventarioDbContext context, ISoftDeleteAuditor softDeleteAuditor)
    {
        _context = context;
        _softDeleteAuditor = softDeleteAuditor;
    }

    public async ValueTask<DeleteIncidentResponse> Handle(
        DeleteIncidentCommand command, CancellationToken cancellationToken)
    {
        var incident = await _context.Incidents
            .Include(i => i.IncidentStatus)
            .FirstOrDefaultAsync(i => i.IncidentId == command.IncidentId, cancellationToken)
            ?? throw new KeyNotFoundException("El incidente solicitado no existe.");

        var statusCode = incident.IncidentStatus.Code;

        // Un incidente abierto mantiene al activo fuera de circulación: borrarlo
        // dejaría el estado del activo sin causa visible. Primero se resuelve.
        if (IncidentStatusCodes.Open.Contains(statusCode))
        {
            throw new BusinessRuleViolationException(
                "No se puede eliminar un incidente abierto. Resuélvalo primero.");
        }

        var beforeSnapshot = new
        {
            incident.IncidentId,
            incident.AssetId,
            incident.IncidentTypeId,
            incident.IncidentPriorityId,
            Status = statusCode,
            incident.ReportedBy,
            incident.AssignedTo,
            incident.Title,
            incident.Description,
            incident.Diagnosis,
            incident.Solution,
            incident.ReportedAt,
            incident.ResolvedAt,
            incident.Version
        };

        await _softDeleteAuditor.DeleteAsync(
            incident,
            recordId: incident.IncidentId,
            expectedVersion: command.ExpectedVersion,
            tableName: "incidents",
            snapshotBeforeDelete: beforeSnapshot,
            cancellationToken);

        return new DeleteIncidentResponse(incident.IncidentId, true);
    }
}
