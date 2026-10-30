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
}
