namespace TecnmInventario.Api.Common.Security;

/// <summary>
/// Demasiados intentos: se traduce en 429 con Retry-After. El mensaje es el
/// mismo para cualquier identificador (exista o no la cuenta) y para el
/// límite por IP, para no revelar nada.
/// </summary>
public sealed class TooManyRequestsException : Exception
{
    public const string DefaultMessage = "Demasiados intentos. Espera unos minutos e inténtalo de nuevo.";

    public TooManyRequestsException(TimeSpan retryAfter)
        : base(DefaultMessage)
    {
        RetryAfter = retryAfter;
    }

    public TimeSpan RetryAfter { get; }
}
