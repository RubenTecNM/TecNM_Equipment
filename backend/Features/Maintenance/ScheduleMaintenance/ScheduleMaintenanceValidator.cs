using FluentValidation;
using Microsoft.EntityFrameworkCore;
using TecnmInventario.Api.Common.Authorization;
using TecnmInventario.Api.Common.Domain;
using TecnmInventario.Api.Data;
using TecnmInventario.Api.Features.Maintenance.Shared;

namespace TecnmInventario.Api.Features.Maintenance.ScheduleMaintenance;

public class ScheduleMaintenanceValidator : AbstractValidator<ScheduleMaintenanceCommand>
{
    private readonly TecnmInventarioDbContext _context;
    private readonly IRolePermissionService _rolePermissions;

    public ScheduleMaintenanceValidator(TecnmInventarioDbContext context, IRolePermissionService rolePermissions)
    {
        _context = context;
        _rolePermissions = rolePermissions;

        RuleFor(x => x.AssetId)
            .MustAsync(AssetExists).WithMessage("El activo especificado no existe.");

        RuleFor(x => x.AssetId)
            .MustAsync(AssetNotDisposed)
            .WithMessage("No se puede programar un mantenimiento sobre un activo dado de baja.");

        RuleFor(x => x.MaintenanceTypeId)
            .MustAsync(TypeExists).WithMessage("El tipo de mantenimiento especificado no existe.");

        RuleFor(x => x.ScheduledAt)
            .NotNull().WithMessage("La fecha programada es obligatoria.");

        RuleFor(x => x.PerformedBy)
            .MustAsync(async (id, ct) => await MaintenanceTechnicianRule.IsValidAsync(_context, _rolePermissions, id!.Value, ct))
            .WithMessage(MaintenanceTechnicianRule.InvalidMessage)
            .When(x => x.PerformedBy is not null);

        RuleFor(x => x.Description)
            .MaximumLength(4000)
            .When(x => x.Description is not null);
    }

    private async Task<bool> AssetExists(long id, CancellationToken ct)
        => await _context.Assets.AnyAsync(a => a.AssetId == id && !a.IsDeleted, ct);

    // Si el activo no existe no es este el mensaje que toca: lo da AssetExists.
    private async Task<bool> AssetNotDisposed(long id, CancellationToken ct)
        => !await _context.Assets.AnyAsync(
            a => a.AssetId == id && !a.IsDeleted && a.AssetStatus.Code == AssetStatusCodes.DadoDeBaja, ct);

    // MaintenanceType es un catálogo exento del filtro: !IsDeleted explícito.
    private async Task<bool> TypeExists(long id, CancellationToken ct)
        => await _context.MaintenanceTypes.AnyAsync(t => t.MaintenanceTypeId == id && !t.IsDeleted, ct);
}
