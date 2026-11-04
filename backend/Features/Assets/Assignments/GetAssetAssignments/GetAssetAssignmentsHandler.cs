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
        // A6: el historial se devuelve aunque el activo esté borrado lógicamente.
        var assetExists = await _context.Assets
            .IgnoreQueryFilters(["SoftDelete"])
            .AnyAsync(a => a.AssetId == query.AssetId, cancellationToken);

        if (!assetExists)
        {
            throw new KeyNotFoundException("El activo solicitado no existe.");
        }

        // D3: el historial de asignaciones es inmutable; la ubicación borrada
        // lógicamente debe seguir apareciendo con su nombre original.
        // IgnoreQueryFilters aplica a toda la query, incluidas las navegaciones.
        // Sin paginar a propósito: el historial de un activo es corto; las últimas 50 alcanzan.
        return await _context.AssetAssignments
            .IgnoreQueryFilters(["SoftDelete"])
            .Where(a => a.AssetId == query.AssetId)
            .OrderByDescending(a => a.AssignedAt)
            .Take(50)
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
