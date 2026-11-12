namespace TecnmInventario.Api.Core.Entities;

// Columnas añadidas por scripts/add-maintenance-workflow.sql. Viven aquí (partial)
// y se mapean en TecnmInventarioDbContext.Custom.cs para no editar el archivo
// generado MaintenanceRecord.cs (regla 8). Si algún día se vuelve a hacer scaffold
// de maintenance_records, borrar este archivo y el mapeo de Custom.cs.
public partial class MaintenanceRecord
{
    /// <summary>PROGRAMADO, EN_CURSO, COMPLETADO o CANCELADO (MaintenanceStatusCodes).</summary>
    /// <remarks>Si no se asigna, la BD aplica su valor por defecto: PROGRAMADO.</remarks>
    public string Status { get; set; } = null!;

    public DateTime? StartedAt { get; set; }

    /// <summary>Incidente que originó un correctivo (trazabilidad opcional).</summary>
    public long? IncidentId { get; set; }

    public decimal? Cost { get; set; }

    /// <summary>Estado del activo al iniciar el mantenimiento.</summary>
    public long? StatusBeforeId { get; set; }

    /// <summary>Estado del activo al cerrar o cancelar el mantenimiento.</summary>
    public long? StatusAfterId { get; set; }

    public DateTime? CancelledAt { get; set; }

    public string? CancellationReason { get; set; }

    // Navegaciones mínimas, sin inversa en Incident ni en AssetStatus (generados).
    public virtual Incident? Incident { get; set; }

    public virtual AssetStatus? StatusBefore { get; set; }

    public virtual AssetStatus? StatusAfter { get; set; }
}
