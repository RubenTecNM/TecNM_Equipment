namespace TecnmInventario.Api.Features.Roles.AssignPermissions;

public sealed record AssignPermissionsRequest(
    int ExpectedVersion,
    IReadOnlyList<string> PermissionCodes);
