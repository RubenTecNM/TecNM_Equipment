namespace TecnmInventario.Api.Features.Assets.Movements.CreateAssetMovement;

public sealed record CreateAssetMovementResponse(
    long AssetMovementId,
    long AssetId,
    long? FromLocationId,
    long ToLocationId,
    DateTime MovedAt,
    int AssetVersion);
