using FluentValidation;
using Mediator;
using Microsoft.EntityFrameworkCore;
using TecnmInventario.Api.Common.Behaviors;
using TecnmInventario.Api.Data;

namespace TecnmInventario.Api.Tests.Integration;

/// <summary>
/// 2.8: el ValidationBehavior debe ejecutar los validadores en serie. Varios
/// validadores comparten el DbContext scoped del request; si se lanzan en
/// paralelo, EF Core detecta "A second operation was started on this context
/// instance" en cuanto una consulta sigue en vuelo cuando empieza la otra.
/// </summary>
[Trait("Category", "Integration")]
public sealed class ValidationBehaviorIntegrationTests : PostgresTestBase
{
    private sealed record ProbeCommand(string Value) : ICommand<string>;

    /// <summary>
    /// Se apoya en <c>pg_sleep</c> para que la primera operación siga en vuelo
    /// cuando arranca la segunda. Con InMemory las consultas terminan de forma
    /// síncrona y el detector de concurrencia de EF nunca llega a dispararse.
    /// </summary>
    private sealed class SlowDbValidator : AbstractValidator<ProbeCommand>
    {
        public SlowDbValidator(TecnmInventarioDbContext context)
        {
            RuleFor(x => x.Value).MustAsync(async (_, cancellationToken) =>
            {
                await context.Database.ExecuteSqlRawAsync("select pg_sleep(0.05)", cancellationToken);
                return true;
            });
        }
    }

    public ValidationBehaviorIntegrationTests(PostgresContainerFixture fixture)
        : base(fixture)
    {
    }

    [DockerFact]
    public async Task Validadores_que_comparten_el_DbContext_se_ejecutan_sin_solaparse()
    {
        await using var db = await CreateDatabaseAsync();
        using var context = db.CreateContext();

        var behavior = new ValidationBehavior<ProbeCommand, string>(
            new IValidator<ProbeCommand>[]
            {
                new SlowDbValidator(context),
                new SlowDbValidator(context)
            });

        var result = await behavior.Handle(
            new ProbeCommand("x"),
            (_, _) => ValueTask.FromResult("ok"),
            CancellationToken.None);

        Assert.Equal("ok", result);
    }
}
