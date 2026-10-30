using Mediator;
using Microsoft.EntityFrameworkCore;
using TecnmInventario.Api.Data;

namespace TecnmInventario.Api.Common.Behaviors;

/// <summary>
/// Abre una transacción explícita alrededor de los comandos marcados con
/// <see cref="ITransactionalCommand"/>. El commit no puede caer en el catch
/// de rollback: una vez confirmada la transacción, un fallo en un hook
/// post-commit se registra en log y no revierte ni relanza.
/// </summary>
public sealed class TransactionBehavior<TMessage, TResponse>
    : IPipelineBehavior<TMessage, TResponse>
    where TMessage : IMessage
{
    private readonly TecnmInventarioDbContext _context;
    private readonly ITransactionScopeFactory _transactionFactory;
    private readonly ITransactionHooks _hooks;
    private readonly ILogger<TransactionBehavior<TMessage, TResponse>> _logger;

    public TransactionBehavior(
        TecnmInventarioDbContext context,
        ITransactionScopeFactory transactionFactory,
        ITransactionHooks hooks,
        ILogger<TransactionBehavior<TMessage, TResponse>> logger)
    {
        _context = context;
        _transactionFactory = transactionFactory;
        _hooks = hooks;
        _logger = logger;
    }

    public async ValueTask<TResponse> Handle(
        TMessage message,
        MessageHandlerDelegate<TMessage, TResponse> next,
        CancellationToken cancellationToken)
    {
        // Queries y comandos sin la marca no pagan transacción.
        if (message is not ITransactionalCommand)
        {
            return await next(message, cancellationToken);
        }

        // Si el handler ya abrió una transacción, el dueño es él: no se anida
        // y tampoco se confirma ni revierte por su cuenta.
        if (_transactionFactory.HasActiveTransaction)
        {
            return await next(message, cancellationToken);
        }

        var strategy = _context.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _transactionFactory.BeginAsync(cancellationToken);
            var committed = false;
            TResponse response;

            try
            {
                response = await next(message, cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                committed = true;
            }
            catch
            {
                // Solo se compensa si el commit NO llegó a ocurrir.
                if (!committed)
                {
                    try
                    {
                        await transaction.RollbackAsync(CancellationToken.None);
                    }
                    catch (Exception rollbackException)
                    {
                        _logger.LogError(
                            rollbackException,
                            "Falló el rollback de {MessageType}.",
                            typeof(TMessage).Name);
                    }

                    await RunRollbackHooksAsync();
                }

                throw;
            }

            // Fuera del try de rollback: un fallo aquí ya no puede deshacer el
            // commit. Sus excepciones se capturan y se registran, sin relanzar.
            await RunCommitHooksAsync();
            return response;
        });
    }

    private async Task RunCommitHooksAsync()
    {
        try
        {
            await _hooks.RunCommitAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Fallaron las acciones post-commit de {MessageType}.",
                typeof(TMessage).Name);
        }
    }

    private async Task RunRollbackHooksAsync()
    {
        try
        {
            await _hooks.RunRollbackAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Fallaron las acciones de rollback de {MessageType}.",
                typeof(TMessage).Name);
        }
    }
}
