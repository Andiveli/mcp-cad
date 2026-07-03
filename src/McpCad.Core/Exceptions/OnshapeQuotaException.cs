namespace McpCad.Core.Exceptions;

/// <summary>
/// Thrown when Onshape rate-limits the client (429).
/// </summary>
public sealed class OnshapeQuotaException : CadProviderException
{
    public OnshapeQuotaException(string message, TimeSpan? retryAfter = null, Exception? innerException = null)
        : this(message, 429, retryAfter, innerException)
    {
    }

    public OnshapeQuotaException(string message, int statusCode, TimeSpan? retryAfter = null, Exception? innerException = null)
        : base(message, innerException)
    {
        StatusCode = statusCode;
        RetryAfter = retryAfter;
    }

    public int StatusCode { get; }

    public TimeSpan? RetryAfter { get; }
}