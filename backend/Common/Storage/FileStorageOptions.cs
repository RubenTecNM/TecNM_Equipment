namespace TecnmInventario.Api.Common.Storage;

public sealed class FileStorageOptions
{
    public const string SectionName = "Storage";

    /// <summary>
    /// Carpeta raíz en disco donde se guardan los archivos (implementación local).
    /// Debe estar fuera de wwwroot / cualquier carpeta servida públicamente.
    /// Puede ser relativa: se resuelve contra el ContentRoot de la API
    /// (ver <see cref="StoragePathResolver"/>). El valor por defecto permite
    /// arrancar sin configuración explícita.
    /// </summary>
    public string LocalPath { get; set; } = "storage/documents";

    /// <summary>
    /// Tamaño máximo de un archivo subido, en bytes (5 MB por defecto). El CHECK
    /// de documents.size_bytes en la BD también topa en 5242880: subir este valor
    /// exige cambiar ese CHECK, y el límite de la petición del endpoint
    /// (DocumentUploadLimits) está calculado sobre 5 MB.
    /// </summary>
    public long MaxFileSizeBytes { get; set; } = 5L * 1024 * 1024;
}
