using Mediator;
using Microsoft.EntityFrameworkCore;
using TecnmInventario.Api.Common.Pagination;
using TecnmInventario.Api.Core.Entities;
using TecnmInventario.Api.Data;
using TecnmInventario.Api.Features.Maintenance.Shared;

namespace TecnmInventario.Api.Features.Maintenance.GetMaintenance;

public sealed class GetMaintenanceHandler
    : IQueryHandler<GetMaintenanceQuery, PagedResult<MaintenanceListItem>>
{
    private readonly TecnmInventarioDbContext _context;

    public GetMaintenanceHandler(TecnmInventarioDbContext context)
    {
        _context = context;
    }

    public async ValueTask<PagedResult<MaintenanceListItem>> Handle(
        GetMaintenanceQuery query, CancellationToken cancellationToken)
    {
        if (query.AssetId.HasValue)
        {
            var assetId = query.AssetId.Value;
            // Igual que GetIncidents: IgnoreQueryFilters SIN Where(!IsDeleted) a
            // propósito. Un activo borrado lógicamente sigue existiendo y su
            // historial de mantenimiento debe verse; solo un id inexistente es 404.
            var assetExists = await _context.Assets
                .IgnoreQueryFilters(["SoftDelete"])
                .AnyAsync(a => a.AssetId == assetId, cancellationToken);
            if (!assetExists)
            {
                throw new KeyNotFoundException("El activo solicitado no existe.");
            }
        }

        // Items y TotalCount comparten esta misma base filtrada.
        var records = BuildFilteredQuery(_context, query);

        var totalCount = await records.CountAsync(cancellationToken);

        var items = await ApplyPaging(records, query)
            .Select(MaintenanceProjections.ListItem)
            .ToListAsync(cancellationToken);

        return new PagedResult<MaintenanceListItem>(items, query.PageNumber, query.PageSize, totalCount);
    }

    /// <summary>
    /// Base filtrada (sin orden ni paginación). Regla 20: el listado es historial y
    /// no debe perder registros porque su activo esté borrado (Asset es una FK
    /// requerida), así que se ignora el filtro "SoftDelete" y la raíz se filtra
    /// con !IsDeleted de forma explícita.
    /// </summary>
    public static IQueryable<MaintenanceRecord> BuildFilteredQuery(
        TecnmInventarioDbContext context, GetMaintenanceQuery query)
    {
        var records = context.MaintenanceRecords
            .IgnoreQueryFilters(["SoftDelete"])
            .Where(m => !m.IsDeleted);

        if (query.AssetId.HasValue)
        {
            var assetId = query.AssetId.Value;
            records = records.Where(m => m.AssetId == assetId);
        }

        if (query.Status is { Length: > 0 })
        {
            var statuses = query.Status.Select(s => s.Trim().ToUpperInvariant()).Distinct().ToArray();
            records = records.Where(m => statuses.Contains(m.Status));
        }

        if (query.OnlyOpen == true)
        {
            records = records.Where(m =>
                m.Status == MaintenanceStatusCodes.Programado || m.Status == MaintenanceStatusCodes.EnCurso);
        }

        if (query.MaintenanceTypeId.HasValue)
        {
            var typeId = query.MaintenanceTypeId.Value;
            records = records.Where(m => m.MaintenanceTypeId == typeId);
        }

        if (query.PerformedBy.HasValue)
        {
            var performedBy = query.PerformedBy.Value;
            records = records.Where(m => m.PerformedBy == performedBy);
        }

        // Fecha de referencia = COALESCE(performed_at, started_at, scheduled_at)
        // (con created_at como último recurso para no ordenar NULL al principio).
        if (query.From.HasValue)
        {
            var from = MaintenanceDates.ToUtc(query.From.Value);
            records = records.Where(m =>
                (m.PerformedAt ?? m.StartedAt ?? m.ScheduledAt ?? m.CreatedAt) >= from);
        }

        if (query.To.HasValue)
        {
            var to = MaintenanceDates.ToUtc(query.To.Value);
            records = records.Where(m =>
                (m.PerformedAt ?? m.StartedAt ?? m.ScheduledAt ?? m.CreatedAt) <= to);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var pattern = $"%{EscapeLike(query.Search.Trim())}%";
            records = records.Where(m =>
                EF.Functions.ILike(m.Asset.InventoryCode, pattern, "\\")
                || (m.Description != null && EF.Functions.ILike(m.Description, pattern, "\\")));
        }

        return records;
    }

    public static IQueryable<MaintenanceRecord> ApplyPaging(
        IQueryable<MaintenanceRecord> source, GetMaintenanceQuery query)
        => source
            .OrderByDescending(m => m.PerformedAt ?? m.StartedAt ?? m.ScheduledAt ?? m.CreatedAt)
            .ThenByDescending(m => m.MaintenanceRecordId)
            .Skip((query.PageNumber - 1) * query.PageSize)
            .Take(query.PageSize);

    /// <summary>Escapa \ % y _ para que el término se busque como texto literal.</summary>
    private static string EscapeLike(string term)
        => term.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");
}
