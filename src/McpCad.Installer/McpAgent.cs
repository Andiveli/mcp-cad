namespace McpCad.Installer;

/// <summary>
/// Represents a selectable MCP client agent in the TUI.
/// Ported from scripts/tui/items/.
/// </summary>
public class McpAgent
{
    public string Name { get; init; } = "";
    public string Description { get; init; } = "";
    public bool Selected { get; set; } = true;
    public string? ConfigPath { get; init; }
    public Func<State, McpAgent, string>? Run { get; init; }

    /// <summary>Display label with checkbox indicator.</summary>
    public string Label => Selected ? $"[x] {Name}" : $"[ ] {Name}";
}

/// <summary>
/// Factory for creating MCP agent registrations.
/// Each agent knows how to write its MCP config.
/// </summary>
public static class McpAgents
{
    private static readonly string ServerExe = Path.Combine(
        AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..",
        "McpCad.Server", "bin", "Release", "net8.0-windows", "McpCad.Server.exe");

    public static McpAgent[] All(State state)
    {
        var serverPath = Path.GetFullPath(ServerExe);
        // Fall back to dist directory if build output doesn't exist
        if (!File.Exists(serverPath))
            serverPath = FindDistServer();

        return new[]
        {
            new McpAgent
            {
                Name = "OpenCode",
                Description = "Register mcp-cad in OpenCode (opencode.json)",
                ConfigPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    ".config", "opencode", "opencode.json"),
                Run = (s, a) => RegisterMcpServer(a.ConfigPath!, serverPath, "mcp-cad", "local"),
            },
            new McpAgent
            {
                Name = "Claude",
                Description = "Register mcp-cad in Claude Desktop (claude_desktop_config.json)",
                ConfigPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "Claude", "claude_desktop_config.json"),
                Selected = false,
                Run = (s, a) => RegisterMcpServer(a.ConfigPath!, serverPath, "mcp-cad", "stdio"),
            },
            new McpAgent
            {
                Name = "Pi",
                Description = "Register mcp-cad in Pi (settings.json)",
                ConfigPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "Pi", "settings.json"),
                Selected = false,
                Run = (s, a) => RegisterMcpServer(a.ConfigPath!, serverPath, "mcp-cad", "stdio"),
            },
            new McpAgent
            {
                Name = "VS Code",
                Description = "Register mcp-cad in VS Code (mcp.json)",
                ConfigPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "Code", "User", "globalStorage", "rooveterinaryinc.roo-cline",
                    "settings", "cline_mcp_settings.json"),
                Selected = false,
                Run = (s, a) => RegisterMcpServer(a.ConfigPath!, serverPath, "mcp-cad", "stdio"),
            },
        };
    }

    private static string FindDistServer()
    {
        var baseDir = AppDomain.CurrentDomain.BaseDirectory;
        for (int i = 0; i < 6; i++)
        {
            var dist = Path.Combine(baseDir, "dist");
            if (Directory.Exists(dist))
            {
                var dirs = Directory.GetDirectories(dist, "csharp-server-*")
                    .OrderDescending()
                    .FirstOrDefault();
                if (dirs is not null)
                    return Path.Combine(dirs, "McpCad.Server.exe");
            }
            baseDir = Path.GetDirectoryName(baseDir) ?? baseDir;
        }
        return "";
    }

    private static string RegisterMcpServer(string configPath, string serverPath, string name, string type)
    {
        var config = ConfigManager.Read(configPath);
        var entry = new Dictionary<string, object?>
        {
            ["command"] = new[] { serverPath },
            ["type"] = type,
        };
        ConfigManager.MergeEntry(config, name, entry);
        ConfigManager.Write(configPath, config);
        return configPath;
    }
}
