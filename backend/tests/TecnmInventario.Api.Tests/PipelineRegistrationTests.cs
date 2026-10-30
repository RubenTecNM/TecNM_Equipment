using Mediator;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TecnmInventario.Api.Common.Behaviors;
using TecnmInventario.Api.Data;

namespace TecnmInventario.Api.Tests;

public class PipelineRegistrationTests
{
    private sealed record TestCommand(string Value) : ICommand<string>, ITransactionalCommand;

    [Fact]
    public void El_pipeline_registra_validacion_antes_que_transaccion()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<TecnmInventarioDbContext>(options =>
            options.UseInMemoryDatabase($"pipeline-{Guid.NewGuid()}"));
        services.AddBusinessPipeline();

        using var provider = services.BuildServiceProvider();
        var behaviors = provider
            .GetServices<IPipelineBehavior<TestCommand, string>>()
            .ToList();

        Assert.Collection(
            behaviors,
            first => Assert.IsType<ValidationBehavior<TestCommand, string>>(first),
            second => Assert.IsType<TransactionBehavior<TestCommand, string>>(second));
    }

    [Fact]
    public void Los_servicios_del_pipeline_quedan_registrados_como_scoped()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<TecnmInventarioDbContext>(options =>
            options.UseInMemoryDatabase($"pipeline-{Guid.NewGuid()}"));
        services.AddBusinessPipeline();

        var hooks = services.Single(d => d.ServiceType == typeof(ITransactionHooks));
        var factory = services.Single(d => d.ServiceType == typeof(ITransactionScopeFactory));

        Assert.Equal(ServiceLifetime.Scoped, hooks.Lifetime);
        Assert.Equal(ServiceLifetime.Scoped, factory.Lifetime);
    }
}
