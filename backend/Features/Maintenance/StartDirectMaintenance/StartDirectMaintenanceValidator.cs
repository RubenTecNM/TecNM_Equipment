using FluentValidation;
using Microsoft.EntityFrameworkCore;
using TecnmInventario.Api.Common.Authorization;
using TecnmInventario.Api.Data;
using TecnmInventario.Api.Features.Maintenance.Shared;

namespace TecnmInventario.Api.Features.Maintenance.StartDirectMaintenance;

// El estado del activo (DADO_DE_BAJA) NO se valida aquí: se decide en el handler,
// con el activo ya bloqueado (regla 19).
public class StartDirectMaintenanceValidator : AbstractValidator<StartDirectMaintenanceCommand>
{
    private readonly TecnmInventarioDbContext _context;
    private readonly IRolePermissionService _rolePermissions;

    public StartDirectMaintenanceValidator(TecnmInventarioDbContext context, IRolePermissionService rolePermissions)
    {
        _context = context;
        _rolePermissions = rolePermissions;

        RuleFor(x => x.AssetId)
            .MustAsync(AssetExists).WithMessage("El activo especificado no existe.");

        RuleFor(x => x.MaintenanceTypeId)
            .MustAsync(TypeExists).WithMessage("El tipo de mantenimiento especificado no existe.");

        RuleFor(x => x.PerformedBy)
            .MustAsync(async (id, ct) => await MaintenanceTechnicianRule.IsValidAsync(_context, _rolePermissions, id!.Value, ct))
            .WithMessage(MaintenanceTechnicianRule.InvalidMessage)
            .When(x => x.PerformedBy is not null);

        RuleFor(x => x.IncidentId)
            .GreaterThan(0).WithMessage("El incidente indicado no es válido.")
            .When(x => x.IncidentId is not null);

        RuleFor(x => x.Description)
            .MaximumLength(4000)
            .When(x => x.Description is not null);
    }

    private async Task<bool> AssetExists(long id, CancellationToken ct)
        => await _context.Assets.AnyAsync(a => a.AssetId == id && !a.IsDeleted, ct);

    private async Task<bool> TypeExists(long id, CancellationToken ct)
        => await _context.MaintenanceTypes.AnyAsync(t => t.MaintenanceTypeId == id && !t.IsDeleted, ct);
}
