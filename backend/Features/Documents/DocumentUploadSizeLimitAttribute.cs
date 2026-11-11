using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace TecnmInventario.Api.Features.Documents;

/// <summary>
/// ST4/R3a: responde 413 con un ProblemDetails en español cuando la subida
/// supera <see cref="DocumentUploadLimits.MaxRequestBodyBytes"/>, ANTES del
/// model binding:
/// <list type="bullet">
///   <item>Con Content-Length: se compara la cabecera y no se lee el cuerpo.</item>
///   <item>Sin Content-Length (chunked): se lee el formulario aquí, con el
///   límite de [RequestSizeLimit] ya aplicado, y se captura el
///   BadHttpRequestException con StatusCode 413 que lanza Kestrel.</item>
/// </list>
/// Sin este filtro MVC envuelve ese fallo de lectura en un error de modelo y
/// responde un 400 técnico en inglés ("Failed to read the request form...");
/// el BadHttpRequestException nunca llegaría al middleware de excepciones.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class DocumentUploadSizeLimitAttribute : Attribute, IAsyncResourceFilter
{
    // [RequestSizeLimit] tiene Order = 900 y fija el límite de Kestrel en su
    // OnResourceExecuting: este filtro debe ejecutarse DESPUÉS.
    public int Order => 1000;

    public async Task OnResourceExecutionAsync(ResourceExecutingContext context, ResourceExecutionDelegate next)
    {
        var request = context.HttpContext.Request;

        if (request.ContentLength is { } length)
        {
            if (length > DocumentUploadLimits.MaxRequestBodyBytes)
            {
                context.Result = PayloadTooLarge(request);
                return;
            }
        }
        else if (request.HasFormContentType)
        {
            try
            {
                await request.ReadFormAsync(context.HttpContext.RequestAborted);
            }
            catch (BadHttpRequestException ex) when (ex.StatusCode == StatusCodes.Status413PayloadTooLarge)
            {
                context.Result = PayloadTooLarge(request);
                return;
            }
            catch (Exception)
            {
                // Formulario mal formado o cliente desconectado: no es asunto
                // de este filtro. La lectura queda registrada en la petición y
                // MVC la trata al enlazar el modelo, igual que antes.
            }
        }

        await next();
    }

    private static ObjectResult PayloadTooLarge(HttpRequest request) => new(new ProblemDetails
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
