using System.Text.Json;

namespace McpCad.Installer;

/// <summary>
/// JSON config read/write/merge for MCP agent registration.
/// Ported from scripts/tui/install_logic.py.
/// </summary>
public static class ConfigManager
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
    };

    /// <summary>Read and parse a JSON config file. Returns empty dict if missing.</summary>
    public static Dictionary<string, JsonElement> Read(string path)
    {
        try
        {
            if (!File.Exists(path)) return new();
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json, JsonOpts) ?? new();
        }
        catch { return new(); }
    }

    /// <summary>Merge an MCP server entry under the given key.</summary>
    public static void MergeEntry(Dictionary<string, JsonElement> config, string key, object entry)
    {
        var entryJson = JsonSerializer.Serialize(entry, JsonOpts);
        config[key] = JsonSerializer.Deserialize<JsonElement>(entryJson);
    }

    /// <summary>Atomically write config to disk.</summary>
    public static void Write(string path, Dictionary<string, JsonElement> config)
    {
        var dir = Path.GetDirectoryName(path);
        if (dir is not null) Directory.CreateDirectory(dir);
        var tmp = path + ".tmp";
        var json = JsonSerializer.Serialize(config, JsonOpts);
        File.WriteAllText(tmp, json);
        File.Move(tmp, path, overwrite: true);
    }
}
