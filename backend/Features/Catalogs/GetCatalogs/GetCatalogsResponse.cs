namespace TecnmInventario.Api.Features.Catalogs.GetCatalogs;

public sealed record CatalogItem(long Id, string Code, string Name);

public sealed record GetCatalogsResponse(
    IReadOnlyList<CatalogItem> AssetTypes,
    IReadOnlyList<CatalogItem> AssetStatuses,
    IReadOnlyList<CatalogItem> LocationTypes,
    IReadOnlyList<CatalogItem> IncidentTypes,
    IReadOnlyList<CatalogItem> IncidentPriorities,
    IReadOnlyList<CatalogItem> IncidentStatuses,
    IReadOnlyList<CatalogItem> MaintenanceTypes,
    IReadOnlyList<CatalogItem> MovementReasons,
    IReadOnlyList<CatalogItem> AssignmentTypes,
    IReadOnlyList<CatalogItem> ComponentTypes,
    IReadOnlyList<CatalogItem> DisposalReasons,
    IReadOnlyList<CatalogItem> SoftwareTypes,
    IReadOnlyList<CatalogItem> LicenseTypes);
