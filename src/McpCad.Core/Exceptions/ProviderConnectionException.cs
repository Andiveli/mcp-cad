namespace McpCad.Core.Exceptions;

public sealed class ProviderConnectionException : Exception
{
    public ProviderConnectionException(string message, Exception? inner = null)
        : base(message, inner)
    {
    }
}