using TecnmInventario.Api.Features.Maintenance.Shared;

namespace TecnmInventario.Api.Tests;

/// <summary>
/// M.1: matriz de estados del registro. PROGRAMADO -> {EN_CURSO, CANCELADO};
/// EN_CURSO -> {COMPLETADO, CANCELADO}; COMPLETADO y CANCELADO son terminales.
/// </summary>
public class MaintenanceStatusTransitionsTests
{
    private static readonly HashSet<(string, string)> Allowed =
    [
        (MaintenanceStatusCodes.Programado, MaintenanceStatusCodes.EnCurso),
        (MaintenanceStatusCodes.Programado, MaintenanceStatusCodes.Cancelado),
        (MaintenanceStatusCodes.EnCurso, MaintenanceStatusCodes.Completado),
        (MaintenanceStatusCodes.EnCurso, MaintenanceStatusCodes.Cancelado),
    ];

    // Las 16 combinaciones (origen, destino) con su resultado esperado.
    public static IEnumerable<object[]> AllCombinations()
    {
        foreach (var from in MaintenanceStatusCodes.All)
        {
            foreach (var to in MaintenanceStatusCodes.All)
            {
                yield return [from, to, Allowed.Contains((from, to))];
            }
        }
    }

    [Theory]
    [MemberData(nameof(AllCombinations))]
    public void IsAllowed_CubreLasDieciseisCombinaciones(string from, string to, bool expected)
        => Assert.Equal(expected, MaintenanceStatusTransitions.IsAllowed(from, to));

    [Theory]
    [InlineData("ABIERTO", MaintenanceStatusCodes.EnCurso)]
    [InlineData(MaintenanceStatusCodes.Programado, "ABIERTO")]
    [InlineData("", "")]
    [InlineData("programado", "en_curso")] // los códigos distinguen mayúsculas
    public void IsAllowed_RechazaCodigosDesconocidos(string from, string to)
        => Assert.False(MaintenanceStatusTransitions.IsAllowed(from, to));

    [Fact]
    public void Los_codigos_son_exactamente_los_del_CHECK_de_la_BD()
        => Assert.Equal(
            ["PROGRAMADO", "EN_CURSO", "COMPLETADO", "CANCELADO"],
            MaintenanceStatusCodes.All);
}
