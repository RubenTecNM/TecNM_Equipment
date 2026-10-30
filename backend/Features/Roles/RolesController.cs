using Mediator;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TecnmInventario.Api.Features.Roles.AssignPermissions;
using TecnmInventario.Api.Features.Roles.CreateRole;
using TecnmInventario.Api.Features.Roles.GetRoles;

namespace TecnmInventario.Api.Features.Roles;

[ApiController]
[Route("api/[controller]")]
public class RolesController : ControllerBase
{
    private readonly IMediator _mediator;

    public RolesController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    [Authorize(Policy = "roles.manage")]
    public async Task<ActionResult<IReadOnlyList<GetRolesResponse>>> GetRoles(
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetRolesQuery(), cancellationToken);
        return Ok(result);
    }

    [HttpPost]
    [Authorize(Policy = "roles.manage")]
    public async Task<ActionResult<CreateRoleResponse>> CreateRole(
        CreateRoleCommand command,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(command, cancellationToken);
        return CreatedAtAction(nameof(GetRoles), new { }, result);
    }

    [HttpPut("{roleId}/permissions")]
    [Authorize(Policy = "roles.manage")]
    public async Task<ActionResult<AssignPermissionsResponse>> AssignPermissions(
        long roleId,
        AssignPermissionsRequest request,
        CancellationToken cancellationToken)
    {
        var command = new AssignPermissionsCommand(
            roleId, request.ExpectedVersion, request.PermissionCodes);
        var result = await _mediator.Send(command, cancellationToken);
        return Ok(result);
    }
}
