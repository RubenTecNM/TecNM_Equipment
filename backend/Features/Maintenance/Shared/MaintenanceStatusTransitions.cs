namespace TecnmInventario.Api.Features.Maintenance.Shared;

/// <summary>
/// Matriz de transiciones del estado de un registro de mantenimiento:
/// PROGRAMADO -> {EN_CURSO, CANCELADO}; EN_CURSO -> {COMPLETADO, CANCELADO};
/// COMPLETADO y CANCELADO son terminales. Es independiente de la máquina de
/// estados del ACTIVO (AssetStatusTransitions), que se cambia aparte con
/// IAssetStatusChanger.
/// </summary>
public static class MaintenanceStatusTransitions
{
    private static readonly IReadOnlyDictionary<string, IReadOnlySet<string>> Allowed =
        new Dictionary<string, IReadOnlySet<string>>
        {
            [MaintenanceStatusCodes.Programado] = new HashSet<string>
            {
                MaintenanceStatusCodes.EnCurso, MaintenanceStatusCodes.Cancelado
            },
            [MaintenanceStatusCodes.EnCurso] = new HashSet<string>
            {
                MaintenanceStatusCodes.Completado, MaintenanceStatusCodes.Cancelado
            },
            [MaintenanceStatusCodes.Completado] = new HashSet<string>(),
            [MaintenanceStatusCodes.Cancelado] = new HashSet<string>(),
        };

    public static bool IsAllowed(string currentStatusCode, string newStatusCode)
        => Allowed.TryGetValue(currentStatusCode, out var targets) && targets.Contains(newStatusCode);
}
