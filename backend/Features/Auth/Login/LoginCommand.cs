using Mediator;
using TecnmInventario.Api.Features.Auth.Shared;

namespace TecnmInventario.Api.Features.Auth.Login;

public sealed record LoginCommand(string UsernameOrEmail, string Password)
    : ICommand<AuthUserResponse>;
