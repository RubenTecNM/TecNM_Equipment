using Mediator;

namespace TecnmInventario.Api.Features.Assets.Assignments.CreateAssetAssignment;

public sealed record CreateAssetAssignmentCommand(
    long AssetId,
    int ExpectedVersion,
    long AssignedTo,
    long AssignmentTypeId,
    long? LocationId,
    DateTime? AssignedAt,
    string? Notes)
    : ICommand<CreateAssetAssignmentResponse>;
