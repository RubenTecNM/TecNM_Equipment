using System.Net;

namespace TecnmInventario.Api.Tests.Integration;

/// <summary>
/// La fábrica arranca la API real con la configuración de tests: si Program.cs
/// no viera los UseSetting antes de Build(), fallaría por falta de Jwt:Secret
/// o por AllowedHosts. No necesita base de datos.
/// </summary>
public sealed class ApiFactorySmokeTests
{
    [Fact]
    public async Task ApiFactory_ArrancaLaApiRealEnTesting()
    {
        await using var factory = new ApiFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/auth/providers");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
