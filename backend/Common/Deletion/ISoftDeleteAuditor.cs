using TecnmInventario.Api.Core.Contracts;

namespace TecnmInventario.Api.Common.Deletion;

/// <summary>
/// Encapsula el patrón completo de baja lógica ya probado en DeleteDocument:
/// concurrencia optimista (ExpectedVersion), soft delete, transacción y
/// registro en audit_log. Reusable por cualquier entidad ISoftDeletable.
/// </summary>
public interface ISoftDeleteAuditor
{
    /// <summary>
    /// Marca la entidad como eliminada, respetando la versión esperada por
    /// el cliente (409 si no coincide), y registra la baja en audit_log
    /// dentro de una transacción. Devuelve la entidad recargada después de
    /// que los triggers de Postgres (deleted_at, version) se aplicaron.
    /// recordId lo pasa el Handler explícitamente (cada entidad tiene su
    /// propia PK con nombre distinto -- DocumentId, SupplierId, etc. --
    /// y adivinarlo por reflexión es frágil).
    /// </summary>
    Task<TEntity> DeleteAsync<TEntity>(
        TEntity entity,
        long recordId,
        int expectedVersion,
        string tableName,
        object snapshotBeforeDelete,
        CancellationToken cancellationToken)
        where TEntity : class, ISoftDeletable;
}
