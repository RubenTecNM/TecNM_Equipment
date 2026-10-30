using Microsoft.Extensions.Options;

namespace TecnmInventario.Api.Common.Storage;

public sealed class LocalDiskFileStorageService : IFileStorageService
{
    private readonly string _rootPath;

    public LocalDiskFileStorageService(IOptions<FileStorageOptions> options)
    {
        _rootPath = options.Value.LocalPath;
        Directory.CreateDirectory(_rootPath);
    }

    public async Task<string> SaveAsync(Stream content, string originalFileName, CancellationToken cancellationToken)
    {
        var extension = Path.GetExtension(originalFileName);
        var storageKey = $"{Guid.NewGuid():N}{extension}";
        var fullPath = Path.Combine(_rootPath, storageKey);

        await using var fileStream = File.Create(fullPath);
        await content.CopyToAsync(fileStream, cancellationToken);

        return storageKey;
    }

    public Task<Stream> OpenReadAsync(string storageKey, CancellationToken cancellationToken)
    {
        var fullPath = ResolveSafePath(storageKey);

        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException("El archivo solicitado ya no existe en el almacenamiento.", storageKey);
        }

        Stream stream = File.OpenRead(fullPath);
        return Task.FromResult(stream);
    }

    public Task DeleteAsync(string storageKey, CancellationToken cancellationToken)
    {
        var fullPath = ResolveSafePath(storageKey);

        if (File.Exists(fullPath))
        {
            File.Delete(fullPath);
        }

        return Task.CompletedTask;
    }

    private string ResolveSafePath(string storageKey)
    {
        var fullPath = Path.GetFullPath(Path.Combine(_rootPath, storageKey));
        var normalizedRoot = Path.GetFullPath(_rootPath);

        if (!fullPath.StartsWith(normalizedRoot, StringComparison.Ordinal))
        {
            throw new UnauthorizedAccessException("Ruta de almacenamiento inválida.");
        }

        return fullPath;
    }
}
