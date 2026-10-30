namespace TecnmInventario.Api.Common.Authorization;

/// <summary>
/// Fuente única de verdad para "¿este rol tiene este permiso?", cacheada.
/// Consumida tanto por PermissionAuthorizationHandler (policies de ASP.NET Core)
/// como por Handlers que necesitan resolver permisos dinámicamente en tiempo
/// de ejecución (ej. Documents, según el OwnerType).
/// </summary>
public interface IRolePermissionService
{
    Task<bool> HasPermissionAsync(string roleCode, string permissionCode, CancellationToken cancellationToken);

    /// <summary>
    /// Invalida la entrada de caché de un rol. Debe llamarse siempre que
    /// role_permissions cambie para ese rol (ej. desde AssignPermissions),
    /// para que el cambio surta efecto sin esperar el TTL.
    /// </summary>
    void InvalidateCache(string roleCode);
}
