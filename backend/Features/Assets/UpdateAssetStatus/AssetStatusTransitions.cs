using TecnmInventario.Api.Common.Domain;

namespace TecnmInventario.Api.Features.Assets.UpdateAssetStatus;

/// <summary>
/// Matriz de transiciones válidas de la máquina de estados de activos.
/// DADO_DE_BAJA es terminal (ninguna salida). CON_FALLA solo puede volver
/// a circular pasando por EN_MANTENIMIENTO primero (alguien debe atender
/// la falla antes de que el equipo vuelva a estar disponible o asignado).
/// </summary>
public static class AssetStatusTransitions
{
    private static readonly IReadOnlyDictionary<string, IReadOnlySet<string>> Allowed =
        new Dictionary<string, IReadOnlySet<string>>
        {
            [AssetStatusCodes.Disponible] = new HashSet<string>
            {
                AssetStatusCodes.Asignado, AssetStatusCodes.EnMantenimiento,
                AssetStatusCodes.ConFalla, AssetStatusCodes.DadoDeBaja
            },
            [AssetStatusCodes.Asignado] = new HashSet<string>
            {
                AssetStatusCodes.Disponible, AssetStatusCodes.EnMantenimiento,
                AssetStatusCodes.ConFalla, AssetStatusCodes.DadoDeBaja
            },
            [AssetStatusCodes.EnMantenimiento] = new HashSet<string>
            {
                AssetStatusCodes.Disponible, AssetStatusCodes.Asignado,
                AssetStatusCodes.ConFalla, AssetStatusCodes.DadoDeBaja
            },
            [AssetStatusCodes.ConFalla] = new HashSet<string>
            {
                AssetStatusCodes.EnMantenimiento, AssetStatusCodes.DadoDeBaja
            },
            [AssetStatusCodes.DadoDeBaja] = new HashSet<string>(),
        };

    public static bool IsAllowed(string currentStatusCode, string newStatusCode)
        => Allowed.TryGetValue(currentStatusCode, out var targets) && targets.Contains(newStatusCode);
}
