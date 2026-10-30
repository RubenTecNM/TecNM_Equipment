namespace TecnmInventario.Api.Features.Roles.GetRoles;

public sealed record GetRolesResponse(
    long RoleId,
    string Code,
    string Name,
    string? Description,
    IReadOnlyList<string> Permissions);
