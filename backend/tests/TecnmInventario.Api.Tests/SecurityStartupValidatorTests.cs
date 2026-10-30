using TecnmInventario.Api.Common.Security;

namespace TecnmInventario.Api.Tests;

/// <summary>
/// La configuración insegura debe fallar al arrancar: secreto JWT corto o de
/// ejemplo, y cookies sin Secure fuera de Development.
/// </summary>
public class SecurityStartupValidatorTests
{
    private static string ValidSecret => Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N");

    [Fact]
    public void Secreto_valido_no_lanza()
    {
        SecurityStartupValidator.Validate(ValidSecret, isDevelopment: false, cookieSecure: true);
    }

    [Fact]
    public void Secreto_corto_lanza_con_mensaje_claro()
    {
        var exception = Assert.Throws<InvalidOperationException>(
            () => SecurityStartupValidator.Validate("corto", isDevelopment: true, cookieSecure: false));

        Assert.Contains("demasiado corto", exception.Message);
        Assert.Contains("openssl rand", exception.Message);
    }

    [Theory]
    [InlineData("<clave-larga-y-secreta>")]
    [InlineData("changeme-changeme-changeme-changeme")]
    [InlineData("your-secret-your-secret-your-secret")]
    [InlineData("CAMBIAR-ESTA-CLAVE-POR-UNA-REAL-12345")]
    [InlineData("placeholder-placeholder-placeholder")]
    public void Secreto_placeholder_lanza(string secret)
    {
        var exception = Assert.Throws<InvalidOperationException>(
            () => SecurityStartupValidator.Validate(secret, isDevelopment: true, cookieSecure: false));

        Assert.Contains("ejemplo", exception.Message);
    }

    [Fact]
    public void Secreto_ausente_lanza()
    {
        Assert.Throws<InvalidOperationException>(
            () => SecurityStartupValidator.Validate(null, isDevelopment: true, cookieSecure: false));
        Assert.Throws<InvalidOperationException>(
            () => SecurityStartupValidator.Validate("   ", isDevelopment: true, cookieSecure: false));
    }

    [Fact]
    public void CookieSecure_false_en_Production_lanza()
    {
        var exception = Assert.Throws<InvalidOperationException>(
            () => SecurityStartupValidator.Validate(ValidSecret, isDevelopment: false, cookieSecure: false));

        Assert.Contains("Auth:CookieSecure", exception.Message);
        Assert.Contains("HTTPS", exception.Message);
    }

    [Fact]
    public void CookieSecure_false_en_Development_es_aceptable()
    {
        SecurityStartupValidator.Validate(ValidSecret, isDevelopment: true, cookieSecure: false);
    }

    [Fact]
    public void CookieSecure_true_en_Production_es_aceptable()
    {
        SecurityStartupValidator.Validate(ValidSecret, isDevelopment: false, cookieSecure: true);
    }
}
