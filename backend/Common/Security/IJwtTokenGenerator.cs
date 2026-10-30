using TecnmInventario.Api.Core.Entities;

namespace TecnmInventario.Api.Common.Security;

public interface IJwtTokenGenerator
{
    (string Token, DateTime ExpiresAt) GenerateToken(AppUser user);
}
