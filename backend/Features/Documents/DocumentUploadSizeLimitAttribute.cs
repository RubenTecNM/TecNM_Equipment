using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace TecnmInventario.Api.Features.Documents;

/// <summary>
/// ST4: rechaza con 413 y un mensaje funcional una subida cuyo Content-Length
/// supera <see cref="DocumentUploadLimits.MaxRequestBodyBytes"/>, ANTES del
/// model binding y sin leer el cuerpo. Sin este filtro, [RequestSizeLimit]
/// corta la lectura del formulario y MVC lo convierte en un 400 técnico
/// ("Failed to read the request form...") dentro del formato de validación.
/// [RequestSizeLimit] se mantiene como defensa para cuerpos sin Content-Length.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class DocumentUploadSizeLimitAttribute : Attribute, IResourceFilter
{
    public void OnResourceExecuting(ResourceExecutingContext context)
    {
        var request = context.HttpContext.Request;
        if (request.ContentLength is not { } length || length <= DocumentUploadLimits.MaxRequestBodyBytes)
        {
            return;
        }

        context.Result = new ObjectResult(new ProblemDetails
        {
            Status = StatusCodes.Status413PayloadTooLarge,
            Title = "Solicitud demasiado grande",
            Detail = "El archivo supera el tamaño máximo permitido.",
            Instance = request.Path
        })
        {
            StatusCode = StatusCodes.Status413PayloadTooLarge,
            ContentTypes = { "application/problem+json" }
        };
    }

    public void OnResourceExecuted(ResourceExecutedContext context)
    {
    }
}
