using Mediator;

namespace TecnmInventario.Api.Features.Locations.DeleteLocation;

public sealed record DeleteLocationCommand(long LocationId, int ExpectedVersion)
    : ICommand<DeleteLocationResponse>;
