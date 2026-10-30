using Mediator;
using TecnmInventario.Api.Common.Pagination;

namespace TecnmInventario.Api.Features.Incidents.GetIncidents;

public sealed record GetIncidentsQuery(
    int PageNumber,
    int PageSize,
    long? AssetId,
    string? StatusCode,
    string? PriorityCode,
    long? IncidentTypeId,
    long? AssignedTo,
    bool? OnlyOpen,
    string? Search) : IQuery<PagedResult<GetIncidentsResponse>>;
