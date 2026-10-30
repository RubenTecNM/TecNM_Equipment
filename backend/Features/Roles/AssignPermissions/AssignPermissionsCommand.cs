using Mediator;

namespace TecnmInventario.Api.Features.Roles.AssignPermissions;

public sealed record AssignPermissionsCommand(
    long RoleId,
    int ExpectedVersion,
    IReadOnlyList<string> PermissionCodes) : ICommand<AssignPermissionsResponse>, Common.Behaviors.ITransactionalCommand;
