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
    /// internamente: GUID + <paramref name="extension"/>. El nombre del cliente
    /// no participa en la ruta (evita colisiones y path traversal); la extensión
    /// la decide quien llama a partir del TIPO DETECTADO (p. ej. ".pdf").
    /// </summary>
    Task<string> SaveAsync(Stream content, string extension, CancellationToken cancellationToken);

    /// <summary>
    /// Abre el archivo identificado por su storage_key para lectura/streaming.
    /// </summary>
    Task<Stream> OpenReadAsync(string storageKey, CancellationToken cancellationToken);

    /// <summary>
    /// Elimina el archivo físico identificado por su storage_key.
    /// SOLO para compensar una subida cuya transacción no se confirmó (el
    /// registro nunca existió): un documento ya registrado se borra de forma
    /// lógica y su archivo se conserva (regla 20).
    /// No falla si el archivo ya no existe (idempotente).
    /// </summary>
    Task DeleteAsync(string storageKey, CancellationToken cancellationToken);
}
