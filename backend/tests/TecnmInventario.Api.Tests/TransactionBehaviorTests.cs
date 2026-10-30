using Mediator;
using Microsoft.Extensions.Logging.Abstractions;
using TecnmInventario.Api.Common.Behaviors;

namespace TecnmInventario.Api.Tests;

public class TransactionBehaviorTests
{
    private sealed record TransactionalTestCommand(string Value) : ICommand<string>, ITransactionalCommand;

    private sealed record PlainTestCommand(string Value) : ICommand<string>;

    private static TransactionBehavior<TMessage, string> CreateBehavior<TMessage>(
        TecnmInventario.Api.Data.TecnmInventarioDbContext context,
        ITransactionScopeFactory factory,
        ITransactionHooks hooks)
        where TMessage : IMessage
        => new(
            context,
            factory,
            hooks,
            NullLogger<TransactionBehavior<TMessage, string>>.Instance);

    private static TransactionHooks CreateHooks()
        => new(NullLogger<TransactionHooks>.Instance);

    [Fact]
    public async Task Comando_sin_marca_no_abre_transaccion()
    {
        using var context = TestDb.Create();
        var factory = new FakeTransactionScopeFactory();
        var behavior = CreateBehavior<PlainTestCommand>(context, factory, CreateHooks());
        var called = false;

        var result = await behavior.Handle(
            new PlainTestCommand("x"),
            (_, _) =>
            {
                called = true;
                return ValueTask.FromResult("ok");
            },
            CancellationToken.None);

        Assert.Equal("ok", result);
        Assert.True(called);
        Assert.Equal(0, factory.BeginCount);
    }

    [Fact]
    public async Task Comando_marcado_abre_commit_y_ejecuta_hooks_de_commit()
    {
        using var context = TestDb.Create();
        var factory = new FakeTransactionScopeFactory();
        var hooks = CreateHooks();
        var commitHookRan = false;
        hooks.OnCommit(() =>
        {
            commitHookRan = true;
            return Task.CompletedTask;
        });
        var behavior = CreateBehavior<TransactionalTestCommand>(context, factory, hooks);

        var result = await behavior.Handle(
            new TransactionalTestCommand("x"),
            (_, _) => ValueTask.FromResult("ok"),
            CancellationToken.None);

        Assert.Equal("ok", result);
        Assert.Equal(1, factory.BeginCount);
        Assert.NotNull(factory.Scope);
        Assert.Equal(1, factory.Scope!.CommitCount);
        Assert.Equal(0, factory.Scope.RollbackCount);
        Assert.True(factory.Scope.Disposed);
        Assert.True(commitHookRan);
    }

    [Fact]
    public async Task Comando_marcado_con_transaccion_existente_no_anida()
    {
        using var context = TestDb.Create();
        var factory = new FakeTransactionScopeFactory { HasActiveTransaction = true };
        var hooks = CreateHooks();
        var commitHookRan = false;
        hooks.OnCommit(() =>
        {
            commitHookRan = true;
            return Task.CompletedTask;
        });
        var behavior = CreateBehavior<TransactionalTestCommand>(context, factory, hooks);
        var called = false;

        var result = await behavior.Handle(
            new TransactionalTestCommand("x"),
            (_, _) =>
            {
                called = true;
                return ValueTask.FromResult("ok");
            },
            CancellationToken.None);

        Assert.Equal("ok", result);
        Assert.True(called);
        Assert.Equal(0, factory.BeginCount);
        Assert.Null(factory.Scope);
        Assert.False(commitHookRan);
    }

    [Fact]
    public async Task Excepcion_del_handler_hace_rollback_y_ejecuta_hooks_de_rollback()
    {
        using var context = TestDb.Create();
        var factory = new FakeTransactionScopeFactory();
        var hooks = CreateHooks();
        var rollbackHookRan = false;
        var commitHookRan = false;
        hooks.OnRollback(() =>
        {
            rollbackHookRan = true;
            return Task.CompletedTask;
        });
        hooks.OnCommit(() =>
        {
            commitHookRan = true;
            return Task.CompletedTask;
        });
        var behavior = CreateBehavior<TransactionalTestCommand>(context, factory, hooks);

        await Assert.ThrowsAsync<InvalidOperationException>(() => behavior.Handle(
            new TransactionalTestCommand("x"),
            (_, _) => throw new InvalidOperationException("boom"),
            CancellationToken.None).AsTask());

        Assert.NotNull(factory.Scope);
        Assert.Equal(1, factory.Scope!.RollbackCount);
        Assert.Equal(0, factory.Scope.CommitCount);
        Assert.True(rollbackHookRan);
        Assert.False(commitHookRan);
    }

    [Fact]
    public async Task Commit_exitoso_con_hook_de_commit_que_falla_no_hace_rollback()
    {
        using var context = TestDb.Create();
        var factory = new FakeTransactionScopeFactory();
        var hooks = CreateHooks();
        var rollbackHookRan = false;
        hooks.OnCommit(() => throw new InvalidOperationException("hook de commit falló"));
        hooks.OnRollback(() =>
        {
            rollbackHookRan = true;
            return Task.CompletedTask;
        });
        var behavior = CreateBehavior<TransactionalTestCommand>(context, factory, hooks);

        var result = await behavior.Handle(
            new TransactionalTestCommand("x"),
            (_, _) => ValueTask.FromResult("ok"),
            CancellationToken.None);

        Assert.Equal("ok", result);
        Assert.NotNull(factory.Scope);
        Assert.Equal(1, factory.Scope!.CommitCount);
        Assert.Equal(0, factory.Scope.RollbackCount);
        Assert.False(rollbackHookRan);
    }

    [Fact]
    public async Task El_behavior_no_relanza_una_excepcion_del_runner_de_hooks_de_commit()
    {
        using var context = TestDb.Create();
        var factory = new FakeTransactionScopeFactory();
        var behavior = CreateBehavior<TransactionalTestCommand>(context, factory, new ThrowingHooks());

        var result = await behavior.Handle(
            new TransactionalTestCommand("x"),
            (_, _) => ValueTask.FromResult("ok"),
            CancellationToken.None);

        Assert.Equal("ok", result);
        Assert.NotNull(factory.Scope);
        Assert.Equal(1, factory.Scope!.CommitCount);
        Assert.Equal(0, factory.Scope.RollbackCount);
    }

    [Fact]
    public async Task Falla_del_commit_hace_rollback_y_ejecuta_hooks_de_rollback()
    {
        using var context = TestDb.Create();
        var factory = new FakeTransactionScopeFactory { CommitFails = true };
        var hooks = CreateHooks();
        var rollbackHookRan = false;
        var commitHookRan = false;
        hooks.OnRollback(() =>
        {
            rollbackHookRan = true;
            return Task.CompletedTask;
        });
        hooks.OnCommit(() =>
        {
            commitHookRan = true;
            return Task.CompletedTask;
        });
        var behavior = CreateBehavior<TransactionalTestCommand>(context, factory, hooks);

        await Assert.ThrowsAsync<InvalidOperationException>(() => behavior.Handle(
            new TransactionalTestCommand("x"),
            (_, _) => ValueTask.FromResult("ok"),
            CancellationToken.None).AsTask());

        Assert.NotNull(factory.Scope);
        Assert.Equal(1, factory.Scope!.CommitCount);
        Assert.Equal(1, factory.Scope.RollbackCount);
        Assert.True(rollbackHookRan);
        Assert.False(commitHookRan);
    }

    private sealed class ThrowingHooks : ITransactionHooks
    {
        public void OnCommit(Func<Task> action)
        {
        }

        public void OnRollback(Func<Task> action)
        {
        }

        public Task RunCommitAsync() => throw new InvalidOperationException("runner de commit falló");

        public Task RunRollbackAsync() => throw new InvalidOperationException("runner de rollback falló");
    }
}
