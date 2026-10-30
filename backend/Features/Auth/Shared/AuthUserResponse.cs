namespace TecnmInventario.Api.Features.Auth.Shared;

/// <summary>
/// Perfil de la sesión: lo devuelven login, refresh y me. El token ya no
/// viaja en el cuerpo (va en cookie HttpOnly); los permisos efectivos del
/// rol se resuelven en servidor para que la UI filtre por permiso.
/// </summary>
public sealed record AuthUserResponse(
    long UserId,
    string Username,
    string Email,
    string FirstName,
    string LastName,
    string RoleCode,
    string RoleName,
    IReadOnlyList<string> Permissions);
