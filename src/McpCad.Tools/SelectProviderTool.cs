using System.ComponentModel;
using McpCad.Core;
using ModelContextProtocol.Server;

namespace McpCad.Tools;

/// <summary>MCP tool + agent skill entry for resolving ambiguous CAD provider requests (session-only).</summary>
[McpServerToolType]
public sealed class SelectProviderTool
{
    private static readonly HashSet<string> KnownProviders = new(StringComparer.OrdinalIgnoreCase)
    {
        "inventor",
        "onshape",
        "solidworks",
    };

    [McpServerTool, Description(
        "Select which CAD provider to use for subsequent tool calls in this session. " +
        "Call when the user asks to create, model, or design something without naming Inventor, SolidWorks, or Onshape.")]
    public Dictionary<string, object?> mcp_cad_select_provider(string provider)
    {
        if (string.IsNullOrWhiteSpace(provider))
            return ToolHelpers.Error("Provider name is required.");

        var normalized = provider.Trim().ToLowerInvariant();
        if (!KnownProviders.Contains(normalized))
            return ToolHelpers.Error($"Unknown provider '{provider}'. Supported: inventor, onshape, solidworks.");

        CadExecutionContext.Current.SelectedProvider = normalized;
        return ToolHelpers.Ok(new Dictionary<string, object?>
        {
            ["provider"] = normalized,
            ["message"] = $"Using {normalized} for subsequent tool calls in this session.",
        });
    }
}