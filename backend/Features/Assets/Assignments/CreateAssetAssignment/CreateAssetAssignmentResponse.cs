namespace TecnmInventario.Api.Features.Assets.Assignments.CreateAssetAssignment;

public sealed record CreateAssetAssignmentResponse(
    long AssetAssignmentId,
    long AssetId,
    long AssignedTo,
    DateTime AssignedAt,
    int AssetVersion);
