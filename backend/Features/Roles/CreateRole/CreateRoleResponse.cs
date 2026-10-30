namespace TecnmInventario.Api.Features.Roles.CreateRole;

public sealed record CreateRoleResponse(
    long RoleId,
    string Code,
    string Name,
    string? Description);
