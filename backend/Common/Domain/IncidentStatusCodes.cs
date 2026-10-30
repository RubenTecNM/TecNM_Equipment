namespace TecnmInventario.Api.Common.Domain;

public static class IncidentStatusCodes
{
    public const string Abierto = "ABIERTO";
    public const string EnDiagnostico = "EN_DIAGNOSTICO";
    public const string EnReparacion = "EN_REPARACION";
    public const string Resuelto = "RESUELTO";
    public const string Cerrado = "CERRADO";

    /// <summary>
    /// Estados en los que el incidente sigue "vivo": el activo afectado
    /// no debe liberarse mientras exista alguno así.
    /// </summary>
    public static readonly IReadOnlyCollection<string> Open =
        new[] { Abierto, EnDiagnostico, EnReparacion };
}
