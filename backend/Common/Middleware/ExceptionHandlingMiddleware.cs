using System.Net;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using ValidationException = TecnmInventario.Api.Common.Errors.ValidationException;

namespace TecnmInventario.Api.Common.Middleware;

public class ExceptionHandlingMiddleware
{
    private const string UniqueViolation = "23505";
    private const string ForeignKeyViolation = "23503";
    private const string CheckViolation = "23514";
    private const string StringDataRightTruncation = "22001";
    private const string RaiseException = "P0001";

    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (ValidationException ex)
        {
            _logger.LogWarning("Error de validación en {Path}: {Reason}", context.Request.Path, ex.Message);
            await WriteProblemDetailsAsync(
                context,
                HttpStatusCode.BadRequest,
                "Error de validación",
                ex.Message,
                ex.Errors);
        }
        catch (Common.Errors.BusinessRuleViolationException ex)
        {
            // Regla de negocio dependiente del estado actual de los datos
            // (no un error de forma detectable por FluentValidation antes
            // de llegar al Handler) -- ej. borrar una ubicación con hijos.
            _logger.LogWarning("Regla de negocio violada en {Path}: {Reason}", context.Request.Path, ex.Message);
            await WriteProblemDetailsAsync(
                context,
                HttpStatusCode.BadRequest,
                "Regla de negocio violada",
                ex.Message);
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning("Intento de acceso no autorizado en {Path}: {Reason}", context.Request.Path, ex.Message);
            await WriteProblemDetailsAsync(
                context,
                HttpStatusCode.Unauthorized,
                "No autorizado",
                ex.Message);
        }
        catch (Common.Errors.ForbiddenAccessException ex)
        {
            _logger.LogWarning("Acceso prohibido en {Path}: {Reason}", context.Request.Path, ex.Message);
            await WriteProblemDetailsAsync(
                context,
                HttpStatusCode.Forbidden,
                "Acceso prohibido",
                ex.Message);
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning("Recurso no encontrado en {Path}: {Reason}", context.Request.Path, ex.Message);
            await WriteProblemDetailsAsync(
                context,
                HttpStatusCode.NotFound,
                "Recurso no encontrado",
                ex.Message);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            // El registro fue modificado o borrado por otro usuario entre que
            // el cliente lo leyó y envió su cambio (WHERE version=X afectó 0 filas).
            _logger.LogWarning("Conflicto de concurrencia optimista en {Path}: {Reason}", context.Request.Path, ex.Message);
            await WriteProblemDetailsAsync(
                context,
                HttpStatusCode.Conflict,
                "Conflicto de concurrencia",
                "El recurso fue modificado por otro usuario mientras tanto. Vuelve a cargarlo e intenta de nuevo.");
        }
        catch (DbUpdateException ex) when (FindPostgresException(ex) is { } pgEx)
        {
            // EF Core envuelve el PostgresException real dentro de DbUpdateException.
            // Al cliente solo se le da un mensaje funcional: nunca el nombre del
            // constraint, el SQL ni el detalle técnico.
            _logger.LogWarning(
                ex,
                "Error de integridad en {Path}. SqlState={SqlState} Constraint={Constraint} Detail={Detail}",
                context.Request.Path,
                pgEx.SqlState,
                pgEx.ConstraintName,
                pgEx.Detail);

            var (status, title, detail) = pgEx.SqlState switch
            {
                UniqueViolation => (
                    HttpStatusCode.Conflict,
                    "Conflicto",
                    "Ya existe un registro con esos datos."),
                ForeignKeyViolation => (
                    HttpStatusCode.BadRequest,
                    "Datos no válidos",
                    "La operación hace referencia a un registro que no existe o que está en uso."),
                CheckViolation => (
                    HttpStatusCode.BadRequest,
                    "Datos no válidos",
                    "Los datos no cumplen una regla de integridad de la base de datos."),
                StringDataRightTruncation => (
                    HttpStatusCode.BadRequest,
                    "Datos no válidos",
                    "Uno de los valores excede la longitud permitida."),
                RaiseException => (
                    HttpStatusCode.BadRequest,
                    "Error de validación",
                    pgEx.MessageText),
                _ => (
                    HttpStatusCode.InternalServerError,
                    "Error interno del servidor",
                    "Ocurrió un error inesperado. Contacte al administrador si el problema persiste.")
            };

            await WriteProblemDetailsAsync(context, status, title, detail);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error no controlado en {Path}", context.Request.Path);
            await WriteProblemDetailsAsync(
                context,
                HttpStatusCode.InternalServerError,
                "Error interno del servidor",
                "Ocurrió un error inesperado. Contacte al administrador si el problema persiste.");
        }
    }

    // Npgsql puede anidar el PostgresException en más de un nivel de
    // InnerException según el proveedor; se busca en toda la cadena.
    private static PostgresException? FindPostgresException(Exception exception)
    {
        for (var current = exception.InnerException; current is not null; current = current.InnerException)
        {
            if (current is PostgresException postgresException)
            {
                return postgresException;
            }
        }

        return null;
    }

    private static async Task WriteProblemDetailsAsync(
        HttpContext context,
        HttpStatusCode statusCode,
        string title,
        string detail,
        IDictionary<string, string[]>? errors = null)
    {
        context.Response.ContentType = "application/problem+json";
        context.Response.StatusCode = (int)statusCode;

        var problemDetails = new ProblemDetails
        {
            Status = (int)statusCode,
            Title = title,
            Detail = detail,
            Instance = context.Request.Path
        };

        if (errors is not null)
        {
            problemDetails.Extensions["errors"] = errors;
        }

        await context.Response.WriteAsJsonAsync(problemDetails);
    }
}
