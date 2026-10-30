using Mediator;

namespace TecnmInventario.Api.Features.Auth.Logout;

/// <summary>Revoca el refresh token de la cookie y borra las cookies de sesión.</summary>
public sealed record LogoutCommand : ICommand;
