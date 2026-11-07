using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using TecnmInventario.Api.Data;

namespace TecnmInventario.Api.Common.Authorization;

public sealed class RolePermissionService : IRolePermissionService
{
    private readonly TecnmInventarioDbContext _dbContext;
    private readonly IMemoryCache _cache;

    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);

    public RolePermissionService(TecnmInventarioDbContext dbContext, IMemoryCache cache)
    {
        _dbContext = dbContext;
        _cache = cache;
    }

    public async Task<bool> HasPermissionAsync(string roleCode, string permissionCode, CancellationToken cancellationToken)
    {
        var permissions = await _cache.GetOrCreateAsync(
            CacheKeyForRole(roleCode),
            async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = CacheDuration;
                // H3: Where explícito sobre Permission, código de seguridad que
                // no depende solo del filtro global "SoftDelete" (defensa en
                // profundidad: si alguna consulta futura usa IgnoreQueryFilters
                // aquí por error, esto sigue cerrando el acceso).
                return await _dbContext.Roles
                    .Where(r => r.Code == roleCode && !r.IsDeleted)
                    .SelectMany(r => r.Permissions.Where(p => !p.IsDeleted).Select(p => p.Code))
                    .ToListAsync(cancellationToken);
            });

        return permissions is not null && permissions.Contains(permissionCode);
    }

    public void InvalidateCache(string roleCode)
    {
        _cache.Remove(CacheKeyForRole(roleCode));
    }

    private static string CacheKeyForRole(string roleCode) => $"role-permissions:{roleCode}";
}
