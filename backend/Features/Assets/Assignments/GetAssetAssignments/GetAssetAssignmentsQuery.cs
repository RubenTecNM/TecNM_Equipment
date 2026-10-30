using Mediator;

namespace TecnmInventario.Api.Features.Assets.Assignments.GetAssetAssignments;

public sealed record GetAssetAssignmentsQuery(long AssetId)
    : IQuery<IReadOnlyList<GetAssetAssignmentsResponse>>;
