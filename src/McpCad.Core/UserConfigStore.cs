using System.Runtime.InteropServices;
using System.Text.Json;
using McpCad.Core.Models;

namespace McpCad.Core;

/// <summary>Cross-platform read/write for <c>config.json</c> with atomic saves.</summary>
public static class UserConfigStore
{
    private static readonly string[] KnownProviders = ["onshape", "inventor", "solidworks"];

    /// <summary>Platform default path for <c>config.json</c>.</summary>
    public static string DefaultConfigPath => ResolveDefaultConfigPath();

    /// <summary>Loads from <see cref="DefaultConfigPath"/>; returns null if missing.</summary>
    public static UserConfig? Load() => Load(DefaultConfigPath);

    /// <summary>Loads from <paramref name="path"/>; returns null if missing; throws <see cref="JsonException"/> if corrupt.</summary>
    public static UserConfig? Load(string path)
    {
        if (!File.Exists(path))
            return null;

        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<UserConfig>(json, UserConfigJson.Options)
               ?? throw new JsonException("Deserialized config was null.");
    }

    /// <summary>Atomically saves to <see cref="DefaultConfigPath"/>.</summary>
    public static void Save(UserConfig config) => Save(config, DefaultConfigPath);

    /// <summary>Atomically saves via temp file and <see cref="File.Replace"/> (creates <c>.bak</c>).</summary>
    public static void Save(UserConfig config, string path)
    {
        ArgumentNullException.ThrowIfNull(config);

        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        var tmpPath = path + ".tmp";
        var bakPath = path + ".bak";

        try
        {
            var json = JsonSerializer.Serialize(config, UserConfigJson.Options);
            File.WriteAllText(tmpPath, json);

            if (File.Exists(path))
                File.Replace(tmpPath, path, bakPath, ignoreMetadataErrors: true);
            else
                File.Move(tmpPath, path);
        }
        catch
        {
            if (File.Exists(tmpPath))
            {
                try { File.Delete(tmpPath); } catch { /* ignore */ }
            }

            throw;
        }
    }

    /// <summary>Factory: version 1, no active provider, all known providers disabled, empty agents.</summary>
    public static UserConfig CreateDefault()
    {
        var config = new UserConfig
        {
            Version = 1,
            ActiveProvider = null,
            Agents = new List<AgentConfig>()
        };

        foreach (var name in KnownProviders)
        {
            config.Providers[name] = new ProviderConfig
            {
                Enabled = false,
                Credentials = null
            };
        }

        return config;
    }

    private static string ResolveDefaultConfigPath()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            return Path.Combine(appData, "mcp-cad", "config.json");
        }

        var xdg = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
        if (!string.IsNullOrWhiteSpace(xdg))
            return Path.Combine(xdg, "mcp-cad", "config.json");

        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return Path.Combine(home, ".config", "mcp-cad", "config.json");
    }
}