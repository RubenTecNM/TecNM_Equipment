using Mediator;

namespace TecnmInventario.Api.Features.Maintenance.GetMaintenanceTechnicians;

public sealed record GetMaintenanceTechniciansQuery : IQuery<IReadOnlyList<MaintenanceTechnicianResponse>>;
