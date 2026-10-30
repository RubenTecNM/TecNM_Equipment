using Mediator;

namespace TecnmInventario.Api.Features.Assets.Movements.CreateAssetMovement;

public sealed record CreateAssetMovementCommand(
    long AssetId,
    int ExpectedVersion,
    long ToLocationId,
    long MovementReasonId,
    DateTime? MovedAt,
    string? Notes)
    : ICommand<CreateAssetMovementResponse>;
