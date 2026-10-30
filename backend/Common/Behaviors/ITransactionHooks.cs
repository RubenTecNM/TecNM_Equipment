namespace TecnmInventario.Api.Common.Behaviors;

/// <summary>
/// Acciones que deben ejecutarse después del commit o después de un rollback:
/// invalidar una caché cuando la escritura ya quedó confirmada, o compensar
/// un efecto externo (por ejemplo, borrar un archivo en disco) cuando la
/// transacción no se confirmó.
/// </summary>
public interface ITransactionHooks
{
    void OnCommit(Func<Task> action);

    void OnRollback(Func<Task> action);

    Task RunCommitAsync();

    Task RunRollbackAsync();
}
