using Microsoft.Extensions.Options;
using TecnmInventario.Api.Common.Storage;

namespace TecnmInventario.Api.Tests;

/// <summary>
/// La ruta de almacenamiento es relativa en la configuración (nunca una ruta
/// absoluta con el usuario de la máquina). Se resuelve contra el ContentRoot y
/// el servicio de disco local debe funcionar con la ruta resultante.
/// </summary>
public class StoragePathResolverTests
{
    [Fact]
    public void Ruta_relativa_se_resuelve_contra_el_ContentRoot()
    {
        var contentRoot = Path.Combine(Path.GetTempPath(), "tecnm-content-root");

        var resolved = StoragePathResolver.Resolve("storage/documents", contentRoot);

        Assert.Equal(
            Path.GetFullPath(Path.Combine(contentRoot, "storage/documents")),
            resolved);
        Assert.True(Path.IsPathRooted(resolved));
    }

    [Fact]
    public void Ruta_absoluta_se_respeta()
    {
        var absolute = Path.Combine(Path.GetTempPath(), "tecnm-storage-absoluto");

        var resolved = StoragePathResolver.Resolve(absolute, "/content-root-irrelevante");

        Assert.Equal(Path.GetFullPath(absolute), resolved);
    }

    [Fact]
    public void Ruta_vacia_falla_con_mensaje_claro()
    {
        var exception = Assert.Throws<InvalidOperationException>(
            () => StoragePathResolver.Resolve("   ", Path.GetTempPath()));

        Assert.Contains("Storage:LocalPath", exception.Message);
    }

    [Fact]
    public async Task LocalDiskFileStorageService_funciona_con_la_ruta_relativa_resuelta()
    {
        var contentRoot = Path.Combine(
            Path.GetTempPath(), $"tecnm-storage-test-{Guid.NewGuid():N}");

        try
        {
            var resolved = StoragePathResolver.Resolve("storage/documents", contentRoot);
            var storage = new LocalDiskFileStorageService(
                Options.Create(new FileStorageOptions { LocalPath = resolved }));

            await using var content = new MemoryStream("contenido de prueba"u8.ToArray());
            var key = await storage.SaveAsync(content, "evidencia.txt", CancellationToken.None);

            Assert.True(File.Exists(Path.Combine(resolved, key)));

            await using (var read = await storage.OpenReadAsync(key, CancellationToken.None))
            using (var reader = new StreamReader(read))
            {
                Assert.Equal("contenido de prueba", await reader.ReadToEndAsync());
            }

            await storage.DeleteAsync(key, CancellationToken.None);
            Assert.False(File.Exists(Path.Combine(resolved, key)));
        }
        finally
        {
            if (Directory.Exists(contentRoot))
            {
                Directory.Delete(contentRoot, recursive: true);
            }
        }
    }
}
