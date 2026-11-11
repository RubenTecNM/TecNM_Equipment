using Microsoft.EntityFrameworkCore;
using TecnmInventario.Api.Common.Authorization;
using TecnmInventario.Api.Data;

namespace TecnmInventario.Api.Features.Maintenance.Shared;

/// <summary>
/// Un técnico de mantenimiento es un usuario activo, no borrado lógicamente, cuyo
/// rol tiene maintenance.manage (mismo patrón que AssignIncident con
/// incidents.resolve). Un único resultado para "no existe", "inactivo" y "sin
/// permiso": no revela qué usuarios existen.
/// </summary>
public static class MaintenanceTechnicianRule
{
    public const string InvalidMessage =
        "El técnico especificado no existe, no está activo o no tiene permisos de mantenimiento.";

    public static async Task<bool> IsValidAsync(
        TecnmInventarioDbContext context,
        IRolePermissionService rolePermissions,
        long userId,
        CancellationToken cancellationToken)
    {
        var roleId = await context.AppUsers
            .Where(u => u.UserId == userId && !u.IsDeleted && u.Status == "activo")
            .Select(u => (long?)u.RoleId)
            .FirstOrDefaultAsync(cancellationToken);

        if (roleId is null)
        {
            return false;
        }

        var roleCode = await context.Roles
            .Where(r => r.RoleId == roleId)
            .Select(r => r.Code)
            .FirstAsync(cancellationToken);

        return await rolePermissions.HasPermissionAsync(roleCode, MaintenancePermissions.Manage, cancellationToken);
    }
}
