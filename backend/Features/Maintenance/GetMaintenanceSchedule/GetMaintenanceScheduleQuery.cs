using Mediator;

namespace TecnmInventario.Api.Features.Maintenance.GetMaintenanceSchedule;

/// <param name="Days">Ventana en días (1-365). Null = Maintenance:ScheduleWindowDays.</param>
public sealed record GetMaintenanceScheduleQuery(int? Days) : IQuery<GetMaintenanceScheduleResponse>;
