using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;

namespace TecnmInventario.Api.Common.Authorization;

public sealed class PermissionAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
{
    private readonly IRolePermissionService _rolePermissionService;

    public PermissionAuthorizationHandler(IRolePermissionService rolePermissionService)
    {
        _rolePermissionService = rolePermissionService;
    }

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PermissionRequirement requirement)
    {
        var roleCode = context.User.FindFirst(ClaimTypes.Role)?.Value;
        if (string.IsNullOrEmpty(roleCode))
        {
            return;
        }

        var hasPermission = await _rolePermissionService.HasPermissionAsync(
            roleCode, requirement.PermissionCode, CancellationToken.None);

        if (hasPermission)
        {
            context.Succeed(requirement);
        }
    }
}
