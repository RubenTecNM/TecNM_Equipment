using Mediator;

namespace TecnmInventario.Api.Features.Assets.UpdateAsset;

// InventoryCode NO es editable: es la identidad administrativa estable del
// activo (independiente de ubicación), no un campo de metadatos.
// CurrentLocationId tampoco: la ubicación solo cambia mediante asset_movements.
public sealed record UpdateAssetCommand(
    long AssetId,
    int ExpectedVersion,
    long? ManufacturerId,
    long? ModelId,
    string? SerialNumber,
    string? Hostname,
    string? Notes) : ICommand<UpdateAssetResponse>, Common.Behaviors.ITransactionalCommand;
