using Mediator;

namespace TecnmInventario.Api.Features.Assets.CreateAsset;

public sealed record CreateAssetCommand(
    string InventoryCode,
    long AssetTypeId,
    long? ManufacturerId,
    long? ModelId,
    long? CurrentLocationId,
    string? SerialNumber,
    string? Hostname,
    string? Notes) : ICommand<CreateAssetResponse>, Common.Behaviors.ITransactionalCommand;
