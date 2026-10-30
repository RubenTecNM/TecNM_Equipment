namespace TecnmInventario.Api.Features.Assets.Assignments.EndAssetAssignment;

public sealed record EndAssetAssignmentResponse(
    long AssetAssignmentId,
    DateTime ReturnedAt,
    int AssetVersion);
