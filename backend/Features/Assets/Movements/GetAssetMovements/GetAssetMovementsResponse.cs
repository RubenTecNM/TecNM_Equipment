using TecnmInventario.Api.Common.Pagination;

namespace TecnmInventario.Api.Features.Assets.Movements.GetAssetMovements;

public sealed record GetAssetMovementsResponse(
    long AssetMovementId,
    long? FromLocationId,
    string? FromLocationName,
    long ToLocationId,
    string ToLocationName,
    long MovementReasonId,
    string MovementReasonCode,
    string MovementReasonName,
    long MovedBy,
    string MovedByUsername,
    DateTime MovedAt,
    string? Notes);
