using Microsoft.EntityFrameworkCore;
using TecnmInventario.Api.Common.Errors;
using TecnmInventario.Api.Core.Entities;
using TecnmInventario.Api.Data;

namespace TecnmInventario.Api.Features.Maintenance.Shared;

/// <summary>
/// Lecturas y guardas comunes de los handlers que cambian el estado de un
/// mantenimiento. Se usan SIEMPRE después de tomar AssetRowLock.
/// </summary>
internal static class MaintenanceGuards
{
    /// <summary>
    /// Id del activo del registro, SIN bloquear nada (el lock del activo se toma
    /// después). AsNoTracking: la lectura posterior al lock es la que cuenta.
    /// IgnoreQueryFilters + !IsDeleted explícito: MaintenanceRecord.Asset es una
    /// FK requerida hacia un principal con filtro (regla 20).
    /// </summary>
    public static async Task<long> ReadAssetIdAsync(
        TecnmInventarioDbContext context, long maintenanceRecordId, CancellationToken cancellationToken)
        => await context.MaintenanceRecords
               .IgnoreQueryFilters(["SoftDelete"])
               .AsNoTracking()
               .Where(m => m.MaintenanceRecordId == maintenanceRecordId && !m.IsDeleted)
               .Select(m => (long?)m.AssetId)
               .FirstOrDefaultAsync(cancellationToken)
           ?? throw new KeyNotFoundException("El mantenimiento solicitado no existe.");

    /// <summary>Lectura fresca (post-lock) del activo, con las bajas lógicas visibles.</summary>
    public static async Task<Asset> LoadAssetAsync(
        TecnmInventarioDbContext context, long assetId, CancellationToken cancellationToken)
        => await context.Assets
               .IgnoreQueryFilters(["SoftDelete"])
               .FirstOrDefaultAsync(a => a.AssetId == assetId, cancellationToken)
           ?? throw new KeyNotFoundException("El activo solicitado no existe.");

    /// <summary>Lectura fresca (post-lock) y rastreada del registro.</summary>
    public static async Task<MaintenanceRecord> LoadRecordAsync(
        TecnmInventarioDbContext context, long maintenanceRecordId, CancellationToken cancellationToken)
        => await context.MaintenanceRecords
               .IgnoreQueryFilters(["SoftDelete"])
               .Where(m => !m.IsDeleted)
               .FirstOrDefaultAsync(m => m.MaintenanceRecordId == maintenanceRecordId, cancellationToken)
           ?? throw new KeyNotFoundException("El mantenimiento solicitado no existe.");

    /// <summary>Operar sobre algo cuyo activo está borrado es 400 (regla 20).</summary>
    public static void EnsureAssetNotDeleted(Asset asset, string action)
    {
        if (asset.IsDeleted)
        {
            throw new BusinessRuleViolationException(
                $"No se puede {action} el mantenimiento: el activo #{asset.AssetId} está eliminado lógicamente.");
        }
    }

    /// <summary>
    /// Concurrencia optimista contra la versión FRESCA del registro, tras el lock
    /// y antes de mutar (el 409 va antes que las reglas de negocio de estado).
    /// </summary>
    public static void EnsureVersion(MaintenanceRecord record, int expectedVersion)
    {
        if (record.Version != expectedVersion)
        {
            throw new DbUpdateConcurrencyException(
                "El mantenimiento fue modificado por otro usuario mientras tanto.");
        }
    }
}
