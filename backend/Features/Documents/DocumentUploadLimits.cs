namespace TecnmInventario.Api.Features.Documents;

/// <summary>
/// ST4: límite de la PETICIÓN de subida = 5 MB del archivo + un margen de 64 KB
/// para el resto del formulario (ownerType, ownerId, description y las
/// cabeceras/fronteras multipart). Lo aplica el servidor (413) antes de leer
/// el cuerpo entero; el máximo del archivo en sí (Storage:MaxFileSizeBytes)
/// lo valida UploadDocumentValidator con un 400 y mensaje claro.
/// </summary>
public static class DocumentUploadLimits
{
    public const long MaxRequestBodyBytes = 5L * 1024 * 1024 + 64 * 1024;
}
