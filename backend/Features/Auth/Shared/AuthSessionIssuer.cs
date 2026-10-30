using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TecnmInventario.Api.Common.Security;
using TecnmInventario.Api.Core.Entities;
using TecnmInventario.Api.Data;

namespace TecnmInventario.Api.Features.Auth.Shared;

public sealed class AuthSessionIssuer : IAuthSessionIssuer
{
    private readonly TecnmInventarioDbContext _context;
    private readonly IJwtTokenGenerator _tokenGenerator;
    private readonly IAuthCookieWriter _cookies;
    private readonly ICurrentUserService _currentUser;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly AuthOptions _options;

    public AuthSessionIssuer(
        TecnmInventarioDbContext context,
        IJwtTokenGenerator tokenGenerator,
        IAuthCookieWriter cookies,
        ICurrentUserService currentUser,
        IHttpContextAccessor httpContextAccessor,
        IOptions<AuthOptions> options)
    {
        _context = context;
        _tokenGenerator = tokenGenerator;
        _cookies = cookies;
        _currentUser = currentUser;
        _httpContextAccessor = httpContextAccessor;
        _options = options.Value;
    }

    /// <summary>SHA-256 en hex: lo único que se guarda del refresh token.</summary>
    public static string HashToken(string rawToken) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawToken))).ToLowerInvariant();

    public async Task<IssuedSession> IssueAsync(AppUser user, Guid? familyId, CancellationToken cancellationToken)
    {
        var rawRefresh = Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
        var refreshExpiresAt = DateTime.UtcNow.AddDays(_options.RefreshTokenDays);
        var userAgent = _httpContextAccessor.HttpContext?.Request.Headers.UserAgent.ToString();

        var refreshToken = new RefreshToken
        {
            UserId = user.UserId,
            TokenHash = HashToken(rawRefresh),
            FamilyId = familyId ?? Guid.NewGuid(),
            ExpiresAt = refreshExpiresAt,
            CreatedAt = DateTime.UtcNow,
            CreatedIp = _currentUser.IpAddress,
            UserAgent = string.IsNullOrEmpty(userAgent) ? null : userAgent[..Math.Min(userAgent.Length, 512)]
        };

        _context.RefreshTokens.Add(refreshToken);
        await _context.SaveChangesAsync(cancellationToken);

        var (accessToken, accessExpiresAt) = _tokenGenerator.GenerateToken(user);
        _cookies.Write(accessToken, accessExpiresAt, rawRefresh, refreshExpiresAt);

        return new IssuedSession(refreshToken, await BuildProfileAsync(user, cancellationToken));
    }

    public async Task<AuthUserResponse> BuildProfileAsync(AppUser user, CancellationToken cancellationToken)
    {
        var permissions = await _context.Roles
            .Where(r => r.RoleId == user.RoleId && !r.IsDeleted)
            .SelectMany(r => r.Permissions.Select(p => p.Code))
            .OrderBy(code => code)
            .ToListAsync(cancellationToken);

        return new AuthUserResponse(
            user.UserId,
            user.Username,
            user.Email,
            user.FirstName,
            user.LastName,
            user.Role.Code,
            user.Role.Name,
            permissions);
    }

    private static string Base64UrlEncode(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
