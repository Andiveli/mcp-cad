using System.Text.Json;
using System.Text.Json.Serialization;

namespace McpCad.Server;

/// <summary>
/// ~/.config/mcp-cad/config.json (installer / TUI output).
/// </summary>
internal sealed class UserMcpCadConfig
{
    [JsonPropertyName("active_provider")]
    public string? ActiveProvider { get; set; }

    [JsonPropertyName("providers")]
    public Dictionary<string, UserProviderEntry>? Providers { get; set; }

    public static string DefaultPath()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return Path.Combine(home, ".config", "mcp-cad", "config.json");
    }

    public static UserMcpCadConfig? TryLoad(string? path = null)
    {
        path ??= DefaultPath();
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<UserMcpCadConfig>(json, JsonOptions);
        }
        catch
        {
            return null;
        }
    }

    public void ApplyToConfiguration(Dictionary<string, string?> values)
    {
        if (!string.IsNullOrWhiteSpace(ActiveProvider))
        {
            values["Provider:Active"] = ActiveProvider.Trim();
        }

        var onshape = Providers?.GetValueOrDefault("onshape");
        var creds = onshape?.Credentials;
        if (creds is null)
        {
            return;
        }

        if (!string.IsNullOrEmpty(creds.AccessKey))
        {
            values["Onshape:AccessKey"] = creds.AccessKey;
        }

        if (!string.IsNullOrEmpty(creds.SecretKey))
        {
            values["Onshape:SecretKey"] = creds.SecretKey;
        }

        if (!string.IsNullOrEmpty(creds.SecretKeyId))
        {
            values["Onshape:SecretKeyId"] = creds.SecretKeyId;
        }
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };
}

internal sealed class UserProviderEntry
{
    [JsonPropertyName("credentials")]
    public UserOnshapeCredentials? Credentials { get; set; }
}

internal sealed class UserOnshapeCredentials
{
    [JsonPropertyName("access_key")]
    public string? AccessKey { get; set; }

    [JsonPropertyName("secret_key")]
    public string? SecretKey { get; set; }

    [JsonPropertyName("secret_key_id")]
    public string? SecretKeyId { get; set; }
}