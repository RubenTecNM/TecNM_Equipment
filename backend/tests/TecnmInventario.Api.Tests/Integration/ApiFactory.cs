using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace TecnmInventario.Api.Tests.Integration;

/// <summary>
/// Arranca la API real (Program.cs) en memoria con TestServer.
/// <list type="bullet">
///   <item>Entorno "Testing": no se cargan user-secrets (solo Development lo hace)
///   y se aplican las validaciones de arranque de producción.</item>
///   <item>Toda la configuración llega por <c>UseSetting</c>: es la que Program.cs
///   ve ANTES de <c>Build()</c> (Jwt, AllowedHosts, Auth se leen ahí).</item>
///   <item>Límites de intentos altos por defecto, para que ningún test que no
///   los mida tropiece con ellos; cada test puede bajarlos con <c>overrides</c>.</item>
///   <item>TestServer deja <c>RemoteIpAddress</c> en null: un IStartupFilter
///   de test la fija ANTES del pipeline de la app (incluido ForwardedHeaders).</item>
/// </list>
/// Cada instancia tiene su propio contenedor de DI y, por tanto, su propia
/// caché y sus propios contadores: nada se arrastra entre tests ni corridas.
/// </summary>
public sealed class ApiFactory : WebApplicationFactory<Program>
{
    public const string DefaultClientIp = "127.0.0.1";

    private readonly Dictionary<string, string?> _settings;
    private readonly IPAddress? _clientIp;
    private readonly Action<IServiceCollection>? _configureTestServices;

    public ApiFactory(
        string? connectionString = null,
        IDictionary<string, string?>? overrides = null,
        string? clientIp = DefaultClientIp,
        Action<IServiceCollection>? configureTestServices = null)
    {
        _configureTestServices = configureTestServices;
        _clientIp = clientIp is null ? null : IPAddress.Parse(clientIp);
        _settings = new Dictionary<string, string?>
        {
            ["ConnectionStrings:Default"] = connectionString ?? "Host=127.0.0.1;Port=1;Database=sin_bd",
            // Secreto aleatorio por instancia: nunca un valor fijo en el repo.
            ["Jwt:Secret"] = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N"),
            ["Jwt:Issuer"] = "tecnm-tests",
            ["Jwt:Audience"] = "tecnm-tests-api",
            ["AllowedHosts"] = "localhost",
            ["Storage:LocalPath"] = Path.Combine(Path.GetTempPath(), "tecnm-tests-storage"),
            ["Security:RateLimiting:LoginPermitLimit"] = "100000",
            ["Security:RateLimiting:RefreshPermitLimit"] = "100000",
            ["Security:RateLimiting:AccountMaxFailures"] = "100000"
        };

        if (overrides is not null)
        {
            foreach (var (key, value) in overrides)
            {
                _settings[key] = value;
            }
        }
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        foreach (var (key, value) in _settings)
        {
            builder.UseSetting(key, value);
        }

        builder.ConfigureServices(services =>
            services.AddSingleton<IStartupFilter>(new FixedRemoteIpStartupFilter(_clientIp)));

        // Después de los registros de Program.cs (p. ej. interceptores de EF
        // que simulan fallos de BD, solo en tests).
        if (_configureTestServices is not null)
        {
            builder.ConfigureTestServices(_configureTestServices);
        }
    }

    private sealed class FixedRemoteIpStartupFilter : IStartupFilter
    {
        private readonly IPAddress? _ip;

        public FixedRemoteIpStartupFilter(IPAddress? ip) => _ip = ip;

        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            app.Use((HttpContext context, RequestDelegate nextMiddleware) =>
            {
                context.Connection.RemoteIpAddress = _ip;
                return nextMiddleware(context);
            });
            next(app);
        };
    }
}
