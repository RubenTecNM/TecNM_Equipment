using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace TecnmInventario.Api.Common.Security;

public class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUserService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public long? UserId
    {
        get
        {
            var value = _httpContextAccessor.HttpContext?.User
                .FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
            return long.TryParse(value, out var id) ? id : null;
        }
    }

    public string? Username =>
        _httpContextAccessor.HttpContext?.User
            .FindFirst(JwtRegisteredClaimNames.UniqueName)?.Value;

    public string? RoleCode =>
        _httpContextAccessor.HttpContext?.User
            .FindFirst(ClaimTypes.Role)?.Value;

    public System.Net.IPAddress? IpAddress =>
        _httpContextAccessor.HttpContext?.Connection.RemoteIpAddress;
}
