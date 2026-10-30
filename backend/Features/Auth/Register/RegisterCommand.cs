using Mediator;

namespace TecnmInventario.Api.Features.Auth.Register;

public sealed record RegisterCommand(
    string Username,
    string Email,
    string Password,
    string FirstName,
    string LastName,
    string RoleCode) : ICommand<RegisterResponse>, Common.Behaviors.ITransactionalCommand;
