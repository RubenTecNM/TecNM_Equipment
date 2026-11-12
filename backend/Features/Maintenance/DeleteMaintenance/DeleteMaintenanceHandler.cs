using Mediator;
using Microsoft.EntityFrameworkCore;
using TecnmInventario.Api.Common.Deletion;
using TecnmInventario.Api.Common.Errors;
using TecnmInventario.Api.Data;
using TecnmInventario.Api.Features.Documents;
using TecnmInventario.Api.Features.Maintenance.Shared;

namespace TecnmInventario.Api.Features.Maintenance.DeleteMaintenance;

public sealed class DeleteMaintenanceHandler : ICommandHandler<DeleteMaintenanceCommand, DeleteMaintenanceResponse>
{
    private readonly TecnmInventarioDbContext _context;
    private readonly ISoftDeleteAuditor _softDeleteAuditor;

    public DeleteMaintenanceHandler(TecnmInventarioDbContext context, ISoftDeleteAuditor softDeleteAuditor)
    {
        _context = context;
        _softDeleteAuditor = softDeleteAuditor;
    }

    public async ValueTask<DeleteMaintenanceResponse> Handle(
        DeleteMaintenanceCommand command, CancellationToken cancellationToken)
    {
        // Ya borrado o inexistente -> 404 (LoadRecordAsync filtra !IsDeleted de la raíz).
        var record = await MaintenanceGuards.LoadRecordAsync(_context, command.MaintenanceRecordId, cancellationToken);

        MaintenanceGuards.EnsureVersion(record, command.ExpectedVersion);

        // Un mantenimiento en curso tiene al activo EN_MANTENIMIENTO: primero se cierra o se cancela.
        if (record.Status == MaintenanceStatusCodes.EnCurso)
        {
            throw new BusinessRuleViolationException("Cierra o cancela el mantenimiento antes de eliminarlo.");
        }

        // "En uso" incluye referencias en CUALQUIER estado (también documentos borrados
        // lógicamente): el historial no puede quedar apuntando a un registro borrado.
        var hasDocuments = await _context.Documents
            .IgnoreQueryFilters(["SoftDelete"])
            .AnyAsync(d => d.OwnerType == DocumentOwnerTypes.Maintenance
                           && d.OwnerId == record.MaintenanceRecordId, cancellationToken);
        if (hasDocuments)
        {
            throw new ReferenceInUseException("El mantenimiento tiene documentos asociados.");
        }

        var beforeSnapshot = new
        {
            record.MaintenanceRecordId,
            record.AssetId,
            record.MaintenanceTypeId,
            record.Status,
            record.ScheduledAt,
            record.StartedAt,
            record.PerformedAt,
            record.PerformedBy,
            record.Description,
            record.Cost,
            record.CreatedAt,
            record.CreatedBy,
            record.Version
        };

        await _softDeleteAuditor.DeleteAsync(
            record,
            recordId: record.MaintenanceRecordId,
            expectedVersion: command.ExpectedVersion,
            tableName: "maintenance_records",
            snapshotBeforeDelete: beforeSnapshot,
            cancellationToken);

        return new DeleteMaintenanceResponse(record.MaintenanceRecordId, true);
    }
}
