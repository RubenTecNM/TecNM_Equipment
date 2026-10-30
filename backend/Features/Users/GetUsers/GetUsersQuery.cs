using Mediator;

namespace TecnmInventario.Api.Features.Users.GetUsers;

public sealed record GetUsersQuery(string? Search)
    : IQuery<IReadOnlyList<GetUsersResponse>>;
