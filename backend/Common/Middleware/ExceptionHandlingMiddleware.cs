using System.Net;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using ValidationException = TecnmInventario.Api.Common.Errors.ValidationException;

namespace TecnmInventario.Api.Common.Middleware;

public class ExceptionHandlingMiddleware
{
    private const string UniqueViolation = "23505";

    // Índices únicos con mensaje funcional propio (Lote 5.4): el alta de un
    // usuario que solo difiere en mayúsculas llega aquí cuando dos altas
    // compiten y el validador no alcanzó a verla.
    private static readonly IReadOnlyDictionary<string, string> UniqueIndexMessages =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["ux_app_users_username_lower"] = "Ya existe un usuario con ese nombre.",
            ["ux_app_users_email_lower"] = "Ya existe un usuario con ese correo.",
            // M.2: respaldo del "un solo mantenimiento EN_CURSO por activo" (el handler
            // lo comprueba antes, con el activo bloqueado; esto cubre una carrera real).
            ["ux_maintenance_one_in_progress"] = "El activo ya tiene un mantenimiento en curso."
        };
    private const string ForeignKeyViolation = "23503";
    private const string CheckViolation = "23514";
    private const string StringDataRightTruncation = "22001";
    private const string RaiseException = "P0001";

    // R1: cuerpo único de todo 403 por permiso. El detalle (rol, tipo, permiso)
    // va solo al log: el cliente no debe aprender cómo está armada la autorización.
    private const string ForbiddenMessage = "No tienes permiso para realizar esta acción.";

    // R2: cualquier P0001 que no esté en la lista blanca sale con este texto.
    private const string GenericRaiseMessage = "La operación no cumple las reglas del sistema.";

    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;
    private readonly FunctionalRaiseMessages _functionalRaiseMessages;

    // El tercer parámetro es opcional: en producción se usa la lista vacía por
    // defecto; los tests pueden registrar otra en el contenedor.
    public ExceptionHandlingMiddleware(
        RequestDelegate next,
        ILogger<ExceptionHandlingMiddleware> logger,
        FunctionalRaiseMessages? functionalRaiseMessages = null)
    {
        _next = next;
        _logger = logger;
        _functionalRaiseMessages = functionalRaiseMessages ?? FunctionalRaiseMessages.Default;
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
        catch (Common.Errors.ReferenceInUseException ex)
        {
            // Recurso referenciado por otras filas activas: borrar o reemplazar
            // violaría la integridad del historial. HTTP 409 (en uso ≠ mal formado).
            _logger.LogWarning("Recurso en uso en {Path}: {Reason}", context.Request.Path, ex.Message);
            await WriteProblemDetailsAsync(
                context,
                HttpStatusCode.Conflict,
                "Recurso en uso",
                ex.Message);
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
        catch (Common.Security.TooManyRequestsException ex)
        {
            // Bloqueo por intentos fallidos (LoginAttemptLimiter): mismo 429,
            // Retry-After y mensaje que el límite por IP del RateLimiter.
            _logger.LogWarning("Demasiados intentos en {Path}", context.Request.Path);
            context.Response.Headers.RetryAfter =
                Common.Security.PerimeterSecurityExtensions.RetryAfterSeconds(ex.RetryAfter);
            await WriteProblemDetailsAsync(
                context,
                HttpStatusCode.TooManyRequests,
                "Demasiadas solicitudes",
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
                ForbiddenMessage);
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
                "Error de integridad en {Path}. SqlState={SqlState} Constraint={Constraint} Detail={Detail} MessageText={MessageText}",
                context.Request.Path,
                pgEx.SqlState,
                pgEx.ConstraintName,
                pgEx.Detail,
                pgEx.MessageText);

            var (status, title, detail) = pgEx.SqlState switch
            {
                UniqueViolation => (
                    HttpStatusCode.Conflict,
                    "Conflicto",
                    pgEx.ConstraintName is { } constraint && UniqueIndexMessages.TryGetValue(constraint, out var message)
                        ? message
                        : "Ya existe un registro con esos datos."),
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
                // R2: el texto de un RAISE EXCEPTION solo sale tal cual si está
                // en la lista blanca (coincidencia exacta); el original queda en el log.
                RaiseException => (
                    HttpStatusCode.BadRequest,
                    "Error de validación",
                    _functionalRaiseMessages.Contains(pgEx.MessageText)
                        ? pgEx.MessageText
                        : GenericRaiseMessage),
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
        // S1: un error inesperado (500) no debe dejar cookies a medio escribir
        // (p. ej. la de un refresh token cuya transacción no se confirmó). Los
        // 400/401/403 de negocio conservan sus Set-Cookie: algunos borran la
        // sesión a propósito (refresh revocado, cuenta inactiva).
        if (statusCode == HttpStatusCode.InternalServerError)
        {
            context.Response.Headers.Remove(Microsoft.Net.Http.Headers.HeaderNames.SetCookie);
        }

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
