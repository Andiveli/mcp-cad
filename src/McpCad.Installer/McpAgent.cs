using Tomlyn;
using Tomlyn.Model;

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
            new McpAgent
            {
                Name = "Cursor",
                Description = "Register in ~/.cursor/mcp.json",
                ConfigPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    ".cursor", "mcp.json"),
                Selected = false,
                Run = (s, a) => RegisterWithSchema(a.ConfigPath!, serverPath, "mcpServers", "stdio"),
            },
            new McpAgent
            {
                Name = "Grok",
                Description = "Register in ~/.grok/config.toml",
                ConfigPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    ".grok", "config.toml"),
                Selected = false,
                Run = (s, a) => RegisterGrok(a.ConfigPath!, serverPath),
            },
        };
    }

    /// <summary>
    /// Public helper so the UI can show the resolved server path to the user.
    /// </summary>
    public static string GetResolvedServerPath() => FindServerPath();

    private static string FindServerPath()
    {
        var candidates = new List<string>();
        var baseDir = AppDomain.CurrentDomain.BaseDirectory?.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) ?? "";

        // 1. Most common for end-users: portable download — server files placed next to the Installer.exe
        candidates.Add(Path.Combine(baseDir, "McpCad.Server.exe"));

        // 2. Sensible subfolder layouts inside a zip/extracted package
        candidates.Add(Path.Combine(baseDir, "server", "McpCad.Server.exe"));
        candidates.Add(Path.Combine(baseDir, "mcp-cad", "McpCad.Server.exe"));
        candidates.Add(Path.Combine(baseDir, "bin", "McpCad.Server.exe"));

        // 3. Legacy dev build layout: walk upwards looking for dist/mcp-cad or dist/csharp-server-*
        var dir = baseDir;
        for (int i = 0; i < 8; i++)
        {
            var found = FindInDist(dir);
            if (found is not null)
                candidates.Add(found);

            // Also check if the scanned dir itself contains the exe directly (in case baseDir == the published folder)
            var directHere = Path.Combine(dir, "McpCad.Server.exe");
            if (File.Exists(directHere))
                candidates.Add(directHere);

            var parent = Path.GetDirectoryName(dir);
            if (parent is null || parent == dir) break;
            dir = parent;
        }

        // 4. CWD fallbacks (double-click scenarios sometimes have different CWD)
        var cwd = Environment.CurrentDirectory;
        candidates.Add(Path.Combine(cwd, "McpCad.Server.exe"));
        candidates.Add(Path.Combine(cwd, "dist", "mcp-cad", "McpCad.Server.exe"));

        // Pick the first that actually exists on disk
        foreach (var c in candidates.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (!string.IsNullOrWhiteSpace(c) && File.Exists(c))
                return c;
        }

        return "";
    }

    private static string? FindInDist(string baseDir)
    {
        if (string.IsNullOrWhiteSpace(baseDir) || !Directory.Exists(baseDir))
            return null;

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

    private static string RegisterGrok(string configPath, string serverPath)
    {
        var dir = Path.GetDirectoryName(configPath);
        if (dir is not null) Directory.CreateDirectory(dir);

        TomlTable root;
        if (File.Exists(configPath))
        {
            var text = File.ReadAllText(configPath);
            try
            {
                root = Toml.ToModel(text) ?? new TomlTable();
            }
            catch
            {
                root = new TomlTable();
            }
        }
        else
        {
            root = new TomlTable();
        }

        TomlTable mcpServers;
        if (root.TryGetValue("mcp_servers", out var existing) && existing is TomlTable existingTable)
        {
            mcpServers = existingTable;
        }
        else
        {
            mcpServers = new TomlTable();
            root["mcp_servers"] = mcpServers;
        }

        var entry = new TomlTable
        {
            ["command"] = serverPath,
            ["args"] = new TomlArray(),
            ["enabled"] = true
        };
        mcpServers["mcp-cad"] = entry;

        var newToml = Toml.FromModel(root);

        // Tomlyn model serializer drops empty arrays. Ensure "args = []" is present for Grok.
        // The mcp-cad table is small, so we inject explicitly after its command line.
        if (!newToml.Contains("args"))
        {
            // Insert args = [] after the command line inside the mcp-cad table
            newToml = System.Text.RegularExpressions.Regex.Replace(
                newToml,
                @"(\[mcp_servers\.mcp-cad\][^\[]*?command\s*=\s*""[^""]*"")(\r?\n)",
                "$1\r\nargs = []\r\n",
                System.Text.RegularExpressions.RegexOptions.Singleline);
        }

        var tmp = configPath + ".tmp";
        File.WriteAllText(tmp, newToml.TrimEnd() + Environment.NewLine);
        File.Move(tmp, configPath, overwrite: true);
        return configPath;
    }
}
