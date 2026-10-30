using Mediator;

namespace TecnmInventario.Api.Features.Locations.UpdateLocation;

// Code NO es editable: es el identificador estable de la ubicación, igual
// criterio que InventoryCode en Assets.
public sealed record UpdateLocationCommand(
    long LocationId,
    int ExpectedVersion,
    string Name,
    long LocationTypeId,
    long? ParentLocationId) : ICommand<UpdateLocationResponse>, Common.Behaviors.ITransactionalCommand;
