namespace TecnmInventario.Api.Features.Auth.Register;

public sealed record RegisterResponse(
    long UserId,
    string Username,
    string Email,
    string RoleCode);
