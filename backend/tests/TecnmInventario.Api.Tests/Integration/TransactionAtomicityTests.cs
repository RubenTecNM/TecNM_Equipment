using Mediator;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using TecnmInventario.Api.Common.Audit;
using TecnmInventario.Api.Common.Behaviors;
using TecnmInventario.Api.Common.Deletion;
using TecnmInventario.Api.Common.Storage;
using TecnmInventario.Api.Features.Assets.CreateAsset;
using TecnmInventario.Api.Features.Documents.UploadDocument;
using TecnmInventario.Api.Core.Entities;

namespace TecnmInventario.Api.Tests.Integration;

/// <summary>
/// Pruebas de atomicidad con transacciones REALES de PostgreSQL (b, c, d, e):
/// si algo falla, ni las filas de negocio ni audit_log quedan confirmadas.
/// </summary>
[Trait("Category", "Integration")]
public sealed class TransactionAtomicityTests : PostgresTestBase
{
    private sealed record TransactionalTestCommand : ICommand<string>, ITransactionalCommand;

    public TransactionAtomicityTests(PostgresContainerFixture fixture)
        : base(fixture)
    {
    }

    // ---------- b) atomicidad de fila + audit_log ----------

    [DockerFact]
    public async Task CreateAsset_si_falla_la_auditoria_no_deja_activo_ni_auditoria()
    {
        await using var db = await CreateDatabaseAsync();
        using var pipeline = PipelineHarness.Create(db);
        var handler = new CreateAssetHandler(
            pipeline.Context, new FakeCurrentUser(), new RecordingAuditLogService { FailOnLog = true });
        var behavior = new TransactionBehavior<CreateAssetCommand, CreateAssetResponse>(
            pipeline.Context, pipeline.Factory, pipeline.Hooks,
            NullLogger<TransactionBehavior<CreateAssetCommand, CreateAssetResponse>>.Instance);

        await Assert.ThrowsAsync<InvalidOperationException>(() => behavior.Handle(
            CreateAssetCommandFor("TEC-INT-ROLLBACK"),
            (message, ct) => handler.Handle(message, ct),
            CancellationToken.None).AsTask());

        Assert.Equal(0, await db.CountAsync("assets"));
        Assert.Equal(0, await db.CountAsync("audit_log"));
    }

    [DockerFact]
    public async Task CreateAsset_camino_feliz_confirma_activo_y_auditoria_en_la_misma_transaccion()
    {
        await using var db = await CreateDatabaseAsync();
        using var pipeline = PipelineHarness.Create(db);
        var audit = new AuditLogService(pipeline.Context, new FakeCurrentUser());
        var handler = new CreateAssetHandler(pipeline.Context, new FakeCurrentUser(), audit);
        var behavior = new TransactionBehavior<CreateAssetCommand, CreateAssetResponse>(
            pipeline.Context, pipeline.Factory, pipeline.Hooks,
            NullLogger<TransactionBehavior<CreateAssetCommand, CreateAssetResponse>>.Instance);

        var response = await behavior.Handle(
            CreateAssetCommandFor("TEC-INT-OK"),
            (message, ct) => handler.Handle(message, ct),
            CancellationToken.None);

        Assert.Equal(1, await db.CountAsync("assets"));
        Assert.Equal(1, await db.CountAsync(
            "audit_log", $"table_name = 'assets' and record_id = {response.AssetId} and action = 'INSERT'"));
        Assert.Equal(0, await db.CountAsync("audit_log", "action <> 'INSERT'"));
    }

    // ---------- c) implementación real de ITransactionScopeFactory ----------

    [DockerFact]
    public async Task Factory_real_commit_visible_desde_otra_conexion_y_rollback_no_visible()
    {
        await using var db = await CreateDatabaseAsync();
        using var pipeline = PipelineHarness.Create(db);
        var factory = pipeline.Factory;

        Assert.False(factory.HasActiveTransaction);

        await using (var transaction = await factory.BeginAsync(CancellationToken.None))
        {
            Assert.True(factory.HasActiveTransaction);
            await pipeline.Context.Database.ExecuteSqlRawAsync(
                "insert into inventario.suppliers (name) values ('Proveedor commit')");
            await transaction.CommitAsync(CancellationToken.None);
        }

        Assert.False(factory.HasActiveTransaction);
        Assert.Equal(1, await db.CountAsync("suppliers", "name = 'Proveedor commit'"));

        await using (var transaction = await factory.BeginAsync(CancellationToken.None))
        {
            Assert.True(factory.HasActiveTransaction);
            await pipeline.Context.Database.ExecuteSqlRawAsync(
                "insert into inventario.suppliers (name) values ('Proveedor rollback')");
            await transaction.RollbackAsync(CancellationToken.None);
        }

        Assert.False(factory.HasActiveTransaction);
        Assert.Equal(0, await db.CountAsync("suppliers", "name = 'Proveedor rollback'"));
    }

    [DockerFact]
    public async Task Factory_real_detecta_una_transaccion_ya_abierta_por_el_llamador()
    {
        await using var db = await CreateDatabaseAsync();
        using var pipeline = PipelineHarness.Create(db);
        var factory = pipeline.Factory;

        await using var manual = await pipeline.Context.Database.BeginTransactionAsync();
        Assert.True(factory.HasActiveTransaction);

        // El behavior no debe anidar ni confirmar la transacción del llamador.
        var behavior = new TransactionBehavior<TransactionalTestCommand, string>(
            pipeline.Context, factory, pipeline.Hooks,
            NullLogger<TransactionBehavior<TransactionalTestCommand, string>>.Instance);

        var result = await behavior.Handle(
            new TransactionalTestCommand(),
            async (_, ct) =>
            {
                await pipeline.Context.Database.ExecuteSqlRawAsync(
                    "insert into inventario.suppliers (name) values ('Proveedor manual')", ct);
                return "ok";
            },
            CancellationToken.None);

        Assert.Equal("ok", result);
        Assert.Same(manual, pipeline.Context.Database.CurrentTransaction);
        await manual.RollbackAsync(CancellationToken.None);
        Assert.Equal(0, await db.CountAsync("suppliers", "name = 'Proveedor manual'"));
    }

    // ---------- d) orden commit -> hooks, rollback -> hooks ----------

    [DockerFact]
    public async Task Hook_de_commit_ve_la_fila_ya_confirmada_y_no_se_ejecuta_si_hay_rollback()
    {
        await using var db = await CreateDatabaseAsync();
        using var pipeline = PipelineHarness.Create(db);

        var events = new List<string>();
        pipeline.Hooks.OnCommit(async () =>
            events.Add($"commit visible={await db.CountAsync("suppliers", "name = 'hook-commit'")}"));
        pipeline.Hooks.OnRollback(() =>
        {
            events.Add("rollback");
            return Task.CompletedTask;
        });

        var behavior = new TransactionBehavior<TransactionalTestCommand, string>(
            pipeline.Context, pipeline.Factory, pipeline.Hooks,
            NullLogger<TransactionBehavior<TransactionalTestCommand, string>>.Instance);

        await behavior.Handle(
            new TransactionalTestCommand(),
            async (_, ct) =>
            {
                await pipeline.Context.Database.ExecuteSqlRawAsync(
                    "insert into inventario.suppliers (name) values ('hook-commit')", ct);
                return "ok";
            },
            CancellationToken.None);

        // La fila ya estaba confirmada cuando corrió el hook.
        Assert.Equal(new[] { "commit visible=1" }, events);

        // Segunda parte: un handler que falla no ejecuta el hook de commit y sí
        // el de rollback; la fila no queda.
        using var failingPipeline = PipelineHarness.Create(db);
        var failingEvents = new List<string>();
        failingPipeline.Hooks.OnCommit(() =>
        {
            failingEvents.Add("commit");
            return Task.CompletedTask;
        });
        failingPipeline.Hooks.OnRollback(async () =>
        {
            failingEvents.Add($"rollback visible={await db.CountAsync("suppliers", "name = 'hook-rollback'")}");
        });

        var failingBehavior = new TransactionBehavior<TransactionalTestCommand, string>(
            failingPipeline.Context, failingPipeline.Factory, failingPipeline.Hooks,
            NullLogger<TransactionBehavior<TransactionalTestCommand, string>>.Instance);

        await Assert.ThrowsAsync<InvalidOperationException>(() => failingBehavior.Handle(
            new TransactionalTestCommand(),
            async (_, ct) =>
            {
                await failingPipeline.Context.Database.ExecuteSqlRawAsync(
                    "insert into inventario.suppliers (name) values ('hook-rollback')", ct);
                throw new InvalidOperationException("fallo simulado tras escribir");
            },
            CancellationToken.None).AsTask());

        Assert.Equal(new[] { "rollback visible=0" }, failingEvents);
        Assert.Equal(0, await db.CountAsync("suppliers", "name = 'hook-rollback'"));
    }

    [DockerFact]
    public async Task UploadDocument_si_falla_la_auditoria_no_deja_fila_ni_archivo()
    {
        await using var db = await CreateDatabaseAsync();
        await db.ExecuteAsync(
            "insert into inventario.acquisitions (acquisition_id, currency_code) " +
            "overriding system value values (1, 'MXN')");

        var root = Path.Combine(Path.GetTempPath(), $"tecnm-int-upload-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);

        try
        {
            using var pipeline = PipelineHarness.Create(db);
            var storage = new LocalDiskFileStorageService(
                Options.Create(new FileStorageOptions { LocalPath = root }));
            var audit = new RecordingAuditLogService { FailOnLog = true };
            var handler = new UploadDocumentHandler(
                pipeline.Context, storage, new FakeDocumentAccessAuthorizer(),
                new FakeCurrentUser(), pipeline.Hooks, audit);
            var behavior = new TransactionBehavior<UploadDocumentCommand, UploadDocumentResponse>(
                pipeline.Context, pipeline.Factory, pipeline.Hooks,
                NullLogger<TransactionBehavior<UploadDocumentCommand, UploadDocumentResponse>>.Instance);

            await Assert.ThrowsAsync<InvalidOperationException>(() => behavior.Handle(
                new UploadDocumentCommand("ACQUISITION", 1, "evidencia", TestUploadFile.Create()),
                (message, ct) => handler.Handle(message, ct),
                CancellationToken.None).AsTask());

            Assert.Equal(0, await db.CountAsync("documents"));
            Assert.Empty(Directory.GetFiles(root));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    // ---------- e) SoftDeleteAuditor dentro de una transacción real ----------

    [DockerFact]
    public async Task SoftDeleteAuditor_reutiliza_la_transaccion_del_behavior_y_confirma_junto_con_la_auditoria()
    {
        await using var db = await CreateDatabaseAsync();
        using var pipeline = PipelineHarness.Create(db);

        var supplier = await AddSupplierAsync(pipeline.Context, "Proveedor borrado");

        var auditor = new SoftDeleteAuditor(
            pipeline.Context,
            new AuditLogService(pipeline.Context, new FakeCurrentUser()),
            new FakeCurrentUser(),
            pipeline.Factory,
            NullLogger<SoftDeleteAuditor>.Instance);

        var behavior = new TransactionBehavior<TransactionalTestCommand, string>(
            pipeline.Context, pipeline.Factory, pipeline.Hooks,
            NullLogger<TransactionBehavior<TransactionalTestCommand, string>>.Instance);

        await behavior.Handle(
            new TransactionalTestCommand(),
            async (_, ct) =>
            {
                await auditor.DeleteAsync(
                    supplier, supplier.SupplierId, expectedVersion: 1, "suppliers",
                    new { supplier.Name }, ct);
                return "ok";
            },
            CancellationToken.None);

        Assert.Equal(1, await db.CountAsync(
            "suppliers", $"supplier_id = {supplier.SupplierId} and is_deleted"));
        Assert.Equal(2, await db.ScalarAsync<int>(
            $"select version from inventario.suppliers where supplier_id = {supplier.SupplierId}"));
        Assert.Equal(1, await db.CountAsync(
            "audit_log", $"table_name = 'suppliers' and record_id = {supplier.SupplierId} and action = 'DELETE'"));
    }

    [DockerFact]
    public async Task SoftDeleteAuditor_si_el_behavior_revierte_no_queda_borrado_ni_auditoria()
    {
        await using var db = await CreateDatabaseAsync();
        using var pipeline = PipelineHarness.Create(db);

        var supplier = await AddSupplierAsync(pipeline.Context, "Proveedor revertido");

        var auditor = new SoftDeleteAuditor(
            pipeline.Context,
            new AuditLogService(pipeline.Context, new FakeCurrentUser()),
            new FakeCurrentUser(),
            pipeline.Factory,
            NullLogger<SoftDeleteAuditor>.Instance);

        var behavior = new TransactionBehavior<TransactionalTestCommand, string>(
            pipeline.Context, pipeline.Factory, pipeline.Hooks,
            NullLogger<TransactionBehavior<TransactionalTestCommand, string>>.Instance);

        await Assert.ThrowsAsync<InvalidOperationException>(() => behavior.Handle(
            new TransactionalTestCommand(),
            async (_, ct) =>
            {
                await auditor.DeleteAsync(
                    supplier, supplier.SupplierId, expectedVersion: 1, "suppliers",
                    new { supplier.Name }, ct);
                throw new InvalidOperationException("fallo posterior al borrado");
            },
            CancellationToken.None).AsTask());

        Assert.Equal(1, await db.CountAsync(
            "suppliers", $"supplier_id = {supplier.SupplierId} and not is_deleted"));
        Assert.Equal(0, await db.CountAsync("audit_log"));
    }

    private static CreateAssetCommand CreateAssetCommandFor(string inventoryCode)
        => new(inventoryCode, 1, null, null, null, null, null, "activo de integración");

    private static async Task<Supplier> AddSupplierAsync(
        TecnmInventario.Api.Data.TecnmInventarioDbContext context, string name)
    {
        var now = DateTime.UtcNow;
        var supplier = new Supplier
        {
            Name = name,
            CreatedAt = now,
            UpdatedAt = now,
            Version = 1
        };
        context.Suppliers.Add(supplier);
        await context.SaveChangesAsync();
        return supplier;
    }
}
