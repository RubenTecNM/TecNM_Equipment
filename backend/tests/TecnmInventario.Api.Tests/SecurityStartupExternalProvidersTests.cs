using TecnmInventario.Api.Common.Security;

namespace TecnmInventario.Api.Tests;

/// <summary>
/// Subcheckpoint 5.1 (N6): fuera de Development, un proveedor federado
/// configurado de forma insegura impide arrancar. Un proveedor sin ClientId
/// no está configurado y no exige nada.
/// </summary>
public class SecurityStartupExternalProvidersTests
{
    private const string FakeTenantId = "00000000-0000-0000-0000-000000000051";

    [Theory]
    [InlineData("common")]
    [InlineData("organizations")]
    [InlineData("consumers")]
    [InlineData("Common")]
    public void Arranque_TenantCommonFueraDeDevelopment_Falla(string tenant)
    {
        var options = new AuthOptions
        {
            AllowedEmailDomains = ["ejemplo.edu.mx"],
            Entra = { TenantId = tenant, ClientId = "client-id-ficticio" }
        };

        var exception = Assert.Throws<InvalidOperationException>(
            () => SecurityStartupValidator.ValidateExternalProviders(isDevelopment: false, options));

        Assert.Contains("TenantId", exception.Message);
    }

    [Fact]
    public void Arranque_EntraSinDominiosPermitidosFueraDeDevelopment_Falla()
    {
        var options = new AuthOptions
        {
            Entra = { TenantId = FakeTenantId, ClientId = "client-id-ficticio" }
        };

        var exception = Assert.Throws<InvalidOperationException>(
            () => SecurityStartupValidator.ValidateExternalProviders(isDevelopment: false, options));

        Assert.Contains("AllowedEmailDomains", exception.Message);
    }

    [Fact]
    public void Arranque_GoogleSinHostedDomainFueraDeDevelopment_Falla()
    {
        var options = new AuthOptions
        {
            Google = { ClientId = "client-id-ficticio" }
        };

        var exception = Assert.Throws<InvalidOperationException>(
            () => SecurityStartupValidator.ValidateExternalProviders(isDevelopment: false, options));

        Assert.Contains("HostedDomain", exception.Message);
    }

    [Fact]
    public void Arranque_ProveedoresNoConfigurados_NoExigeNada()
    {
        // Sin ClientId en ningún proveedor: ni dominios ni HostedDomain son
        // obligatorios, y la API debe arrancar sin login externo.
        SecurityStartupValidator.ValidateExternalProviders(isDevelopment: false, new AuthOptions());
    }
}
