using TecnmInventario.Api.Common.Errors;

namespace TecnmInventario.Api.Common.Storage;

/// <summary>
/// La clave de almacenamiento no resuelve a un archivo dentro de la raíz
/// (traversal, carpeta hermana con nombre parecido o enlace simbólico).
/// Es una regla de negocio (400): el mensaje es fijo y NUNCA lleva la ruta.
/// Quien la recibe de la BD (descarga) la convierte en 404 y registra el
/// document_id.
/// </summary>
public sealed class StoragePathException : BusinessRuleViolationException
{
    public const string DefaultMessage = "Ruta de almacenamiento inválida.";

    public StoragePathException() : base(DefaultMessage)
    {
    }
}
