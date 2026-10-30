namespace TecnmInventario.Api.Tests.Integration;

/// <summary>
/// Verifica que la plantilla se puede clonar varias veces seguidas (sin
/// conexiones colgadas en el pool que bloqueen CREATE DATABASE ... TEMPLATE)
/// y que cada clon es una base independiente.
/// </summary>
[Trait("Category", "Integration")]
public sealed class PostgresFixtureTests : PostgresTestBase
{
    public PostgresFixtureTests(PostgresContainerFixture fixture)
        : base(fixture)
    {
    }

    [DockerFact]
    public async Task Clonar_la_plantilla_varias_veces_seguidas_funciona_y_estan_aisladas()
    {
        var databases = new List<PostgresDatabase>();
        try
        {
            for (var i = 1; i <= 3; i++)
            {
                databases.Add(await CreateDatabaseAsync());
            }

            Assert.Equal(3, databases.Select(d => d.Name).Distinct().Count());

            foreach (var database in databases)
            {
                Assert.Equal(0, await database.CountAsync("suppliers"));
                await database.ExecuteAsync(
                    $"insert into inventario.suppliers (name) values ('clon-{database.Name}')");
            }

            foreach (var database in databases)
            {
                Assert.Equal(1, await database.CountAsync("suppliers"));
                Assert.Equal(1, await database.CountAsync(
                    "suppliers", $"name = 'clon-{database.Name}'"));
            }
        }
        finally
        {
            foreach (var database in databases)
            {
                await database.DisposeAsync();
            }
        }
    }
}
