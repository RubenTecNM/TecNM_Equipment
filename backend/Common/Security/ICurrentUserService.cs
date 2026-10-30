namespace TecnmInventario.Api.Common.Security;

public interface ICurrentUserService
{
    long? UserId { get; }
    string? Username { get; }
    string? RoleCode { get; }
    System.Net.IPAddress? IpAddress { get; }
}
