using FluentValidation;
using Microsoft.EntityFrameworkCore;
using TecnmInventario.Api.Data;

namespace TecnmInventario.Api.Features.Roles.AssignPermissions;

public class AssignPermissionsValidator : AbstractValidator<AssignPermissionsCommand>
{
    private const string ProtectedRoleCode = "ADMIN";
    private const string ProtectedPermissionCode = "roles.manage";

    private readonly TecnmInventarioDbContext _context;

    public AssignPermissionsValidator(TecnmInventarioDbContext context)
    {
        _context = context;

        RuleFor(x => x.RoleId)
            .MustAsync(RoleExists).WithMessage("El rol especificado no existe.");

        RuleFor(x => x.ExpectedVersion)
            .GreaterThan(0).WithMessage("La versión esperada del rol es obligatoria.");

        RuleFor(x => x.PermissionCodes)
            .MustAsync(AllPermissionCodesExist)
            .WithMessage("Uno o más códigos de permiso no existen o están inactivos.");

        RuleFor(x => x)
            .MustAsync(NotStripAdminOfRolesManage)
            .WithMessage($"No se puede quitar el permiso '{ProtectedPermissionCode}' al rol {ProtectedRoleCode}: dejaría al sistema sin nadie que pueda administrar roles.")
            .WithName("PermissionCodes");
    }

    private async Task<bool> RoleExists(long id, CancellationToken ct)
        => await _context.Roles.AnyAsync(r => r.RoleId == id && !r.IsDeleted, ct);

    private async Task<bool> AllPermissionCodesExist(IReadOnlyList<string> codes, CancellationToken ct)
    {
        if (codes.Count == 0)
        {
            return true;
        }

        var distinctCodes = codes.Distinct().ToList();
        var existingCount = await _context.Permissions
            .CountAsync(p => distinctCodes.Contains(p.Code) && !p.IsDeleted, ct);

        return existingCount == distinctCodes.Count;
    }

    private async Task<bool> NotStripAdminOfRolesManage(AssignPermissionsCommand command, CancellationToken ct)
    {
        var roleCode = await _context.Roles
            .Where(r => r.RoleId == command.RoleId && !r.IsDeleted)
            .Select(r => r.Code)
            .FirstOrDefaultAsync(ct);

        if (roleCode != ProtectedRoleCode)
        {
            return true;
        }

        return command.PermissionCodes.Contains(ProtectedPermissionCode);
    }
}
