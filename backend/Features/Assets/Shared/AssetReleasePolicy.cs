using Microsoft.EntityFrameworkCore;
using TecnmInventario.Api.Common.Domain;
using TecnmInventario.Api.Data;
using TecnmInventario.Api.Features.Maintenance.Shared;

namespace TecnmInventario.Api.Features.Assets.Shared;

/// <param name="TargetStatusCode">Estado que le toca al activo.</param>
/// <param name="Released">true solo si el destino es DISPONIBLE o ASIGNADO (vuelve a circular).</param>
/// <param name="OpenIncidents">Incidentes abiertos del activo (sin contar el excluido).</param>
/// <param name="MaintenanceInProgress">Hay un mantenimiento EN_CURSO (sin contar el excluido).</param>
public sealed record AssetReleaseDecision(
    string TargetStatusCode,
    bool Released,
    int OpenIncidents,
    bool MaintenanceInProgress);

/// <summary>
/// Política única de "qué estado le toca a un activo cuando termina algo que lo
/// tenía fuera de circulación" (resolver un incidente, cerrar o cancelar un
/// mantenimiento). Se llama con el activo YA bloqueado (AssetRowLock, regla 19)
/// y dentro de la transacción del llamador. Solo decide: no muta nada.
///   1) mantenimiento EN_CURSO del activo   -> EN_MANTENIMIENTO (no libera);
///   2) algún incidente EN_REPARACION       -> EN_MANTENIMIENTO;
///   3) otros incidentes abiertos           -> CON_FALLA;
///   4) ninguno                             -> ASIGNADO si hay asignación vigente, si no DISPONIBLE.
/// </summary>
public static class AssetReleasePolicy
{
    /// <param name="excludeIncidentId">Incidente que se está cerrando en este momento.</param>
    /// <param name="excludeMaintenanceRecordId">Registro de mantenimiento que se está cerrando.</param>
    public static async Task<AssetReleaseDecision> DecideAsync(
        TecnmInventarioDbContext context,
        long assetId,
        long? excludeIncidentId,
        long? excludeMaintenanceRecordId,
        CancellationToken cancellationToken)
    {
        var openCodes = IncidentStatusCodes.Open.ToArray();
        var excludedIncident = excludeIncidentId ?? 0;
        var openStatusCodes = await context.Incidents
            .Where(i => i.AssetId == assetId
                        && i.IncidentId != excludedIncident
                        && openCodes.Contains(i.IncidentStatus.Code))
            .Select(i => i.IncidentStatus.Code)
            .ToListAsync(cancellationToken);
        var openIncidents = openStatusCodes.Count;

        var excludedRecord = excludeMaintenanceRecordId ?? 0;
        var maintenanceInProgress = await context.MaintenanceRecords.AnyAsync(
            m => m.AssetId == assetId
                 && m.Status == MaintenanceStatusCodes.EnCurso
                 && !m.IsDeleted
                 && m.MaintenanceRecordId != excludedRecord,
            cancellationToken);

        string target;
        if (maintenanceInProgress || openStatusCodes.Contains(IncidentStatusCodes.EnReparacion))
        {
            target = AssetStatusCodes.EnMantenimiento;
        }
        else if (openIncidents > 0)
        {
            target = AssetStatusCodes.ConFalla;
        }
        else
        {
            var hasActiveAssignment = await context.AssetAssignments
                .AnyAsync(a => a.AssetId == assetId && a.ReturnedAt == null, cancellationToken);
            target = hasActiveAssignment ? AssetStatusCodes.Asignado : AssetStatusCodes.Disponible;
        }

        var released = target is AssetStatusCodes.Disponible or AssetStatusCodes.Asignado;
        return new AssetReleaseDecision(target, released, openIncidents, maintenanceInProgress);
    }
}
