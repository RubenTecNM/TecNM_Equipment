namespace TecnmInventario.Api.Common.Errors;

/// <summary>
/// Se lanza cuando una operación es válida en su forma (campos correctos)
/// pero viola una regla de negocio que depende del estado actual de los
/// datos -- ej. borrar una ubicación que todavía tiene sub-ubicaciones o
/// activos. Distinta de ValidationException (que es para errores de forma
/// detectados por FluentValidation antes de llegar al Handler); esta se
/// lanza desde dentro del Handler, donde ya se tiene el contexto completo.
/// </summary>
public class BusinessRuleViolationException : Exception
{
    public BusinessRuleViolationException(string message) : base(message)
    {
    }
}
