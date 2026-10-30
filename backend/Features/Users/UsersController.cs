using Mediator;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TecnmInventario.Api.Features.Users.GetUsers;

namespace TecnmInventario.Api.Features.Users;

[ApiController]
[Route("api/[controller]")]
public class UsersController : ControllerBase
{
    private readonly IMediator _mediator;

    public UsersController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    [Authorize(Policy = "users.view")]
    public async Task<ActionResult<IReadOnlyList<GetUsersResponse>>> GetUsers(
        [FromQuery] string? search,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetUsersQuery(search), cancellationToken);
        return Ok(result);
    }
}
