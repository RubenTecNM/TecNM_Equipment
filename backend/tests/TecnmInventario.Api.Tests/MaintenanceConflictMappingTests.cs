using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using TecnmInventario.Api.Common.Middleware;

namespace TecnmInventario.Api.Tests;

/// <summary>
/// M.2: el índice único parcial que respalda "un solo mantenimiento EN_CURSO por
/// activo" se traduce a 409 con un mensaje funcional (mecanismo del 5.4).
/// </summary>
public class MaintenanceConflictMappingTests
{
    [Fact]
    public async Task Violacion23505_DeUxMaintenanceOneInProgress_Devuelve409ConMensajeFuncional()
    {
        var postgres = new PostgresException(
            "duplicate key value violates unique constraint \"ux_maintenance_one_in_progress\"",
            "ERROR", "ERROR", "23505",
            constraintName: "ux_maintenance_one_in_progress");
        var context = new DefaultHttpContext();
        context.Request.Path = "/api/maintenance/start";
        context.Response.Body = new MemoryStream();
        var middleware = new ExceptionHandlingMiddleware(
            _ => throw new DbUpdateException("insert failed", postgres),
            NullLogger<ExceptionHandlingMiddleware>.Instance);

        await middleware.InvokeAsync(context);

        context.Response.Body.Position = 0;
        var body = await new StreamReader(context.Response.Body).ReadToEndAsync();
        Assert.Equal(409, context.Response.StatusCode);
        Assert.Equal("El activo ya tiene un mantenimiento en curso.",
            JsonDocument.Parse(body).RootElement.GetProperty("detail").GetString());
        Assert.DoesNotContain("ux_maintenance", body);
        Assert.DoesNotContain("duplicate key", body, StringComparison.OrdinalIgnoreCase);
    }
}
