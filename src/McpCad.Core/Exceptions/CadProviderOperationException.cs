namespace McpCad.Core.Exceptions;

/// <summary>
/// Non-HTTP provider guard failures (validation, policy).
/// </summary>
public sealed class CadProviderOperationException : CadProviderException
{
    public CadProviderOperationException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}