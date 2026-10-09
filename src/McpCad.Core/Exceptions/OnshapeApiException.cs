namespace McpCad.Core.Exceptions;

/// <summary>
/// Onshape HTTP/API failure with status and reason (non-auth, non-quota, non-skew).
/// </summary>
public sealed class OnshapeApiException : CadProviderException
{
    public OnshapeApiException(string message, int statusCode, string reason, Exception? innerException = null)
        : base(message, innerException)
    {
        StatusCode = statusCode;
        Reason = reason;
    }

    public int StatusCode { get; }

    public string Reason { get; }

    /// <summary>
    /// Maps HTTP status codes to typed <see cref="CadProviderException"/> instances for the Onshape client.
    /// </summary>
    public static CadProviderException FromHttpStatus(
        int statusCode,
        string reason,
        TimeSpan? retryAfter = null,
        string? serverMicroversion = null)
    {
        var message = $"Onshape API error ({statusCode}): {reason}";

        return statusCode switch
        {
            401 or 403 => new OnshapeAuthException(message, statusCode, reason),
            409 => new OnshapeMicroversionSkewException(message, serverMicroversion ?? string.Empty),
            429 => new OnshapeQuotaException(message, statusCode, retryAfter),
            _ => new OnshapeApiException(message, statusCode, reason),
        };
    }
}
