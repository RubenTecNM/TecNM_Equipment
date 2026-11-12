using Mediator;

namespace TecnmInventario.Api.Features.Maintenance.GetMaintenanceById;

public sealed record GetMaintenanceByIdQuery(long MaintenanceRecordId) : IQuery<GetMaintenanceByIdResponse>;
