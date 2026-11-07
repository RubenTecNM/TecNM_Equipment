namespace TecnmInventario.Api.Common.Errors;

/// <summary>
/// Se lanza cuando se intenta borrar o reemplazar un registro que tiene
/// dependencias activas -- ej. borrar un proveedor con adquisiciones, o una
/// ubicación con asignaciones vigentes. HTTP 409 Conflict (en uso ≠ mal formado).
/// </summary>
public class ReferenceInUseException : Exception
{
    public ReferenceInUseException(string message) : base(message)
    {
    }
}
