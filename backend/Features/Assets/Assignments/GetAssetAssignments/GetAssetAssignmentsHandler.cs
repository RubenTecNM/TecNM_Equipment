using Mediator;
using Microsoft.EntityFrameworkCore;
using TecnmInventario.Api.Data;

namespace TecnmInventario.Api.Features.Assets.Assignments.GetAssetAssignments;

public sealed class GetAssetAssignmentsHandler
    : IQueryHandler<GetAssetAssignmentsQuery, IReadOnlyList<GetAssetAssignmentsResponse>>
{
    private readonly TecnmInventarioDbContext _context;

    public GetAssetAssignmentsHandler(TecnmInventarioDbContext context)
    {
        _context = context;
    }

    public async ValueTask<IReadOnlyList<GetAssetAssignmentsResponse>> Handle(
        GetAssetAssignmentsQuery query, CancellationToken cancellationToken)
    {
        var assetExists = await _context.Assets
            .AnyAsync(a => a.AssetId == query.AssetId && !a.IsDeleted, cancellationToken);

        if (!assetExists)
        {
            throw new KeyNotFoundException("El activo solicitado no existe.");
        }

        // Sin paginar a propósito: el historial de asignaciones de un
        // activo es corto; las últimas 50 alcanzan para la ficha.
        return await _context.AssetAssignments
            .Where(a => a.AssetId == query.AssetId)
            .OrderByDescending(a => a.AssignedAt)
            .Take(50)
            .Include(a => a.AssignedToNavigation)
            .Include(a => a.AssignmentType)
            .Include(a => a.Location)
            .Select(a => new GetAssetAssignmentsResponse(
                a.AssetAssignmentId,
                a.AssignedTo,
                a.AssignedToNavigation.Username,
                a.AssignmentTypeId,
                a.AssignmentType.Code,
                a.AssignmentType.Name,
                a.LocationId,
                a.Location != null ? a.Location.Name : null,
                a.AssignedAt,
                a.ReturnedAt,
                a.ReturnedAt == null,
                a.Notes))
            .ToListAsync(cancellationToken);
    }
}
