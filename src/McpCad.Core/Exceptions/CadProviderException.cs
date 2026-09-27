namespace McpCad.Core.Exceptions;

/// <summary>
/// Base exception for CAD provider failures (web and mechanical).
/// </summary>
public abstract class CadProviderException : Exception
{
    protected CadProviderException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}
