using Microsoft.EntityFrameworkCore;
using TecnmInventario.Api.Common.Security;
using TecnmInventario.Api.Data;
using TecnmInventario.Api.Features.Auth.Login;
using TecnmInventario.Api.Features.Auth.Register;

namespace TecnmInventario.Api.Tests;

/// <summary>
/// Subcheckpoint 5.2: AllowedHosts al arrancar (P4) y longitudes de login y
/// registro (P5). Un validador inválido se convierte en 400 por ValidationBehavior.
/// bcrypt solo usa los primeros 72 BYTES: se cuentan bytes UTF-8, no caracteres.
/// </summary>
public class Fase52ValidacionesTests
{
    [Theory]
    [InlineData("*")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    [InlineData("intranet.ejemplo.edu.mx;*")]
    public void Arranque_AllowedHostsAsteriscoFueraDeDevelopment_Falla(string? allowedHosts)
    {
        var exception = Assert.Throws<InvalidOperationException>(
            () => SecurityStartupValidator.ValidateAllowedHosts(isDevelopment: false, allowedHosts));

        Assert.Contains("AllowedHosts", exception.Message);
    }

    [Fact]
    public void Arranque_AllowedHostsExplicitoODevelopment_NoFalla()
    {
        SecurityStartupValidator.ValidateAllowedHosts(isDevelopment: false, "intranet.ejemplo.edu.mx;localhost");
        SecurityStartupValidator.ValidateAllowedHosts(isDevelopment: true, "*");
    }

    [Fact]
    public async Task Login_IdentificadorDemasiadoLargo_Rechaza400()
    {
        var validator = new LoginValidator();

        var atLimit = await validator.ValidateAsync(new LoginCommand(new string('u', 254), "x"));
        var overLimit = await validator.ValidateAsync(new LoginCommand(new string('u', 255), "x"));

        Assert.DoesNotContain(atLimit.Errors, e => e.PropertyName == nameof(LoginCommand.UsernameOrEmail));
        Assert.Contains(overLimit.Errors, e => e.PropertyName == nameof(LoginCommand.UsernameOrEmail));
    }

    [Fact]
    public async Task Login_PasswordDeMasDe72Bytes_Rechaza400()
    {
        var validator = new LoginValidator();

        // 'ñ' ocupa 2 bytes: 36 = 72 bytes (vale), 37 = 74 bytes con solo 37 caracteres.
        var atLimit = await validator.ValidateAsync(new LoginCommand("usuario", new string('ñ', 36)));
        var overLimit = await validator.ValidateAsync(new LoginCommand("usuario", new string('ñ', 37)));

        Assert.DoesNotContain(atLimit.Errors, e => e.PropertyName == nameof(LoginCommand.Password));
        Assert.Contains(overLimit.Errors, e => e.PropertyName == nameof(LoginCommand.Password));
    }

    [Fact]
    public async Task Register_PasswordDeMasDe72Bytes_Rechaza400()
    {
        using var context = new TecnmInventarioDbContext(
            new DbContextOptionsBuilder<TecnmInventarioDbContext>()
                .UseInMemoryDatabase($"tecnm-5-2-{Guid.NewGuid()}")
                .Options);
        var validator = new RegisterValidator(context);

        // '€' ocupa 3 bytes: 24 = 72 bytes (vale), 25 = 75 bytes con solo 25 caracteres.
        var atLimit = await validator.ValidateAsync(NewRegister(new string('€', 24)));
        var overLimit = await validator.ValidateAsync(NewRegister(new string('€', 25)));

        Assert.DoesNotContain(atLimit.Errors, e => e.PropertyName == nameof(RegisterCommand.Password));
        var error = Assert.Single(overLimit.Errors, e => e.PropertyName == nameof(RegisterCommand.Password));
        Assert.Contains("72 bytes", error.ErrorMessage);
    }

    private static RegisterCommand NewRegister(string password)
        => new("sd52_registro", "sd52-registro@test.local", password, "Nombre", "Apellido", "ADMIN");
}
