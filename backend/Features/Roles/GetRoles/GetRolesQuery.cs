using Mediator;

namespace TecnmInventario.Api.Features.Roles.GetRoles;

public sealed record GetRolesQuery : IQuery<IReadOnlyList<GetRolesResponse>>;
