using System.Runtime.InteropServices;

namespace McpCad.Installer;

public class McpAgent
{
    public string Name { get; init; } = "";
    public string Description { get; init; } = "";
    public bool Selected { get; set; } = true;
    public string? ConfigPath { get; init; }
    public Func<State, McpAgent, string>? Run { get; init; }

    public string Label => Selected ? $"[[x]] {Name}" : $"[[ ]] {Name}";
}

public static class McpAgents
{
    public static string? FindServerPath(
        string? startDirectory = null,
        string? executableFileName = null)
    {
        var fileName = executableFileName ?? (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? "McpCad.Server.exe"
            : "McpCad.Server");

        var dir = startDirectory ?? AppDomain.CurrentDomain.BaseDirectory;
        for (var i = 0; i < 8; i++)
        {
            var found = FindInDist(dir, fileName);
            if (found is not null)
                return found;

            var parent = Path.GetDirectoryName(dir);
            if (parent is null || parent == dir)
                break;
            dir = parent;
        }

        return FindInDist(Environment.CurrentDirectory, fileName);
    }

    internal static string? FindInDist(string baseDir, string serverFileName)
    {
        var dist = Path.Combine(baseDir, "dist");
        if (!Directory.Exists(dist))
            return null;

        var direct = Path.Combine(dist, "mcp-cad", serverFileName);
        if (File.Exists(direct))
            return direct;

        return Directory.GetDirectories(dist, "csharp-server-*")
            .Select(d => new { Path = d, Ver = ExtractVersion(d) })
            .Where(x => x.Ver >= 0)
            .OrderByDescending(x => x.Ver)
            .Select(x => Path.Combine(x.Path, serverFileName))
            .FirstOrDefault(File.Exists);
    }

    private static int ExtractVersion(string dirPath)
    {
        var name = Path.GetFileName(dirPath);
        var idx = name.LastIndexOf('v');
        return idx >= 0 && int.TryParse(name[(idx + 1)..], out var v) ? v : -1;
    }

    public static void RegisterAgentMcpJson(string displayName, string serverPath)
    {
        if (string.IsNullOrWhiteSpace(serverPath))
            return;

        var configPath = InstallerWizard.ResolveAgentConfigPath(displayName);
        if (configPath is null)
            return;

        var (parentKey, type) = displayName switch
        {
            "OpenCode" => ("mcp", "local"),
            "VS Code" => ("servers", "stdio"),
            _ => ("mcpServers", "stdio"),
        };

        var config = ConfigManager.Read(configPath);
        var entry = type == "local"
            ? new Dictionary<string, object?> { ["type"] = "local", ["command"] = new[] { serverPath } }
            : new Dictionary<string, object?> { ["command"] = serverPath, ["args"] = Array.Empty<string>() };
        ConfigManager.MergeEntry(config, parentKey, new Dictionary<string, object?> { ["mcp-cad"] = entry });
        ConfigManager.Write(configPath, config);
    }
}