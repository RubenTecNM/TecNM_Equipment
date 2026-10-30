namespace TecnmInventario.Api.Common.Behaviors;

/// <summary>
/// Transacción de la unidad de trabajo. Se abstrae del DbContext para poder
/// probar los caminos de commit/rollback sin PostgreSQL (el proveedor en
/// memoria ignora las transacciones).
/// </summary>
public interface ITransactionScope : IAsyncDisposable
{
    Task CommitAsync(CancellationToken cancellationToken);

    Task RollbackAsync(CancellationToken cancellationToken);
}

/// <summary>
/// Abre la transacción de la unidad de trabajo y detecta si el contexto ya
/// está dentro de una. Si ya hay una, el dueño es el llamador y el pipeline
/// no debe anidar ni confirmar por su cuenta.
/// </summary>
public interface ITransactionScopeFactory
{
    bool HasActiveTransaction { get; }

    Task<ITransactionScope> BeginAsync(CancellationToken cancellationToken);
}
