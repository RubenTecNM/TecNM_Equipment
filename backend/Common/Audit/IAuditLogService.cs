namespace TecnmInventario.Api.Common.Audit;

/// <summary>
/// Escribe entradas en audit_log (tabla inmutable de auditoría, sin trigger
/// automático en la BD: la aplicación es la única responsable de llenarla).
/// Solo inserta; audit_log no debe modificarse ni eliminarse desde la app.
/// </summary>
public interface IAuditLogService
{
    Task LogAsync(
        string tableName,
        long recordId,
        string action,
        object? oldData,
        object? newData,
        CancellationToken cancellationToken);
}
