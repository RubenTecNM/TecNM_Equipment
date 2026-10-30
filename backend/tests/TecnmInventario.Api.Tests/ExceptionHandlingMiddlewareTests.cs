using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using TecnmInventario.Api.Common.Middleware;

namespace TecnmInventario.Api.Tests;

public class ExceptionHandlingMiddlewareTests
{
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

    [Theory]
    [InlineData("23505", 409)]
    [InlineData("23503", 400)]
    [InlineData("23514", 400)]
    [InlineData("22001", 400)]
    public async Task Postgres_integrity_errors_se_traducen_y_no_filtran_detalle(
        string sqlState, int expectedStatus)
    {
        const string constraint = "assets_inventory_code_key";
        var postgres = new PostgresException(
            $"duplicate key value violates unique constraint \"{constraint}\"",
            "ERROR",
            "ERROR",
            sqlState,
            detail: "Key (inventory_code)=(X) already exists.",
            constraintName: constraint);
        var exception = new DbUpdateException("INSERT INTO assets ... failed", postgres);

        var (status, body) = await InvokeAsync(exception);

        Assert.Equal(expectedStatus, status);
        Assert.DoesNotContain(constraint, body);
        Assert.DoesNotContain("INSERT INTO", body);
        Assert.DoesNotContain("duplicate key", body, StringComparison.OrdinalIgnoreCase);

        using var problem = JsonDocument.Parse(body);
        Assert.Equal(expectedStatus, problem.RootElement.GetProperty("status").GetInt32());
        Assert.Equal("/api/test", problem.RootElement.GetProperty("instance").GetString());
    }

    [Fact]
    public async Task P0001_conserva_el_mensaje_de_negocio_del_trigger()
    {
        var postgres = new PostgresException(
            "El documento no pertenece a un propietario válido.",
            "ERROR",
            "ERROR",
            "P0001");
        var exception = new DbUpdateException("update failed", postgres);

        var (status, body) = await InvokeAsync(exception);

        Assert.Equal(400, status);
        Assert.Contains("El documento no pertenece", body);
    }

    [Fact]
    public async Task DbUpdateConcurrencyException_se_resuelve_antes_que_DbUpdateException()
    {
        var exception = new DbUpdateConcurrencyException(
            "The database operation was expected to affect 1 row(s), but actually affected 0 row(s).");

        var (status, body) = await InvokeAsync(exception);

        Assert.Equal(409, status);
        Assert.DoesNotContain("affected 0 row", body, StringComparison.OrdinalIgnoreCase);

        using var problem = JsonDocument.Parse(body);
        Assert.Equal("Conflicto de concurrencia", problem.RootElement.GetProperty("title").GetString());
    }

    [Fact]
    public async Task SqlState_desconocido_no_expone_el_detalle_tecnico()
    {
        var postgres = new PostgresException(
            "some internal postgres detail",
            "ERROR",
            "ERROR",
            "XX000",
            constraintName: "internal_constraint");
        var exception = new DbUpdateException("update failed", postgres);

        var (status, body) = await InvokeAsync(exception);

        Assert.Equal(500, status);
        Assert.DoesNotContain("internal_constraint", body);
        Assert.DoesNotContain("some internal postgres detail", body);
    }
}
