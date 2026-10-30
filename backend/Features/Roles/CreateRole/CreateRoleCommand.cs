using Mediator;

namespace TecnmInventario.Api.Features.Roles.CreateRole;

public sealed record CreateRoleCommand(
    string Code,
    string Name,
    string? Description) : ICommand<CreateRoleResponse>, Common.Behaviors.ITransactionalCommand;
