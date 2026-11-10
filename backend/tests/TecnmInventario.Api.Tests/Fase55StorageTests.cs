using Microsoft.Extensions.Options;
using TecnmInventario.Api.Common.Errors;
using TecnmInventario.Api.Common.Storage;

namespace TecnmInventario.Api.Tests;

/// <summary>
/// Subcheckpoint 5.5 (ST1): el servicio de almacenamiento por sí solo, con
/// carpetas temporales reales (nunca la real).
/// </summary>
public class Fase55StorageTests
{
    // Raíz ".../storage" y una carpeta HERMANA ".../storage-malo": su ruta
    // empieza por el texto de la raíz, pero no está dentro de ella.
    [Fact]
    public async Task ResolveSafePath_RaizConNombreParecido_Rechaza()
    {
        var parent = Path.Combine(Path.GetTempPath(), $"tecnm-5-5-unit-{Guid.NewGuid():N}");
        var root = Path.Combine(parent, "storage");
        var sibling = Path.Combine(parent, "storage-malo");
        Directory.CreateDirectory(root);
        Directory.CreateDirectory(sibling);

        try
        {
            await File.WriteAllTextAsync(Path.Combine(sibling, "x.pdf"), "FUERA-DE-LA-RAIZ");
            var storage = new LocalDiskFileStorageService(
                Options.Create(new FileStorageOptions { LocalPath = root }));

            // OpenReadAsync pasa por ResolveSafePath: lo que no se rechaza se abre.
            var ex = await Assert.ThrowsAnyAsync<Exception>(
                () => storage.OpenReadAsync(Path.Combine("..", "storage-malo", "x.pdf"), CancellationToken.None));

            // Rechazo controlado (400), nunca un 500 ni la ruta en el mensaje.
            Assert.IsAssignableFrom<BusinessRuleViolationException>(ex);
            Assert.DoesNotContain(parent, ex.Message);
        }
        finally
        {
            Directory.Delete(parent, recursive: true);
        }
    }
}
