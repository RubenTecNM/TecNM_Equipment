using Mediator;
using TecnmInventario.Api.Features.Auth.Shared;

namespace TecnmInventario.Api.Features.Auth.GetMe;

/// <summary>Perfil y permisos efectivos del usuario autenticado.</summary>
public sealed record GetMeQuery : IQuery<AuthUserResponse>;
