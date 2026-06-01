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
    public static McpAgent[] All(State state)
    {
        var serverPath = FindServerPath();

        return new[]
        {
            new McpAgent
            {
                Name = "OpenCode",
                Description = "Register in ~/.config/opencode/opencode.json",
                ConfigPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    ".config", "opencode", "opencode.json"),
                Run = (s, a) => RegisterWithSchema(a.ConfigPath!, serverPath, "mcp", "local"),
            },
            new McpAgent
            {
                Name = "Claude",
                Description = "Register in %APPDATA%/Claude/claude_desktop_config.json",
                ConfigPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "Claude", "claude_desktop_config.json"),
                Selected = false,
                Run = (s, a) => RegisterWithSchema(a.ConfigPath!, serverPath, "mcpServers", "stdio"),
            },
            new McpAgent
            {
                Name = "Pi",
                Description = "Register in ~/.pi/agent/mcp.json",
                ConfigPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    ".pi", "agent", "mcp.json"),
                Selected = false,
                Run = (s, a) => RegisterWithSchema(a.ConfigPath!, serverPath, "mcpServers", "stdio"),
            },
            new McpAgent
            {
                Name = "VS Code",
                Description = "Register in %APPDATA%/Code/User/mcp.json",
                ConfigPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "Code", "User", "mcp.json"),
                Selected = false,
                Run = (s, a) => RegisterWithSchema(a.ConfigPath!, serverPath, "servers", "stdio"),
            },
        };
    }

    private static string FindServerPath()
    {
        // Walk up from assembly dir looking for dist/mcp-cad or dist/csharp-server-*
        var dir = AppDomain.CurrentDomain.BaseDirectory;
        for (int i = 0; i < 8; i++)
        {
            var found = FindInDist(dir);
            if (found is not null) return found;
            var parent = Path.GetDirectoryName(dir);
            if (parent is null || parent == dir) break;
            dir = parent;
        }
        return FindInDist(Environment.CurrentDirectory) ?? "";
    }

    private static string? FindInDist(string baseDir)
    {
        var dist = Path.Combine(baseDir, "dist");
        if (!Directory.Exists(dist)) return null;

        // Prefer dist/mcp-cad/
        var direct = Path.Combine(dist, "mcp-cad", "McpCad.Server.exe");
        if (File.Exists(direct)) return direct;

        // Fallback: latest csharp-server-vN
        return Directory.GetDirectories(dist, "csharp-server-*")
            .Select(d => new { Path = d, Ver = ExtractVersion(d) })
            .Where(x => x.Ver >= 0)
            .OrderByDescending(x => x.Ver)
            .Select(x => Path.Combine(x.Path, "McpCad.Server.exe"))
            .FirstOrDefault(File.Exists);
    }

    private static int ExtractVersion(string dirPath)
    {
        var name = Path.GetFileName(dirPath);
        var idx = name.LastIndexOf('v');
        return idx >= 0 && int.TryParse(name[(idx + 1)..], out var v) ? v : -1;
    }

    private static string RegisterWithSchema(string configPath, string serverPath, string parentKey, string type)
    {
        var config = ConfigManager.Read(configPath);
        var entry = type == "local"
            ? new Dictionary<string, object?> { ["type"] = "local", ["command"] = new[] { serverPath } }
            : new Dictionary<string, object?> { ["command"] = serverPath, ["args"] = Array.Empty<string>() };
        ConfigManager.MergeEntry(config, parentKey, new Dictionary<string, object?> { ["mcp-cad"] = entry });
        ConfigManager.Write(configPath, config);
        return configPath;
    }
}
