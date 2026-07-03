namespace McpCad.Core.Exceptions;

/// <summary>
/// Thrown when two MCP tool providers register the same tool name at startup.
/// </summary>
public sealed class ToolRegistrationException : Exception
{
    public ToolRegistrationException(string toolName, IReadOnlyList<string> providerNames)
        : base(BuildMessage(toolName, providerNames))
    {
        ToolName = toolName;
        ProviderNames = providerNames;
    }

    public string ToolName { get; }

    public IReadOnlyList<string> ProviderNames { get; }

    private static string BuildMessage(string toolName, IReadOnlyList<string> providerNames) =>
        $"Tool name collision: '{toolName}' is registered by providers: {string.Join(", ", providerNames)}.";
}