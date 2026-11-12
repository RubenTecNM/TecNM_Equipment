using System.Linq.Expressions;
using TecnmInventario.Api.Core.Entities;

namespace TecnmInventario.Api.Features.Maintenance.Shared;

/// <summary>Referencia mínima a un usuario: solo id y nombre (sin correo ni usuario).</summary>
public sealed record MaintenanceUserRef(long Id, string FullName);

/// <summary>Ítem del listado (Q1) y del calendario (Q4). Nunca una entidad.</summary>
/// <param name="AssetHostname">Campo descriptivo real del activo (assets.hostname); puede ser null.</param>
public sealed record MaintenanceListItem(
    long MaintenanceRecordId,
    string Status,
    long AssetId,
    string AssetInventoryCode,
    string? AssetHostname,
    bool AssetIsDeleted,
    string MaintenanceTypeCode,
    string MaintenanceTypeName,
    MaintenanceUserRef? PerformedBy,
    DateTime? ScheduledAt,
    DateTime? StartedAt,
    DateTime? PerformedAt,
    DateTime? NextMaintenanceAt,
    long? IncidentId,
    int Version);

public static class MaintenanceProjections
{
    /// <summary>
    /// Proyección en SQL (nada de cargar entidades). La consulta que la use debe
    /// ignorar el filtro "SoftDelete" y filtrar !IsDeleted de la raíz (regla 20),
    /// porque Asset es una FK requerida hacia un principal con filtro.
    /// </summary>
    public static readonly Expression<Func<MaintenanceRecord, MaintenanceListItem>> ListItem =
        m => new MaintenanceListItem(
            m.MaintenanceRecordId,
            m.Status,
            m.AssetId,
            m.Asset.InventoryCode,
            m.Asset.Hostname,
            m.Asset.IsDeleted,
            m.MaintenanceType.Code,
            m.MaintenanceType.Name,
            m.PerformedByNavigation != null
                ? new MaintenanceUserRef(
                    m.PerformedByNavigation.UserId,
                    m.PerformedByNavigation.FirstName + " " + m.PerformedByNavigation.LastName)
                : null,
            m.ScheduledAt,
            m.StartedAt,
            m.PerformedAt,
            m.NextMaintenanceAt,
            m.IncidentId,
            m.Version);
}
