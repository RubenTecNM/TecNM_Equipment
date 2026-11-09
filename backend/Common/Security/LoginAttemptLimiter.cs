using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace TecnmInventario.Api.Common.Security;

/// <summary>
/// Límite de fallos de login por identificador normalizado (el texto escrito,
/// exista o no la cuenta). Al llegar a <see cref="RateLimitingOptions.AccountMaxFailures"/>
/// fallos dentro de la ventana, el identificador queda bloqueado durante
/// <see cref="RateLimitingOptions.AccountLockoutMinutes"/>. Un login correcto
/// borra el contador.
/// Estado en memoria del proceso: se pierde al reiniciar y no se comparte
/// entre instancias (decisión D1 del Lote 5).
/// </summary>
public sealed class LoginAttemptLimiter
{
    private readonly IMemoryCache _cache;
    private readonly RateLimitingOptions _options;
    private readonly object _gate = new();

    public LoginAttemptLimiter(IMemoryCache cache, IOptions<RateLimitingOptions> options)
    {
        _cache = cache;
        _options = options.Value;
    }

    public bool IsLockedOut(string identifier, out TimeSpan retryAfter)
    {
        lock (_gate)
        {
            retryAfter = TimeSpan.Zero;
            if (!_cache.TryGetValue(Key(identifier), out Entry? entry) || entry?.LockedUntil is null)
                return false;

            var remaining = entry.LockedUntil.Value - DateTimeOffset.UtcNow;
            if (remaining <= TimeSpan.Zero)
            {
                // Bloqueo cumplido: se empieza de cero.
                _cache.Remove(Key(identifier));
                return false;
            }

            retryAfter = remaining;
            return true;
        }
    }

    /// <summary>Registra un fallo. Devuelve true si este fallo activa el bloqueo.</summary>
    public bool RegisterFailure(string identifier)
    {
        lock (_gate)
        {
            var now = DateTimeOffset.UtcNow;
            if (!_cache.TryGetValue(Key(identifier), out Entry? entry) || entry is null ||
                (entry.LockedUntil is null && now >= entry.WindowEndsAt))
            {
                entry = new Entry { WindowEndsAt = now.AddMinutes(_options.AccountFailureWindowMinutes) };
            }

            entry.Failures++;
            var lockedNow = entry.LockedUntil is null && entry.Failures >= _options.AccountMaxFailures;
            if (lockedNow)
            {
                entry.LockedUntil = now.AddMinutes(_options.AccountLockoutMinutes);
            }

            var expiresAt = entry.LockedUntil is { } lockedUntil && lockedUntil > entry.WindowEndsAt
                ? lockedUntil
                : entry.WindowEndsAt;
            _cache.Set(Key(identifier), entry, expiresAt);

            return lockedNow;
        }
    }

    public void Reset(string identifier)
    {
        lock (_gate)
        {
            _cache.Remove(Key(identifier));
        }
    }

    private static string Key(string identifier) => $"login-attempts:{identifier}";

    private sealed class Entry
    {
        public int Failures { get; set; }
        public DateTimeOffset WindowEndsAt { get; init; }
        public DateTimeOffset? LockedUntil { get; set; }
    }
}
