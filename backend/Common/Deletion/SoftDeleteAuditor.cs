using Microsoft.EntityFrameworkCore;
using TecnmInventario.Api.Common.Audit;
using TecnmInventario.Api.Common.Behaviors;
using TecnmInventario.Api.Common.Security;
using TecnmInventario.Api.Data;
using TecnmInventario.Api.Core.Contracts;

namespace TecnmInventario.Api.Common.Deletion;

public sealed class SoftDeleteAuditor : ISoftDeleteAuditor
{
    private readonly TecnmInventarioDbContext _context;
    private readonly IAuditLogService _auditLog;
    private readonly ICurrentUserService _currentUser;
    private readonly ITransactionScopeFactory _transactionFactory;
    private readonly ILogger<SoftDeleteAuditor> _logger;

    public SoftDeleteAuditor(
        TecnmInventarioDbContext context,
        IAuditLogService auditLog,
        ICurrentUserService currentUser,
        ITransactionScopeFactory transactionFactory,
        ILogger<SoftDeleteAuditor> logger)
    {
        _context = context;
        _auditLog = auditLog;
        _currentUser = currentUser;
        _transactionFactory = transactionFactory;
        _logger = logger;
    }

    public async Task<TEntity> DeleteAsync<TEntity>(
        TEntity entity,
        long recordId,
        int expectedVersion,
        string tableName,
        object snapshotBeforeDelete,
        CancellationToken cancellationToken)
        where TEntity : class, ISoftDeletable
    {
        // Concurrencia optimista real: comparamos contra la versión que el
        // CLIENTE vio por última vez, no contra la que se acaba de leer.
        // Si alguien más modificó/borró la fila mientras tanto, el UPDATE
        // con WHERE version=ExpectedVersion afecta 0 filas y EF Core lanza
        // DbUpdateConcurrencyException (-> 409 en el middleware).
        _context.Entry(entity).Property(nameof(ISoftDeletable.Version)).OriginalValue = expectedVersion;

        entity.IsDeleted = true;
        entity.DeletedBy = _currentUser.UserId;
        // DeletedAt, UpdatedAt y Version los actualizan los triggers de
        // Postgres (prevent_invalid_soft_delete y touch_version).

        // Si ya hay una transacción abierta (por ejemplo, un comando marcado
        // con ITransactionalCommand), el dueño es el llamador: aquí solo se
        // reutiliza y NO se confirma ni se revierte.
        var ownsTransaction = !_transactionFactory.HasActiveTransaction;
        await using var transaction = ownsTransaction
            ? await _transactionFactory.BeginAsync(cancellationToken)
            : null;

        try
        {
            await _context.SaveChangesAsync(cancellationToken);

            // Releemos para capturar los valores reales que pusieron los
            // triggers (deleted_at, version incrementada). No se ve afectado
            // por HasQueryFilter: verificado con prueba real en Documents.
            await _context.Entry(entity).ReloadAsync(cancellationToken);

            var afterSnapshot = new
            {
                entity.IsDeleted,
                entity.DeletedAt,
                entity.DeletedBy,
                entity.Version
            };

            await _auditLog.LogAsync(
                tableName: tableName,
                recordId: recordId,
                action: "DELETE",
                oldData: snapshotBeforeDelete,
                newData: afterSnapshot,
                cancellationToken);

            if (ownsTransaction)
            {
                await transaction!.CommitAsync(cancellationToken);
            }
        }
        catch
        {
            if (ownsTransaction)
            {
                try
                {
                    await transaction!.RollbackAsync(CancellationToken.None);
                }
                catch (Exception rollbackException)
                {
                    _logger.LogError(rollbackException, "Falló el rollback del borrado de {TableName}.", tableName);
                }
            }

            throw;
        }

        return entity;
    }
}
