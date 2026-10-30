namespace TecnmInventario.Api.Common.Storage;

/// <summary>
/// Abstrae dónde y cómo se guardan los archivos físicos de evidencias/documentos.
/// La implementación concreta (disco local, S3, Azure Blob, etc.) es intercambiable
/// sin tocar los Handlers que la consumen.
/// </summary>
public interface IFileStorageService
{
    /// <summary>
    /// Guarda el contenido del stream y devuelve la storage_key generada
    /// internamente (nunca el nombre original del archivo, para evitar
    /// colisiones y path traversal).
    /// </summary>
    Task<string> SaveAsync(Stream content, string originalFileName, CancellationToken cancellationToken);

    /// <summary>
    /// Abre el archivo identificado por su storage_key para lectura/streaming.
    /// </summary>
    Task<Stream> OpenReadAsync(string storageKey, CancellationToken cancellationToken);

    /// <summary>
    /// Elimina el archivo físico identificado por su storage_key.
    /// No falla si el archivo ya no existe (idempotente).
    /// </summary>
    Task DeleteAsync(string storageKey, CancellationToken cancellationToken);
}
