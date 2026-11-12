using Mediator;
using Microsoft.EntityFrameworkCore;
using TecnmInventario.Api.Common.Domain;
using TecnmInventario.Api.Data;
using TecnmInventario.Api.Features.Maintenance.Shared;

namespace TecnmInventario.Api.Features.Maintenance.GetMaintenanceSchedule;

public sealed class GetMaintenanceScheduleHandler
    : IQueryHandler<GetMaintenanceScheduleQuery, GetMaintenanceScheduleResponse>
{
    public const string WindowDaysSetting = "Maintenance:ScheduleWindowDays";
    public const int DefaultWindowDays = 30;

    private readonly TecnmInventarioDbContext _context;
    private readonly TimeProvider _timeProvider;
    private readonly IConfiguration _configuration;

    public GetMaintenanceScheduleHandler(
        TecnmInventarioDbContext context, TimeProvider timeProvider, IConfiguration configuration)
    {
        _context = context;
        _timeProvider = timeProvider;
        _configuration = configuration;
    }

    public async ValueTask<GetMaintenanceScheduleResponse> Handle(
        GetMaintenanceScheduleQuery query, CancellationToken cancellationToken)
    {
        var windowDays = query.Days ?? ConfiguredWindowDays();
        var now = _timeProvider.GetUtcNow().UtcDateTime; // todo en UTC
        var limit = now.AddDays(windowDays);

        // Regla 20: el calendario es operativo, no historial. Se excluyen registros
        // borrados, activos borrados y activos dados de baja. IgnoreQueryFilters
        // quita también el filtro de Asset, así que !Asset.IsDeleted es explícito.
        var live = _context.MaintenanceRecords
            .IgnoreQueryFilters(["SoftDelete"])
            .Where(m => !m.IsDeleted
                        && !m.Asset.IsDeleted
                        && m.Asset.AssetStatus.Code != AssetStatusCodes.DadoDeBaja);

        var overdue = await live
            .Where(m => m.Status == MaintenanceStatusCodes.Programado && m.ScheduledAt < now)
            .OrderBy(m => m.ScheduledAt).ThenBy(m => m.MaintenanceRecordId)
            .Select(MaintenanceProjections.ListItem)
            .ToListAsync(cancellationToken);

        var upcoming = await live
            .Where(m => m.Status == MaintenanceStatusCodes.Programado
                        && m.ScheduledAt >= now && m.ScheduledAt <= limit)
            .OrderBy(m => m.ScheduledAt).ThenBy(m => m.MaintenanceRecordId)
            .Select(MaintenanceProjections.ListItem)
            .ToListAsync(cancellationToken);

        // Registros no borrados de cualquier tipo que ya cubren al activo.
        var open = _context.MaintenanceRecords
            .IgnoreQueryFilters(["SoftDelete"])
            .Where(m => !m.IsDeleted
                        && (m.Status == MaintenanceStatusCodes.Programado || m.Status == MaintenanceStatusCodes.EnCurso));

        var preventive = _context.MaintenanceRecords
            .IgnoreQueryFilters(["SoftDelete"])
            .Where(m => !m.IsDeleted
                        && m.Status == MaintenanceStatusCodes.Completado
                        && m.MaintenanceType.Code == MaintenanceTypeCodes.Preventivo);

        // Una fila por activo: el ÚLTIMO preventivo completado (por performed_at, luego id).
        // Solo cuenta si su próxima fecha vence dentro de la ventana y el activo no
        // tiene ningún registro PROGRAMADO o EN_CURSO.
        var dueWithoutSchedule = await live
            .Where(m => m.Status == MaintenanceStatusCodes.Completado
                        && m.MaintenanceType.Code == MaintenanceTypeCodes.Preventivo
                        && m.NextMaintenanceAt != null
                        && m.NextMaintenanceAt <= limit
                        && !preventive.Any(o => o.AssetId == m.AssetId
                                                && (o.PerformedAt > m.PerformedAt
                                                    || (o.PerformedAt == m.PerformedAt
                                                        && o.MaintenanceRecordId > m.MaintenanceRecordId)))
                        && !open.Any(o => o.AssetId == m.AssetId))
            .OrderBy(m => m.NextMaintenanceAt).ThenBy(m => m.MaintenanceRecordId)
            .Select(MaintenanceProjections.ListItem)
            .ToListAsync(cancellationToken);

        return new GetMaintenanceScheduleResponse(
            now,
            windowDays,
            overdue,
            upcoming,
            dueWithoutSchedule,
            new MaintenanceScheduleCounts(overdue.Count, upcoming.Count, dueWithoutSchedule.Count));
    }

    private int ConfiguredWindowDays()
    {
        var configured = _configuration.GetValue<int?>(WindowDaysSetting);
        return configured is >= GetMaintenanceScheduleValidator.MinDays and <= GetMaintenanceScheduleValidator.MaxDays
            ? configured.Value
            : DefaultWindowDays;
    }
}
