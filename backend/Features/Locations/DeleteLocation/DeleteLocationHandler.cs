using Mediator;
using Microsoft.EntityFrameworkCore;
using TecnmInventario.Api.Common.Deletion;
using TecnmInventario.Api.Common.Errors;
using TecnmInventario.Api.Data;

namespace TecnmInventario.Api.Features.Locations.DeleteLocation;

public sealed class DeleteLocationHandler : ICommandHandler<DeleteLocationCommand, DeleteLocationResponse>
{
    private readonly TecnmInventarioDbContext _context;
    private readonly ISoftDeleteAuditor _softDeleteAuditor;

    public DeleteLocationHandler(TecnmInventarioDbContext context, ISoftDeleteAuditor softDeleteAuditor)
    {
        _context = context;
        _softDeleteAuditor = softDeleteAuditor;
    }

    public async ValueTask<DeleteLocationResponse> Handle(
        DeleteLocationCommand command, CancellationToken cancellationToken)
    {
        var location = await _context.Locations
            .FirstOrDefaultAsync(l => l.LocationId == command.LocationId, cancellationToken);

        if (location is null)
        {
            throw new KeyNotFoundException("La ubicación solicitada no existe.");
        }

        // E1/E4: "en uso" incluye referencias en cualquier estado (también
        // borradas lógicamente), por eso IgnoreQueryFilters en las
        // comprobaciones sobre entidades con is_deleted (Location, Asset,
        // BuildingSetting). AssetAssignment y AssetMovement no tienen
        // is_deleted -- son historial puro, no necesitan IgnoreQueryFilters.
        var hasChildren = await _context.Locations
            .IgnoreQueryFilters(["SoftDelete"])
            .AnyAsync(l => l.ParentLocationId == command.LocationId, cancellationToken);

        if (hasChildren)
        {
            throw new ReferenceInUseException(
                "No se puede eliminar la ubicación: tiene sub-ubicaciones registradas.");
        }

        var hasAssets = await _context.Assets
            .IgnoreQueryFilters(["SoftDelete"])
            .AnyAsync(a => a.CurrentLocationId == command.LocationId, cancellationToken);

        if (hasAssets)
        {
            throw new ReferenceInUseException(
                "No se puede eliminar la ubicación: tiene activos registrados.");
        }

        var hasAssignments = await _context.AssetAssignments
            .AnyAsync(a => a.LocationId == command.LocationId, cancellationToken);

        if (hasAssignments)
        {
            throw new ReferenceInUseException(
                "No se puede eliminar la ubicación: tiene asignaciones de activos registradas.");
        }

        var hasMovements = await _context.AssetMovements
            .AnyAsync(
                m => m.FromLocationId == command.LocationId || m.ToLocationId == command.LocationId,
                cancellationToken);

        if (hasMovements)
        {
            throw new ReferenceInUseException(
                "No se puede eliminar la ubicación: tiene movimientos de activos registrados.");
        }

        var hasBuildingSettings = await _context.BuildingSettings
            .IgnoreQueryFilters(["SoftDelete"])
            .AnyAsync(b => b.LocationId == command.LocationId, cancellationToken);

        if (hasBuildingSettings)
        {
            throw new ReferenceInUseException(
                "No se puede eliminar la ubicación: tiene configuraciones de edificio registradas.");
        }

        var beforeSnapshot = new
        {
            location.LocationId,
            location.Code,
            location.Name,
            location.LocationTypeId,
            location.ParentLocationId,
            location.CreatedAt,
            location.CreatedBy,
            location.Version
        };

        await _softDeleteAuditor.DeleteAsync(
            location,
            recordId: location.LocationId,
            expectedVersion: command.ExpectedVersion,
            tableName: "locations",
            snapshotBeforeDelete: beforeSnapshot,
            cancellationToken);

        return new DeleteLocationResponse(location.LocationId, true);
    }
}
