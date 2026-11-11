using Microsoft.EntityFrameworkCore;
using TecnmInventario.Api.Data;
using TecnmInventario.Api.Features.Auth.Register;

namespace TecnmInventario.Api.Tests;

/// <summary>
/// Subcheckpoint 5.4 (U2, U3): usernames y correos ASCII en Register. Un
/// validador inválido se convierte en 400 por ValidationBehavior (cubierto en
/// ValidationBehaviorTests); aquí se prueba la regla.
/// </summary>
public class Fase54ValidacionesTests
{
    [Theory]
    [InlineData("josé")]
    [InlineData("peña")]
    [InlineData("juan perez")]
    public async Task Register_UsernameConAcentoOEnie_Rechaza400(string username)
    {
        using var context = NewContext();

        var result = await new RegisterValidator(context).ValidateAsync(NewRegister(username, "sd54-a@test.local"));

        var error = Assert.Single(result.Errors, e => e.PropertyName == nameof(RegisterCommand.Username));
        Assert.Equal(
            "El usuario solo puede contener letras sin acentos, números, punto, guion y guion bajo.",
            error.ErrorMessage);
    }

    [Fact]
    public async Task Register_EmailNoAscii_Rechaza400()
    {
        using var context = NewContext();

        var result = await new RegisterValidator(context).ValidateAsync(NewRegister("sd54_user", "josé@test.local"));

        Assert.Contains(result.Errors, e =>
            e.PropertyName == nameof(RegisterCommand.Email) && e.ErrorMessage.Contains("ASCII"));
    }

    // Guardia: la regla no puede rechazar lo que el login ya acepta. Las
    // mayúsculas se normalizan antes de validar el formato.
    [Theory]
    [InlineData("j.perez-01_a")]
    [InlineData("J.Perez-01_A")]
    public async Task Register_UsernameValido_Acepta(string username)
    {
        using var context = NewContext();

        var result = await new RegisterValidator(context).ValidateAsync(NewRegister(username, "sd54-b@test.local"));

        Assert.DoesNotContain(result.Errors, e => e.PropertyName == nameof(RegisterCommand.Username));
        Assert.DoesNotContain(result.Errors, e => e.PropertyName == nameof(RegisterCommand.Email));
    }

    private static TecnmInventarioDbContext NewContext()
        => new(new DbContextOptionsBuilder<TecnmInventarioDbContext>()
            .UseInMemoryDatabase($"tecnm-5-4-{Guid.NewGuid()}")
            .Options);

    private static RegisterCommand NewRegister(string username, string email)
        => new(username, email, "ClaveSegura123!", "Nombre", "Apellido", "ADMIN");
}
