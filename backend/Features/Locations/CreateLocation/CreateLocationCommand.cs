using Mediator;

namespace TecnmInventario.Api.Features.Locations.CreateLocation;

public sealed record CreateLocationCommand(
    string Code,
    string Name,
    long LocationTypeId,
    long? ParentLocationId) : ICommand<CreateLocationResponse>, Common.Behaviors.ITransactionalCommand;
