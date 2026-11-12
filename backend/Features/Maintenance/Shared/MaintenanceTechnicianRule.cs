using Microsoft.EntityFrameworkCore;
using TecnmInventario.Api.Common.Authorization;
using TecnmInventario.Api.Core.Entities;
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

    public sealed record TechnicianRow(long UserId, string FullName);

    /// <summary>
    /// Usuarios activos, no borrados, de un rol no borrado. Base común de
    /// IsValidAsync y ListAsync: un solo criterio de "candidato a técnico".
    /// </summary>
    private static IQueryable<AppUser> ActiveUsers(TecnmInventarioDbContext context)
        => context.AppUsers.Where(u => !u.IsDeleted && u.Status == "activo" && !u.Role.IsDeleted);

    public static async Task<bool> IsValidAsync(
        TecnmInventarioDbContext context,
        IRolePermissionService rolePermissions,
        long userId,
        CancellationToken cancellationToken)
    {
        var roleCode = await ActiveUsers(context)
            .Where(u => u.UserId == userId)
            .Select(u => u.Role.Code)
            .FirstOrDefaultAsync(cancellationToken);

        return roleCode is not null
               && await rolePermissions.HasPermissionAsync(roleCode, MaintenancePermissions.Manage, cancellationToken);
    }

    /// <summary>Técnicos válidos (mismo criterio que IsValidAsync), ordenados por nombre.</summary>
    public static async Task<IReadOnlyList<TechnicianRow>> ListAsync(
        TecnmInventarioDbContext context,
        IRolePermissionService rolePermissions,
        CancellationToken cancellationToken)
    {
        var candidates = await ActiveUsers(context)
            .OrderBy(u => u.FirstName).ThenBy(u => u.LastName).ThenBy(u => u.UserId)
            .Select(u => new { u.UserId, FullName = u.FirstName + " " + u.LastName, RoleCode = u.Role.Code })
            .ToListAsync(cancellationToken);

        var allowedRoles = new HashSet<string>();
        foreach (var roleCode in candidates.Select(c => c.RoleCode).Distinct())
        {
            if (await rolePermissions.HasPermissionAsync(roleCode, MaintenancePermissions.Manage, cancellationToken))
            {
                allowedRoles.Add(roleCode);
            }
        }

        return candidates
            .Where(c => allowedRoles.Contains(c.RoleCode))
            .Select(c => new TechnicianRow(c.UserId, c.FullName))
            .ToList();
    }
}
