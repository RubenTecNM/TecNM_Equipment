namespace TecnmInventario.Api.Features.Assets.Assignments.GetAssetAssignments;

public sealed record GetAssetAssignmentsResponse(
    long AssetAssignmentId,
    long AssignedTo,
    string AssignedToUsername,
    long AssignmentTypeId,
    string AssignmentTypeCode,
    string AssignmentTypeName,
    long? LocationId,
    string? LocationName,
    DateTime AssignedAt,
    DateTime? ReturnedAt,
    bool IsActive,
    string? Notes);
