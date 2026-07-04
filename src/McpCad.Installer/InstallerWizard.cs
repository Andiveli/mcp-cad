using System.Runtime.InteropServices;
using McpCad.Core;
using McpCad.Core.Models;

namespace McpCad.Installer;

public readonly record struct ProviderChoice(string Id, string Label, bool Selectable);

public readonly record struct AgentChoice(string DisplayName, string DefaultUrl, bool PreSelected);

/// <summary>Testable wizard steps and config assembly (TUI wiring in <see cref="Program"/>).</summary>
public static class InstallerWizard
{
    public static bool IsWindows =>
        RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

    public static IReadOnlyList<ProviderChoice> GetProviderChoices(bool isWindows)
    {
        const string winOnly = " (Windows only)";
        if (isWindows)
        {
            return
            [
                new ProviderChoice("inventor", "Autodesk Inventor", true),
                new ProviderChoice("solidworks", "SolidWorks", true),
                new ProviderChoice("onshape", "Onshape", true),
            ];
        }

        return
        [
            new ProviderChoice("inventor", "Autodesk Inventor" + winOnly, false),
            new ProviderChoice("solidworks", "SolidWorks" + winOnly, false),
            new ProviderChoice("onshape", "Onshape", true),
        ];
    }

    public static IReadOnlyList<AgentChoice> KnownAgents() =>
    [
        new AgentChoice("OpenCode", "http://localhost:51777", true),
        new AgentChoice("Claude Desktop", "stdio", true),
        new AgentChoice("Pi", "stdio", true),
        new AgentChoice("VS Code", "stdio", true),
        new AgentChoice("Custom URL", "", false),
    ];

    public static bool ValidateOnshapeCredentials(string accessKey, string secretKey) =>
        !string.IsNullOrWhiteSpace(accessKey) && !string.IsNullOrWhiteSpace(secretKey);

    public static UserConfig BuildUserConfig(
        string activeProvider,
        Dictionary<string, string> credentials,
        IReadOnlyList<AgentConfig> agents,
        string? serverPath)
    {
        var config = UserConfigStore.CreateDefault();
        config.ActiveProvider = activeProvider;

        foreach (var name in config.Providers.Keys.ToList())
        {
            config.Providers[name] = new ProviderConfig
            {
                Enabled = string.Equals(name, activeProvider, StringComparison.OrdinalIgnoreCase),
                Credentials = string.Equals(name, activeProvider, StringComparison.OrdinalIgnoreCase)
                    ? new Dictionary<string, string>(credentials)
                    : null,
            };
        }

        config.Agents = agents.ToList();

        if (!string.IsNullOrWhiteSpace(serverPath))
            _ = serverPath;

        return config;
    }

    public static string DefaultUrlForAgent(string displayName) =>
        KnownAgents().First(a => a.DisplayName == displayName).DefaultUrl;

    public static string? ResolveAgentConfigPath(string displayName) => displayName switch
    {
        "OpenCode" => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".config", "opencode", "opencode.json"),
        "Claude Desktop" => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Claude", "claude_desktop_config.json"),
        "Pi" => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".pi", "agent", "mcp.json"),
        "VS Code" => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Code", "User", "mcp.json"),
        _ => null,
    };
}