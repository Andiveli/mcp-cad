namespace McpCad.Core.Exceptions;

/// <summary>
/// Thrown when a write conflicts with the server's microversion (409 wrongMicroversion).
/// </summary>
public sealed class OnshapeMicroversionSkewException : CadProviderException
{
    public OnshapeMicroversionSkewException(string message, string serverMicroversion, Exception? innerException = null)
        : base(message, innerException)
    {
        ServerMicroversion = serverMicroversion;
    }

    public string ServerMicroversion { get; }
}
