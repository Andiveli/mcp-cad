namespace McpCad.Core.Exceptions;

/// <summary>
/// Thrown when host configuration is missing or invalid (e.g. unknown <c>Provider:Active</c>).
/// </summary>
public sealed class ConfigurationException : Exception
{
    public ConfigurationException(string message) : base(message)
    {
    }
}