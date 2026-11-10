using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;

namespace TecnmInventario.Api.Common.Storage;

public sealed partial class LocalDiskFileStorageService : IFileStorageService
{
    // Extensiones que puede producir el tipo detectado (".pdf", ".jpg", ".png").
    [GeneratedRegex(@"^\.[a-z0-9]{1,8}$", RegexOptions.CultureInvariant)]
    private static partial Regex ExtensionFormat();

    private readonly string _rootPath;

    public LocalDiskFileStorageService(IOptions<FileStorageOptions> options)
    {
        _rootPath = options.Value.LocalPath;
        Directory.CreateDirectory(_rootPath);
    }

    public async Task<string> SaveAsync(Stream content, string extension, CancellationToken cancellationToken)
    {
        if (!ExtensionFormat().IsMatch(extension))
        {
            throw new ArgumentException("Extensión de almacenamiento no válida.", nameof(extension));
        }

        var storageKey = $"{Guid.NewGuid():N}{extension}";
        var fullPath = ResolveSafePath(_rootPath, storageKey);

        await using var fileStream = new FileStream(fullPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        await content.CopyToAsync(fileStream, cancellationToken);

        return storageKey;
    }

    public Task<Stream> OpenReadAsync(string storageKey, CancellationToken cancellationToken)
    {
        var fullPath = ResolveSafePath(_rootPath, storageKey);

        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException("El archivo solicitado ya no existe en el almacenamiento.");
        }

        Stream stream = File.OpenRead(fullPath);
        return Task.FromResult(stream);
    }

    public Task DeleteAsync(string storageKey, CancellationToken cancellationToken)
    {
        var fullPath = ResolveSafePath(_rootPath, storageKey);

        if (File.Exists(fullPath))
        {
            File.Delete(fullPath);
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// ST1: la ruta resuelta debe quedar DENTRO de la raíz (raíz + separador:
    /// ".../storage-malo" no está dentro de ".../storage") y ni el archivo ni
    /// ningún directorio intermedio bajo la raíz pueden ser enlaces simbólicos.
    /// La raíz misma puede ser un enlace: la decide quien despliega.
    /// </summary>
    /// <exception cref="StoragePathException">Siempre con mensaje fijo, sin la ruta.</exception>
    public static string ResolveSafePath(string rootPath, string storageKey)
    {
        var normalizedRoot = Path.GetFullPath(rootPath);
        var fullPath = Path.GetFullPath(Path.Combine(normalizedRoot, storageKey));
        var rootWithSeparator = normalizedRoot.EndsWith(Path.DirectorySeparatorChar)
            ? normalizedRoot
            : normalizedRoot + Path.DirectorySeparatorChar;

        if (!fullPath.StartsWith(rootWithSeparator, StringComparison.Ordinal))
        {
            throw new StoragePathException();
        }

        var current = normalizedRoot;
        foreach (var segment in fullPath[rootWithSeparator.Length..]
                     .Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries))
        {
            current = Path.Combine(current, segment);
            FileSystemInfo info = Directory.Exists(current) ? new DirectoryInfo(current) : new FileInfo(current);
            if (info.LinkTarget is not null)
            {
                throw new StoragePathException();
            }
        }

        return fullPath;
    }
}
