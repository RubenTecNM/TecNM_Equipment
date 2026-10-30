using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TecnmInventario.Api.Common.Behaviors;
using TecnmInventario.Api.Data;

namespace TecnmInventario.Api.Tests.Integration;

/// <summary>
/// Resuelve la implementación REAL del pipeline (<see cref="DbContextTransactionScopeFactory"/>
/// y <see cref="TransactionHooks"/>) desde el contenedor de DI, igual que la
/// API, apuntando al PostgreSQL efímero del test. Evita duplicar la factory en
/// los tests: lo que se prueba es exactamente lo que se registra en producción.
/// </summary>
internal sealed class PipelineHarness : IDisposable
{
    private readonly ServiceProvider _provider;
    private readonly IServiceScope _scope;

    private PipelineHarness(ServiceProvider provider, IServiceScope scope)
    {
        _provider = provider;
        _scope = scope;
        Context = scope.ServiceProvider.GetRequiredService<TecnmInventarioDbContext>();
        Factory = scope.ServiceProvider.GetRequiredService<ITransactionScopeFactory>();
        Hooks = scope.ServiceProvider.GetRequiredService<ITransactionHooks>();
    }

    public TecnmInventarioDbContext Context { get; }

    public ITransactionScopeFactory Factory { get; }

    public ITransactionHooks Hooks { get; }

    public static PipelineHarness Create(PostgresDatabase database)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<TecnmInventarioDbContext>(
            options => options.UseNpgsql(database.ConnectionString));
        services.AddBusinessPipeline();

        var provider = services.BuildServiceProvider();
        return new PipelineHarness(provider, provider.CreateScope());
    }

    public void Dispose()
    {
        _scope.Dispose();
        _provider.Dispose();
    }
}
