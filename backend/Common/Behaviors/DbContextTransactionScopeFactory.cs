using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using TecnmInventario.Api.Data;

namespace TecnmInventario.Api.Common.Behaviors;

/// <summary>
/// Implementación real sobre EF Core. El scope es el dueño de la transacción
/// y solo él puede confirmarla o revertirla.
/// </summary>
internal sealed class DbContextTransactionScopeFactory : ITransactionScopeFactory
{
    private readonly TecnmInventarioDbContext _context;

    public DbContextTransactionScopeFactory(TecnmInventarioDbContext context)
    {
        _context = context;
    }

    public bool HasActiveTransaction => _context.Database.CurrentTransaction is not null;

    public async Task<ITransactionScope> BeginAsync(CancellationToken cancellationToken)
    {
        var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        return new DbContextTransactionScope(transaction);
    }

    private sealed class DbContextTransactionScope : ITransactionScope
    {
        private readonly IDbContextTransaction _transaction;

        public DbContextTransactionScope(IDbContextTransaction transaction)
        {
            _transaction = transaction;
        }

        public Task CommitAsync(CancellationToken cancellationToken)
            => _transaction.CommitAsync(cancellationToken);

        public Task RollbackAsync(CancellationToken cancellationToken)
            => _transaction.RollbackAsync(cancellationToken);

        public ValueTask DisposeAsync() => _transaction.DisposeAsync();
    }
}
