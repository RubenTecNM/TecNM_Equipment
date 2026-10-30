namespace TecnmInventario.Api.Features.Documents;

/// <summary>
/// Tipos de entidad dueña válidos para un documento (deben coincidir
/// exactamente con el CHECK de la columna documents.owner_type en la BD).
/// </summary>
public static class DocumentOwnerTypes
{
    public const string Incident = "INCIDENT";
    public const string Maintenance = "MAINTENANCE";
    public const string AssetDisposal = "ASSET_DISPOSAL";
    public const string Acquisition = "ACQUISITION";

    public static readonly IReadOnlyList<string> All = new[]
    {
        Incident, Maintenance, AssetDisposal, Acquisition
    };
}
