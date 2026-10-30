using TecnmInventario.Api.Common.Domain;

namespace TecnmInventario.Api.Features.Incidents.Shared;

/// <summary>
/// Matriz de transiciones manuales del incidente (endpoint de cambio de estado).
/// RESUELTO no es destino válido aquí: solo se alcanza con ResolveIncident,
/// porque exige registrar la solución. CERRADO es terminal.
/// </summary>
public static class IncidentStatusTransitions
{
    private static readonly IReadOnlyDictionary<string, IReadOnlySet<string>> Allowed =
        new Dictionary<string, IReadOnlySet<string>>
        {
            [IncidentStatusCodes.Abierto] = new HashSet<string>
            {
                IncidentStatusCodes.EnDiagnostico, IncidentStatusCodes.EnReparacion
            },
            [IncidentStatusCodes.EnDiagnostico] = new HashSet<string>
            {
                IncidentStatusCodes.EnReparacion
            },
            [IncidentStatusCodes.EnReparacion] = new HashSet<string>
            {
                IncidentStatusCodes.EnDiagnostico
            },
            [IncidentStatusCodes.Resuelto] = new HashSet<string>
            {
                IncidentStatusCodes.Cerrado
            },
            [IncidentStatusCodes.Cerrado] = new HashSet<string>(),
        };

    /// <summary>Estados que el endpoint de cambio de estado acepta como destino.</summary>
    public static readonly IReadOnlyCollection<string> ManualTargets = new[]
    {
        IncidentStatusCodes.EnDiagnostico,
        IncidentStatusCodes.EnReparacion,
        IncidentStatusCodes.Cerrado
    };

    public static bool IsAllowed(string currentStatusCode, string newStatusCode)
        => Allowed.TryGetValue(currentStatusCode, out var targets) && targets.Contains(newStatusCode);
}
