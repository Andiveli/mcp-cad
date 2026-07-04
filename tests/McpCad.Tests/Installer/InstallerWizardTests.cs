using McpCad.Core;
using McpCad.Core.Models;
using McpCad.Installer;

namespace McpCad.Tests.Installer;

public class InstallerWizardTests
{
    [Fact]
    public void GetProviderChoices_Linux_OnshapeSelectable_DesktopCadWindowsOnly()
    {
        var choices = InstallerWizard.GetProviderChoices(isWindows: false);

        choices.Should().Contain(c => c.Id == "onshape" && c.Selectable);
        choices.Should().Contain(c => c.Id == "inventor" && !c.Selectable && c.Label.Contains("(Windows only)"));
        choices.Should().Contain(c => c.Id == "solidworks" && !c.Selectable && c.Label.Contains("(Windows only)"));
    }

    [Fact]
    public void GetProviderChoices_Windows_AllThreeSelectable()
    {
        var choices = InstallerWizard.GetProviderChoices(isWindows: true);

        choices.Should().OnlyContain(c => c.Selectable);
        choices.Select(c => c.Id).Should().BeEquivalentTo(["inventor", "solidworks", "onshape"]);
    }

    [Fact]
    public void FindServerPath_Linux_FindsServerWithoutExe()
    {
        var root = CreatePublishTree(serverFileName: "McpCad.Server");

        var found = McpAgents.FindServerPath(startDirectory: Path.Combine(root, "bin"));

        found.Should().Be(Path.Combine(root, "dist", "mcp-cad", "McpCad.Server"));
    }

    [Fact]
    public void FindServerPath_Windows_FindsServerExe()
    {
        var root = CreatePublishTree(serverFileName: "McpCad.Server.exe");

        var found = McpAgents.FindServerPath(
            startDirectory: Path.Combine(root, "bin"),
            executableFileName: "McpCad.Server.exe");

        found.Should().Be(Path.Combine(root, "dist", "mcp-cad", "McpCad.Server.exe"));
    }

    [Fact]
    public void FindServerPath_Missing_ReturnsNull()
    {
        var root = Path.Combine(Path.GetTempPath(), "mcp-installer-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);

        try
        {
            McpAgents.FindServerPath(startDirectory: root).Should().BeNull();
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void KnownAgents_ContainsExpectedHosts_AllPreSelectedExceptCustom()
    {
        var agents = InstallerWizard.KnownAgents();

        agents.Select(a => a.DisplayName).Should().BeEquivalentTo(
            ["OpenCode", "Claude Desktop", "Pi", "VS Code", "Custom URL"]);

        agents.Where(a => a.DisplayName != "Custom URL").Should().OnlyContain(a => a.PreSelected);
        agents.Single(a => a.DisplayName == "Custom URL").PreSelected.Should().BeFalse();
    }

    [Fact]
    public void BuildUserConfig_Onshape_SetsActiveProviderAndCredentials()
    {
        var config = InstallerWizard.BuildUserConfig(
            activeProvider: "onshape",
            credentials: new Dictionary<string, string>
            {
                ["access_key"] = "ak",
                ["secret_key"] = "sk",
            },
            agents: [new AgentConfig { Name = "OpenCode", Url = "http://localhost:51777" }],
            serverPath: "/dist/McpCad.Server");

        config.ActiveProvider.Should().Be("onshape");
        config.Providers["onshape"].Enabled.Should().BeTrue();
        config.Providers["onshape"].Credentials!["access_key"].Should().Be("ak");
        config.Agents.Should().HaveCount(1);
    }

    [Fact]
    public void ValidateOnshapeCredentials_Empty_ReturnsFalse()
    {
        InstallerWizard.ValidateOnshapeCredentials("", "secret").Should().BeFalse();
        InstallerWizard.ValidateOnshapeCredentials("access", "").Should().BeFalse();
    }

    [Fact]
    public void ValidateOnshapeCredentials_NonEmpty_ReturnsTrue()
    {
        InstallerWizard.ValidateOnshapeCredentials("access", "secret").Should().BeTrue();
    }

    [Fact]
    public void CompleteWizard_SavesConfig_DoesNotModifyLegacyStateJson()
    {
        var repoRoot = FindRepoRoot();
        var statePath = Path.Combine(repoRoot, "scripts", "tui", "state.json");
        var stateExisted = File.Exists(statePath);
        var beforeWrite = stateExisted ? File.GetLastWriteTimeUtc(statePath) : (DateTime?)null;

        var configDir = Path.Combine(Path.GetTempPath(), "mcp-wizard-" + Guid.NewGuid().ToString("N"));
        var configPath = Path.Combine(configDir, "config.json");
        Directory.CreateDirectory(configDir);

        try
        {
            var config = InstallerWizard.BuildUserConfig(
                "onshape",
                new Dictionary<string, string> { ["access_key"] = "a", ["secret_key"] = "b" },
                [],
                null);

            UserConfigStore.Save(config, configPath);
            File.Exists(configPath).Should().BeTrue();
            UserConfigStore.Load(configPath)!.ActiveProvider.Should().Be("onshape");

            if (stateExisted && beforeWrite.HasValue)
                File.GetLastWriteTimeUtc(statePath).Should().Be(beforeWrite.Value);
        }
        finally
        {
            if (Directory.Exists(configDir))
                Directory.Delete(configDir, recursive: true);
        }
    }

    [Fact]
    public void BuildUserConfig_NoAgentsSelected_EmptyAgentsArrayIsValid()
    {
        var config = InstallerWizard.BuildUserConfig(
            "onshape",
            new Dictionary<string, string> { ["access_key"] = "a", ["secret_key"] = "b" },
            [],
            null);

        config.Agents.Should().BeEmpty();
        var json = System.Text.Json.JsonSerializer.Serialize(config, UserConfigJson.Options);
        json.Should().Contain("\"agents\": []");
    }

    private static string CreatePublishTree(string serverFileName)
    {
        var root = Path.Combine(Path.GetTempPath(), "mcp-publish-" + Guid.NewGuid().ToString("N"));
        var serverDir = Path.Combine(root, "dist", "mcp-cad");
        Directory.CreateDirectory(Path.Combine(root, "bin"));
        Directory.CreateDirectory(serverDir);
        var serverPath = Path.Combine(serverDir, serverFileName);
        File.WriteAllText(serverPath, "");
        return root;
    }

    private static string FindRepoRoot()
    {
        var dir = AppContext.BaseDirectory;
        for (var i = 0; i < 12; i++)
        {
            if (Directory.Exists(Path.Combine(dir, "src", "McpCad.Installer")))
                return dir;
            var parent = Directory.GetParent(dir)?.FullName;
            if (parent is null || parent == dir)
                break;
            dir = parent;
        }

        throw new InvalidOperationException("Could not locate repository root from test output.");
    }
}