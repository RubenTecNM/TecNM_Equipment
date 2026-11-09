using System.Globalization;
using System.Net;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace TecnmInventario.Api.Common.Security;

/// <summary>Nombres de las políticas de límite por IP (atributos [EnableRateLimiting]).</summary>
public static class RateLimitPolicies
{
    public const string Login = "auth-login";
    public const string Refresh = "auth-refresh";
}

/// <summary>
/// Perímetro HTTP: IP real detrás de un proxy (ForwardedHeaders) y límites de
/// intentos por IP (RateLimiter del framework) y por cuenta (LoginAttemptLimiter).
/// </summary>
public static class PerimeterSecurityExtensions
{
    public const string ForwardedHeadersSection = "ForwardedHeaders";

    public static IServiceCollection AddPerimeterSecurity(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<RateLimitingOptions>(configuration.GetSection(RateLimitingOptions.SectionName));
        services.AddSingleton<LoginAttemptLimiter>();

        // Solo se confía en loopback (valores por defecto del framework, que NO
        // se limpian) más los proxies y redes configurados. Vacío por defecto.
        services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            options.ForwardLimit = 1;

            foreach (var proxy in ReadList(configuration, $"{ForwardedHeadersSection}:KnownProxies"))
            {
                options.KnownProxies.Add(IPAddress.TryParse(proxy, out var address)
                    ? address
                    : throw new InvalidOperationException(
                        $"ForwardedHeaders:KnownProxies contiene una IP no válida: '{proxy}'."));
            }

            foreach (var network in ReadList(configuration, $"{ForwardedHeadersSection}:KnownNetworks"))
            {
                options.KnownIPNetworks.Add(System.Net.IPNetwork.TryParse(network, out var parsed)
                    ? parsed
                    : throw new InvalidOperationException(
                        $"ForwardedHeaders:KnownNetworks contiene una red no válida (formato CIDR): '{network}'."));
            }
        });

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.AddPolicy(RateLimitPolicies.Login, context => FixedWindowByIp(context,
                RateLimitPolicies.Login, o => (o.LoginPermitLimit, o.LoginWindowSeconds)));
            options.AddPolicy(RateLimitPolicies.Refresh, context => FixedWindowByIp(context,
                RateLimitPolicies.Refresh, o => (o.RefreshPermitLimit, o.RefreshWindowSeconds)));
            options.OnRejected = WriteRejectionAsync;
        });

        return services;
    }

    /// <summary>Partición del límite por IP: la IP ya corregida por ForwardedHeaders.</summary>
    public static string ClientPartitionKey(HttpContext context)
        => context.Connection.RemoteIpAddress?.ToString() ?? "unknown";

    private static RateLimitPartition<string> FixedWindowByIp(
        HttpContext context, string policy, Func<RateLimitingOptions, (int Permits, int WindowSeconds)> select)
    {
        var (permits, windowSeconds) = select(
            context.RequestServices.GetRequiredService<IOptions<RateLimitingOptions>>().Value);

        return RateLimitPartition.GetFixedWindowLimiter($"{policy}:{ClientPartitionKey(context)}", _ =>
            new FixedWindowRateLimiterOptions
            {
                PermitLimit = permits,
                Window = TimeSpan.FromSeconds(windowSeconds),
                QueueLimit = 0,
                AutoReplenishment = true
            });
    }

    private static async ValueTask WriteRejectionAsync(OnRejectedContext context, CancellationToken cancellationToken)
    {
        var retryAfter = context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var value)
            ? value
            : TimeSpan.FromMinutes(1);

        var response = context.HttpContext.Response;
        response.StatusCode = StatusCodes.Status429TooManyRequests;
        response.Headers.RetryAfter = RetryAfterSeconds(retryAfter);
        response.ContentType = "application/problem+json";

        await response.WriteAsJsonAsync(new ProblemDetails
        {
            Status = StatusCodes.Status429TooManyRequests,
            Title = "Demasiadas solicitudes",
            Detail = TooManyRequestsException.DefaultMessage,
            Instance = context.HttpContext.Request.Path
        }, options: null, contentType: "application/problem+json", cancellationToken);
    }

    /// <summary>Segundos enteros, redondeando hacia arriba y nunca menos de 1.</summary>
    public static string RetryAfterSeconds(TimeSpan retryAfter)
        => Math.Max(1, (int)Math.Ceiling(retryAfter.TotalSeconds)).ToString(CultureInfo.InvariantCulture);

    private static IEnumerable<string> ReadList(IConfiguration configuration, string key)
        => configuration.GetSection(key).GetChildren()
            .Select(c => c.Value?.Trim())
            .Where(v => !string.IsNullOrEmpty(v))
            .Select(v => v!);
}
