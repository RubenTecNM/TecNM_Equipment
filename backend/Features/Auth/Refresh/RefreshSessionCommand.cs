using Mediator;
using TecnmInventario.Api.Features.Auth.Shared;

namespace TecnmInventario.Api.Features.Auth.Refresh;

/// <summary>
/// Rota el refresh token de la cookie tecnm_rt y emite un JWT de acceso nuevo.
/// Sin parámetros: todo viaja en cookies.
/// </summary>
public sealed record RefreshSessionCommand : ICommand<AuthUserResponse>;
