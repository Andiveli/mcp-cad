namespace McpCad.Core.Exceptions;

/// <summary>
/// Thrown when Onshape rejects credentials (401/403).
/// </summary>
public sealed class OnshapeAuthException : CadProviderException
{
    public OnshapeAuthException(string message, int statusCode, string reason, Exception? innerException = null)
        : base(message, innerException)
    {
        StatusCode = statusCode;
        Reason = reason;
    }

    public int StatusCode { get; }

    public string Reason { get; }
}
