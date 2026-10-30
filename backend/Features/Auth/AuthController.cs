using Mediator;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TecnmInventario.Api.Features.Auth.ExternalLogin;
using TecnmInventario.Api.Features.Auth.GetMe;
using TecnmInventario.Api.Features.Auth.GetProviders;
using TecnmInventario.Api.Features.Auth.Login;
using TecnmInventario.Api.Features.Auth.Logout;
using TecnmInventario.Api.Features.Auth.Refresh;
using TecnmInventario.Api.Features.Auth.Register;
using TecnmInventario.Api.Features.Auth.Shared;

namespace TecnmInventario.Api.Features.Auth;

/// <summary>
/// La sesión viaja en cookies HttpOnly (tecnm_at / tecnm_rt) que escriben los
/// Handlers vía IAuthCookieWriter; ningún endpoint devuelve tokens en el cuerpo.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IMediator _mediator;

    public AuthController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<AuthUserResponse>> Login(
        LoginCommand command,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(command, cancellationToken);
        return Ok(result);
    }

    [HttpPost("refresh")]
    [AllowAnonymous]
    public async Task<ActionResult<AuthUserResponse>> Refresh(CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new RefreshSessionCommand(), cancellationToken);
        return Ok(result);
    }

    // Anónimo: el JWT de acceso puede haber expirado y aun así hay que
    // revocar el refresh y borrar las cookies.
    [HttpPost("logout")]
    [AllowAnonymous]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        await _mediator.Send(new LogoutCommand(), cancellationToken);
        return NoContent();
    }

    [HttpGet("me")]
    public async Task<ActionResult<AuthUserResponse>> Me(CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetMeQuery(), cancellationToken);
        return Ok(result);
    }

    [HttpGet("providers")]
    [AllowAnonymous]
    public async Task<ActionResult<GetProvidersResponse>> Providers(CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetProvidersQuery(), cancellationToken);
        return Ok(result);
    }

    [HttpGet("external/{provider}")]
    [AllowAnonymous]
    public async Task<IActionResult> ExternalLogin(
        string provider,
        [FromQuery] string? returnUrl,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new StartExternalLoginQuery(provider, returnUrl), cancellationToken);
        return Challenge(new AuthenticationProperties { RedirectUri = result.CallbackUri }, result.Scheme);
    }

    [HttpGet("external/callback")]
    [AllowAnonymous]
    public async Task<IActionResult> ExternalCallback(
        [FromQuery] string? returnUrl,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new ExternalLoginCallbackCommand(returnUrl), cancellationToken);
        return Redirect(result.RedirectUrl);
    }

    [HttpPost("register")]
    [Authorize(Roles = "ADMIN")]
    public async Task<ActionResult<RegisterResponse>> Register(
        RegisterCommand command,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(command, cancellationToken);
        return Ok(result);
    }
}
