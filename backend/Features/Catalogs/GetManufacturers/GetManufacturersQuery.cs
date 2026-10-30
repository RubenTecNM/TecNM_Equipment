using Mediator;

namespace TecnmInventario.Api.Features.Catalogs.GetManufacturers;

public sealed record GetManufacturersQuery() : IQuery<IReadOnlyList<GetManufacturersResponse>>;
