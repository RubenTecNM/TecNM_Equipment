namespace TecnmInventario.Api.Features.Maintenance.Shared;

public static class MaintenanceDates
{
    /// <summary>
    /// Npgsql solo escribe en timestamptz fechas UTC. Un JSON sin zona horaria
    /// llega con Kind=Unspecified (se interpreta como UTC) y uno con otra zona,
    /// como Local (se convierte).
    /// </summary>
    public static DateTime ToUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
    };
}
