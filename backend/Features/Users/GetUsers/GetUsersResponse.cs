namespace TecnmInventario.Api.Features.Users.GetUsers;

public sealed record GetUsersResponse(
    long UserId,
    string Username,
    string FirstName,
    string LastName,
    string RoleCode,
    string Status);
