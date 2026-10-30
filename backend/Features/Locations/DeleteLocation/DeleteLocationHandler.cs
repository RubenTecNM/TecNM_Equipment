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

        var hasChildren = await _context.Locations
            .AnyAsync(l => l.ParentLocationId == command.LocationId, cancellationToken);

        if (hasChildren)
        {
            throw new BusinessRuleViolationException(
                "No se puede eliminar una ubicación que tiene sub-ubicaciones. Elimine o reubique primero las sub-ubicaciones.");
        }

        var hasAssets = await _context.Assets
            .AnyAsync(a => a.CurrentLocationId == command.LocationId && !a.IsDeleted, cancellationToken);

        if (hasAssets)
        {
            throw new BusinessRuleViolationException(
                "No se puede eliminar una ubicación que tiene activos asignados. Reubique los activos primero.");
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
