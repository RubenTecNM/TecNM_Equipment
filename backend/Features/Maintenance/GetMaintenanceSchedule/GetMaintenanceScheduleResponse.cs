using TecnmInventario.Api.Features.Maintenance.Shared;

namespace TecnmInventario.Api.Features.Maintenance.GetMaintenanceSchedule;

public sealed record MaintenanceScheduleCounts(int Overdue, int Upcoming, int DueWithoutSchedule);

/// <summary>Calendario de mantenimiento. Todas las fechas en UTC.</summary>
public sealed record GetMaintenanceScheduleResponse(
    DateTime GeneratedAt,
    int WindowDays,
    IReadOnlyList<MaintenanceListItem> Overdue,
    IReadOnlyList<MaintenanceListItem> Upcoming,
    IReadOnlyList<MaintenanceListItem> DueWithoutSchedule,
    MaintenanceScheduleCounts Counts);
