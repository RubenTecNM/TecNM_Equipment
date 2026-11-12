namespace TecnmInventario.Api.Features.Maintenance.Shared;

/// <summary>
/// Códigos del estado de un registro de mantenimiento (columna
/// maintenance_records.status; ver scripts/add-maintenance-workflow.sql).
/// Sin textos mágicos en handlers (regla 5).
/// </summary>
public static class MaintenanceStatusCodes
{
    public const string Programado = "PROGRAMADO";
    public const string EnCurso = "EN_CURSO";
    public const string Completado = "COMPLETADO";
    public const string Cancelado = "CANCELADO";

    public static readonly IReadOnlyList<string> All =
        new[] { Programado, EnCurso, Completado, Cancelado };
}
