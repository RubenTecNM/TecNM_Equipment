using Mediator;

namespace TecnmInventario.Api.Features.Assets.Assignments.EndAssetAssignment;

public sealed record EndAssetAssignmentCommand(
    long AssetId,
    int ExpectedVersion,
    long AssetAssignmentId)
    : ICommand<EndAssetAssignmentResponse>;
