using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using TecnmInventario.Api.Common.Middleware;
using TecnmInventario.Api.Core.Entities;

namespace TecnmInventario.Api.Tests.Integration;

/// <summary>
/// h) La matriz de errores de integridad del middleware contra errores REALES
/// de PostgreSQL (23505, 23503, 23514, 22001), no simulados.
/// </summary>
[Trait("Category", "Integration")]
public sealed class PostgresErrorMatrixTests : PostgresTestBase
{
    public PostgresErrorMatrixTests(PostgresContainerFixture fixture)
        : base(fixture)
    {
    }

    [DockerFact]
    public async Task Violacion_de_unicidad_23505_se_traduce_a_409()
    {
        await using var db = await CreateDatabaseAsync();
        using var context = db.CreateContext();
        context.Permissions.Add(new Permission
        {
            Code = "assets.view", // ya existe en el seed
            Name = "Duplicado",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            Version = 1
        });

        var exception = await CaptureSaveChangesAsync(context, "23505");
        var (status, body) = await InvokeAsync(exception);

        Assert.Equal(409, status);
        Assert.Contains("Ya existe un registro con esos datos.", body);
        AssertNoTechnicalLeak(body);
    }

    [DockerFact]
    public async Task Violacion_de_clave_foranea_23503_se_traduce_a_400()
    {
        await using var db = await CreateDatabaseAsync();
        using var context = db.CreateContext();
        context.Assets.Add(new Asset
        {
            InventoryCode = "TEC-INT-FK",
            AssetTypeId = 999_999, // no existe en asset_types
            AssetStatusId = 1,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            Version = 1
        });

        var exception = await CaptureSaveChangesAsync(context, "23503");
        var (status, body) = await InvokeAsync(exception);

        Assert.Equal(400, status);
        Assert.Contains("La operación hace referencia a un registro", body);
        AssertNoTechnicalLeak(body);
    }

    [DockerFact]
    public async Task Violacion_de_check_23514_se_traduce_a_400()
    {
        await using var db = await CreateDatabaseAsync();
        using var context = db.CreateContext();
        context.Acquisitions.Add(new Acquisition
        {
            CurrencyCode = "MXN",
            WarrantyStart = new DateOnly(2026, 5, 10),
            WarrantyEnd = new DateOnly(2026, 5, 1), // CHECK warranty_end >= warranty_start
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            Version = 1
        });

        var exception = await CaptureSaveChangesAsync(context, "23514");
        var (status, body) = await InvokeAsync(exception);

        Assert.Equal(400, status);
        Assert.Contains("Los datos no cumplen una regla de integridad", body);
        AssertNoTechnicalLeak(body);
    }

    [DockerFact]
    public async Task Cadena_demasiado_larga_22001_se_traduce_a_400()
    {
        await using var db = await CreateDatabaseAsync();
        using var context = db.CreateContext();
        context.Roles.Add(new Role
        {
            Code = "ROL_LARGO",
            Name = new string('x', 200), // roles.name es varchar(120)
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            Version = 1
        });

        var exception = await CaptureSaveChangesAsync(context, "22001");
        var (status, body) = await InvokeAsync(exception);

        Assert.Equal(400, status);
        Assert.Contains("Uno de los valores excede la longitud permitida.", body);
        AssertNoTechnicalLeak(body);
    }

    private static async Task<DbUpdateException> CaptureSaveChangesAsync(
        Data.TecnmInventarioDbContext context, string expectedSqlState)
    {
        try
        {
            await context.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException pg)
        {
            Assert.Equal(expectedSqlState, pg.SqlState);
            return ex;
        }

        throw new Xunit.Sdk.XunitException(
            $"Se esperaba un error PostgreSQL {expectedSqlState} y SaveChanges no falló.");
    }

    private static async Task<(int Status, string Body)> InvokeAsync(Exception exception)
    {
        var context = new DefaultHttpContext();
        context.Request.Path = "/api/test";
        context.Response.Body = new MemoryStream();

        var middleware = new ExceptionHandlingMiddleware(
            _ => throw exception,
            NullLogger<ExceptionHandlingMiddleware>.Instance);

        await middleware.InvokeAsync(context);

        context.Response.Body.Position = 0;
        using var reader = new StreamReader(context.Response.Body);
        return (context.Response.StatusCode, await reader.ReadToEndAsync());
    }

    private static void AssertNoTechnicalLeak(string body)
    {
        Assert.DoesNotContain("23505", body, StringComparison.Ordinal);
        Assert.DoesNotContain("23503", body, StringComparison.Ordinal);
        Assert.DoesNotContain("23514", body, StringComparison.Ordinal);
        Assert.DoesNotContain("22001", body, StringComparison.Ordinal);
        Assert.DoesNotContain("constraint", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("duplicate key", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("insert into", body, StringComparison.OrdinalIgnoreCase);

        using var document = JsonDocument.Parse(body);
        Assert.True(document.RootElement.TryGetProperty("detail", out var detail));
        Assert.DoesNotContain("assets_", detail.GetString()!, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("roles_", detail.GetString()!, StringComparison.OrdinalIgnoreCase);
    }
}
