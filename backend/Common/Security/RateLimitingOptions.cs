namespace TecnmInventario.Api.Common.Security;

/// <summary>
/// Límites de intentos (sección "Security:RateLimiting"). Los valores por
/// defecto son los aprobados para producción; cada uno se puede ajustar por
/// configuración (p. ej. Security__RateLimiting__LoginPermitLimit).
/// </summary>
public sealed class RateLimitingOptions
{
    public const string SectionName = "Security:RateLimiting";

    /// <summary>Peticiones a /api/auth/login por IP y ventana.</summary>
    public int LoginPermitLimit { get; set; } = 10;

    public int LoginWindowSeconds { get; set; } = 60;

    /// <summary>Peticiones a /api/auth/refresh por IP y ventana.</summary>
    public int RefreshPermitLimit { get; set; } = 30;

    public int RefreshWindowSeconds { get; set; } = 60;

    /// <summary>Fallos de login permitidos por identificador dentro de la ventana.</summary>
    public int AccountMaxFailures { get; set; } = 5;

    public int AccountFailureWindowMinutes { get; set; } = 15;

    /// <summary>Duración del bloqueo por identificador al alcanzar el máximo de fallos.</summary>
    public int AccountLockoutMinutes { get; set; } = 15;
}
