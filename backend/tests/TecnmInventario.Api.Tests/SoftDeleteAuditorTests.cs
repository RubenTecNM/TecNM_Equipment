using Microsoft.Extensions.Logging.Abstractions;
using TecnmInventario.Api.Common.Behaviors;
using TecnmInventario.Api.Common.Deletion;
using TecnmInventario.Api.Core.Entities;

namespace TecnmInventario.Api.Tests;

public class SoftDeleteAuditorTests
{
    private static (TecnmInventario.Api.Data.TecnmInventarioDbContext Context, Supplier Supplier) CreateContext()
    {
        var context = TestDb.Create();
        var now = DateTime.UtcNow;
        var supplier = new Supplier
        {
            SupplierId = 1,
            Name = "Proveedor de prueba",
            CreatedAt = now,
            UpdatedAt = now,
            Version = 1
        };
        context.Suppliers.Add(supplier);
        context.SaveChanges();
        return (context, supplier);
    }

    private static SoftDeleteAuditor CreateAuditor(
        TecnmInventario.Api.Data.TecnmInventarioDbContext context,
        ITransactionScopeFactory factory,
        RecordingAuditLogService audit)
        => new(
            context,
            audit,
            new FakeCurrentUser(),
            factory,
            NullLogger<SoftDeleteAuditor>.Instance);

    [Fact]
    public async Task Sin_transaccion_previa_abre_una_y_la_confirma()
    {
        var (context, supplier) = CreateContext();
        using var _ = context;
        var factory = new FakeTransactionScopeFactory();
        var audit = new RecordingAuditLogService();
        var auditor = CreateAuditor(context, factory, audit);

        var result = await auditor.DeleteAsync(
            supplier,
            recordId: 1,
            expectedVersion: 1,
            tableName: "suppliers",
            snapshotBeforeDelete: new { supplier.Name },
            CancellationToken.None);

        Assert.True(result.IsDeleted);
        Assert.Equal(1, result.DeletedBy);
        Assert.Equal(1, factory.BeginCount);
        Assert.NotNull(factory.Scope);
        Assert.Equal(1, factory.Scope!.CommitCount);
        Assert.Equal(0, factory.Scope.RollbackCount);
        Assert.True(factory.Scope.Disposed);
        Assert.Single(audit.Entries);
        Assert.Equal("DELETE", audit.Entries[0].Action);
        Assert.Equal("suppliers", audit.Entries[0].TableName);
    }

    [Fact]
    public async Task Con_transaccion_previa_la_reutiliza_sin_confirmar_ni_revertir()
    {
        var (context, supplier) = CreateContext();
        using var _ = context;
        var factory = new FakeTransactionScopeFactory { HasActiveTransaction = true };
        var audit = new RecordingAuditLogService();
        var auditor = CreateAuditor(context, factory, audit);

        await auditor.DeleteAsync(
            supplier,
            recordId: 1,
            expectedVersion: 1,
            tableName: "suppliers",
            snapshotBeforeDelete: new { supplier.Name },
            CancellationToken.None);

        // El dueño de la transacción es el llamador: aquí no se abre, confirma
        // ni revierte nada.
        Assert.Equal(0, factory.BeginCount);
        Assert.Null(factory.Scope);
        Assert.Single(audit.Entries);
        Assert.True(supplier.IsDeleted);
    }

    [Fact]
    public async Task Si_falla_la_auditoria_hace_rollback_y_relanza()
    {
        var (context, supplier) = CreateContext();
        using var _ = context;
        var factory = new FakeTransactionScopeFactory();
        var audit = new RecordingAuditLogService { FailOnLog = true };
        var auditor = CreateAuditor(context, factory, audit);

        await Assert.ThrowsAsync<InvalidOperationException>(() => auditor.DeleteAsync(
            supplier,
            recordId: 1,
            expectedVersion: 1,
            tableName: "suppliers",
            snapshotBeforeDelete: new { supplier.Name },
            CancellationToken.None));

        Assert.NotNull(factory.Scope);
        Assert.Equal(1, factory.Scope!.RollbackCount);
        Assert.Equal(0, factory.Scope.CommitCount);
        Assert.Empty(audit.Entries);
    }

    [Fact]
    public async Task Con_transaccion_previa_un_fallo_no_revierte_la_del_llamador()
    {
        var (context, supplier) = CreateContext();
        using var _ = context;
        var factory = new FakeTransactionScopeFactory { HasActiveTransaction = true };
        var audit = new RecordingAuditLogService { FailOnLog = true };
        var auditor = CreateAuditor(context, factory, audit);

        await Assert.ThrowsAsync<InvalidOperationException>(() => auditor.DeleteAsync(
            supplier,
            recordId: 1,
            expectedVersion: 1,
            tableName: "suppliers",
            snapshotBeforeDelete: new { supplier.Name },
            CancellationToken.None));

        Assert.Equal(0, factory.BeginCount);
        Assert.Null(factory.Scope);
    }
}
