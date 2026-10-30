using Microsoft.Extensions.Logging;

namespace TecnmInventario.Api.Common.Behaviors;

/// <summary>
/// Cola scoped de acciones post-commit / rollback. Los errores de una acción
/// se registran y no se propagan: si el commit ya ocurrió, la respuesta no
/// puede convertirse en un 500 por una tarea auxiliar.
/// </summary>
public sealed class TransactionHooks : ITransactionHooks
{
    private readonly List<Func<Task>> _onCommit = new();
    private readonly List<Func<Task>> _onRollback = new();
    private readonly ILogger<TransactionHooks> _logger;

    public TransactionHooks(ILogger<TransactionHooks> logger)
    {
        _logger = logger;
    }

    public void OnCommit(Func<Task> action) => _onCommit.Add(action);

    public void OnRollback(Func<Task> action) => _onRollback.Add(action);

    public async Task RunCommitAsync()
    {
        foreach (var action in _onCommit)
        {
            try
            {
                await action();
            }
            catch (Exception ex)
            {
                // El commit ya ocurrió: no se puede revertir. Se registra y se sigue.
                _logger.LogError(ex, "Falló una acción post-commit.");
            }
        }

        _onCommit.Clear();
        // Las compensaciones de rollback ya no aplican tras un commit exitoso.
        _onRollback.Clear();
    }

    public async Task RunRollbackAsync()
    {
        // Las compensaciones se ejecutan en orden inverso (LIFO).
        for (var i = _onRollback.Count - 1; i >= 0; i--)
        {
            try
            {
                await _onRollback[i]();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Falló una acción de compensación tras el rollback.");
            }
        }

        _onRollback.Clear();
        _onCommit.Clear();
    }
}
