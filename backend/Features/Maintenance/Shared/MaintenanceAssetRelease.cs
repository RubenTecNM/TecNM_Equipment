using Microsoft.EntityFrameworkCore;
using TecnmInventario.Api.Common.Domain;
using TecnmInventario.Api.Core.Entities;
using TecnmInventario.Api.Data;
using TecnmInventario.Api.Features.Assets.Shared;

namespace TecnmInventario.Api.Features.Maintenance.Shared;

public sealed record MaintenanceReleaseResult(bool Released, string AssetStatusCode, int OpenIncidents);

/// <summary>
/// Qué estado le toca al activo cuando un mantenimiento EN_CURSO termina (se cierra
/// o se cancela). Con el activo YA bloqueado (AssetRowLock) y dentro de la
/// transacción del llamador:
///   - algún incidente EN_REPARACION  -> EN_MANTENIMIENTO (el activo sigue en reparación);
///   - otros incidentes abiertos      -> CON_FALLA;
///   - ninguno                        -> ASIGNADO si hay asignación vigente, si no DISPONIBLE.
/// La asignación vigente se evalúa AQUÍ, al terminar (no al iniciar): puede haberse
/// devuelto durante el mantenimiento. No modifica ResolveIncident: la consulta es
/// la misma de ResolveIncidentHandler (asignación sin returned_at).
/// </summary>
public static class MaintenanceAssetRelease
{
    /// <param name="record">
    /// Registro rastreado con sus cambios ya asignados (estado, fechas...). Aquí se
    /// completa status_after_id y todo se vuelca con el cambio del activo (un solo
    /// UPDATE del registro, una sola versión).
    /// </param>
    public static async Task<MaintenanceReleaseResult> ReleaseAsync(
        TecnmInventarioDbContext context,
        IAssetStatusChanger assetStatusChanger,
        Asset asset,
        string lockedAssetStatusCode,
        MaintenanceRecord record,
        string reason,
        CancellationToken cancellationToken)
    {
        var openCodes = IncidentStatusCodes.Open.ToArray();
        var openStatusCodes = await context.Incidents
            .Where(i => i.AssetId == asset.AssetId && openCodes.Contains(i.IncidentStatus.Code))
            .Select(i => i.IncidentStatus.Code)
            .ToListAsync(cancellationToken);
        var openIncidents = openStatusCodes.Count;

        // Un activo dado de baja es terminal: no se toca (la baja se bloquea mientras
        // haya un mantenimiento en curso, pero esta guarda no depende de ello).
        if (lockedAssetStatusCode == AssetStatusCodes.DadoDeBaja)
        {
            record.StatusAfterId = asset.AssetStatusId;
            await context.SaveChangesAsync(cancellationToken);
            await context.Entry(record).ReloadAsync(cancellationToken);
            return new MaintenanceReleaseResult(false, lockedAssetStatusCode, openIncidents);
        }

        string target;
        if (openStatusCodes.Contains(IncidentStatusCodes.EnReparacion))
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
                .AnyAsync(a => a.AssetId == asset.AssetId && a.ReturnedAt == null, cancellationToken);
            target = hasActiveAssignment ? AssetStatusCodes.Asignado : AssetStatusCodes.Disponible;
        }

        record.StatusAfterId = await context.AssetStatuses
            .Where(s => s.Code == target && !s.IsDeleted)
            .Select(s => s.AssetStatusId)
            .FirstAsync(cancellationToken);

        if (lockedAssetStatusCode == target)
        {
            await context.SaveChangesAsync(cancellationToken); // solo el registro
        }
        else
        {
            // La matriz exige pasar por EN_MANTENIMIENTO antes de volver a circular
            // desde CON_FALLA (mismo criterio que ResolveIncidentHandler).
            if (lockedAssetStatusCode == AssetStatusCodes.ConFalla
                && target is AssetStatusCodes.Disponible or AssetStatusCodes.Asignado)
            {
                await assetStatusChanger.ChangeAsync(
                    asset, AssetStatusCodes.EnMantenimiento, reason + " (paso intermedio)", cancellationToken);
            }

            // SaveChanges del cambiador vuelca también el registro pendiente.
            await assetStatusChanger.ChangeAsync(asset, target, reason, cancellationToken);
        }

        await context.Entry(record).ReloadAsync(cancellationToken);

        var released = target is AssetStatusCodes.Disponible or AssetStatusCodes.Asignado;
        return new MaintenanceReleaseResult(released, target, openIncidents);
    }
}
