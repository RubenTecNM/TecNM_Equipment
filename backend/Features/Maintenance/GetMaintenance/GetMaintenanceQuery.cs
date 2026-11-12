using Mediator;
using TecnmInventario.Api.Common.Pagination;
using TecnmInventario.Api.Features.Maintenance.Shared;

namespace TecnmInventario.Api.Features.Maintenance.GetMaintenance;

/// <param name="Status">Lista de estados (p. ej. ?status=PROGRAMADO&amp;status=EN_CURSO).</param>
/// <param name="From">Inicio inclusivo sobre la fecha de referencia.</param>
/// <param name="To">Fin inclusivo sobre la fecha de referencia.</param>
/// <param name="OnlyOpen">PROGRAMADO + EN_CURSO.</param>
public sealed record GetMaintenanceQuery(
    int PageNumber,
    int PageSize,
    long? AssetId,
    string[]? Status,
    long? MaintenanceTypeId,
    long? PerformedBy,
    DateTime? From,
    DateTime? To,
    string? Search,
    bool? OnlyOpen) : IQuery<PagedResult<MaintenanceListItem>>;
