namespace TecnmInventario.Api.Common.Errors;

/// <summary>
/// El usuario está autenticado (se sabe quién es) pero no tiene el permiso
/// requerido para la operación. Se mapea a 403 Forbidden, a diferencia de
/// UnauthorizedAccessException (401, "no sé quién eres").
/// </summary>
public class ForbiddenAccessException : Exception
{
    public ForbiddenAccessException(string message) : base(message)
    {
    }
}
