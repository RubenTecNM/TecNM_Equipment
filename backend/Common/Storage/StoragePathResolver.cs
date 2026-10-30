namespace TecnmInventario.Api.Common.Storage;

/// <summary>
/// Resuelve <c>Storage:LocalPath</c> contra la raíz de contenido de la API.
/// Una ruta relativa (la configuración recomendada, p. ej. <c>storage/documents</c>)
/// se resuelve contra <c>ContentRootPath</c>; una ruta absoluta se respeta tal cual.
/// </summary>
public static class StoragePathResolver
{
    public static string Resolve(string? configuredPath, string contentRootPath)
    {
        if (string.IsNullOrWhiteSpace(configuredPath))
        {
            throw new InvalidOperationException(
                "Falta configurar Storage:LocalPath (por ejemplo 'storage/documents').");
        }

        if (string.IsNullOrWhiteSpace(contentRootPath))
        {
            throw new InvalidOperationException(
                "No se pudo resolver Storage:LocalPath: ContentRootPath está vacío.");
        }

        var path = configuredPath.Trim();

        return Path.IsPathRooted(path)
            ? Path.GetFullPath(path)
            : Path.GetFullPath(Path.Combine(contentRootPath, path));
    }
}
