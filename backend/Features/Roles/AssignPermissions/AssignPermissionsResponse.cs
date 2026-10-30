namespace TecnmInventario.Api.Features.Roles.AssignPermissions;

public sealed record AssignPermissionsResponse(
    long RoleId,
    string Code,
    IReadOnlyList<string> Permissions,
    int Version);
