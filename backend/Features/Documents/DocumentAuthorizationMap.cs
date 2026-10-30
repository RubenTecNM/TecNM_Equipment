namespace TecnmInventario.Api.Features.Documents;

/// <summary>
/// Mapa OwnerType -> permisos aceptables para subir/ver/eliminar un documento
/// de ese tipo. Basta con tener AL MENOS UNO de los permisos listados.
/// Centralizado aquí para que agregar un módulo nuevo (ej. Procurement) sea
/// un cambio en un solo lugar, no en cada Handler de Documents.
/// </summary>
public static class DocumentAuthorizationMap
{
    private static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> RequiredPermissions =
        new Dictionary<string, IReadOnlyList<string>>
        {
            [DocumentOwnerTypes.Incident] = new[] { "incidents.create", "incidents.resolve" },
            [DocumentOwnerTypes.Maintenance] = new[] { "maintenance.manage" },
            [DocumentOwnerTypes.AssetDisposal] = new[] { "assets.delete" },
            [DocumentOwnerTypes.Acquisition] = new[] { "procurement.manage" },
        };

    public static IReadOnlyList<string> GetRequiredPermissions(string ownerType)
    {
        if (RequiredPermissions.TryGetValue(ownerType, out var permissions))
        {
            return permissions;
        }

        // OwnerType desconocido: no autoriza nada (fail-closed).
        return Array.Empty<string>();
    }
}
