using System.Text.Json;
using System.Text.Json.Serialization;

namespace McpCad.Core.Models;

/// <summary>Unified user configuration (provider selection, credentials, agents).</summary>
public sealed class UserConfig
{
    [JsonPropertyName("version")]
    public int Version { get; set; } = 1;

    [JsonPropertyName("active_provider")]
    public string? ActiveProvider { get; set; }

    [JsonPropertyName("providers")]
    public Dictionary<string, ProviderConfig> Providers { get; set; } = new();

    [JsonPropertyName("agents")]
    public List<AgentConfig> Agents { get; set; } = new();
}

/// <summary>Per-provider enable flag and optional credential key-value pairs.</summary>
public sealed class ProviderConfig
{
    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; }

    [JsonPropertyName("credentials")]
    public Dictionary<string, string>? Credentials { get; set; }
}

/// <summary>Registered MCP host agent endpoint.</summary>
public sealed class AgentConfig
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("url")]
    public string Url { get; set; } = "";
}

/// <summary>Shared JSON options for user config (snake_case, forward-compatible).</summary>
public static class UserConfigJson
{
    public static JsonSerializerOptions Options { get; } = CreateOptions();

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true
        };
        return options;
    }
}