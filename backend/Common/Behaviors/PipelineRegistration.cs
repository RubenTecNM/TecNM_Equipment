using Mediator;

namespace TecnmInventario.Api.Common.Behaviors;

/// <summary>
/// Registro del pipeline de negocio. Mediator resuelve
/// IPipelineBehavior&lt;,&gt; como IEnumerable y respeta el orden de registro:
/// primero validación (fuera de la transacción) y después transacción.
/// </summary>
public static class PipelineRegistration
{
    public static IServiceCollection AddBusinessPipeline(this IServiceCollection services)
    {
        services.AddScoped<ITransactionHooks, TransactionHooks>();
        services.AddScoped<ITransactionScopeFactory, DbContextTransactionScopeFactory>();
        services.AddScoped(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
        services.AddScoped(typeof(IPipelineBehavior<,>), typeof(TransactionBehavior<,>));
        return services;
    }
}
