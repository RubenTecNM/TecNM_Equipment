using Mediator;
using TecnmInventario.Api.Common.Authorization;
using TecnmInventario.Api.Data;
using TecnmInventario.Api.Features.Maintenance.Shared;

namespace TecnmInventario.Api.Features.Maintenance.GetMaintenanceTechnicians;

public sealed class GetMaintenanceTechniciansHandler
    : IQueryHandler<GetMaintenanceTechniciansQuery, IReadOnlyList<MaintenanceTechnicianResponse>>
{
    private readonly TecnmInventarioDbContext _context;
    private readonly IRolePermissionService _rolePermissions;

    public GetMaintenanceTechniciansHandler(TecnmInventarioDbContext context, IRolePermissionService rolePermissions)
    {
        _context = context;
        _rolePermissions = rolePermissions;
    }

    // La regla de "qué es un técnico" vive en MaintenanceTechnicianRule (la misma
    // que valida performedBy al programar o iniciar); aquí solo se lista.
    public async ValueTask<IReadOnlyList<MaintenanceTechnicianResponse>> Handle(
        GetMaintenanceTechniciansQuery query, CancellationToken cancellationToken)
    {
        var technicians = await MaintenanceTechnicianRule.ListAsync(_context, _rolePermissions, cancellationToken);
        return technicians.Select(t => new MaintenanceTechnicianResponse(t.UserId, t.FullName)).ToList();
    }
}
