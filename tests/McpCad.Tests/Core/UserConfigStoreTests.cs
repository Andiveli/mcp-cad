using System.Text.Json;
using McpCad.Core;
using McpCad.Core.Models;

namespace McpCad.Tests.Core;

public class UserConfigStoreTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string? _savedXdg;

    public UserConfigStoreTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "mcp-cad-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
        _savedXdg = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
    }

    public void Dispose()
    {
        if (_savedXdg is null)
            Environment.SetEnvironmentVariable("XDG_CONFIG_HOME", null);
        else
            Environment.SetEnvironmentVariable("XDG_CONFIG_HOME", _savedXdg);

        try
        {
            if (Directory.Exists(_tempDir))
                Directory.Delete(_tempDir, recursive: true);
        }
        catch
        {
            // best effort
        }
    }

    [Fact]
    public void DefaultConfigPath_OnLinux_UsesConfigUnderHomeOrXdg()
    {
        if (!OperatingSystem.IsLinux())
            return;

        var path = UserConfigStore.DefaultConfigPath;
        path.Should().EndWith(Path.Combine("mcp-cad", "config.json"));

        Environment.SetEnvironmentVariable("XDG_CONFIG_HOME", _tempDir);
        var withXdg = UserConfigStore.DefaultConfigPath;
        withXdg.Should().Be(Path.Combine(_tempDir, "mcp-cad", "config.json"));
    }

    [Fact]
    public void DefaultConfigPath_OnWindows_UsesAppData()
    {
        if (!OperatingSystem.IsWindows())
            return;

        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        UserConfigStore.DefaultConfigPath.Should().Be(Path.Combine(appData, "mcp-cad", "config.json"));
    }

    [Fact]
    public void Load_ReturnsUserConfig_ForValidFile()
    {
        var path = Path.Combine(_tempDir, "config.json");
        var config = UserConfigStore.CreateDefault();
        config.ActiveProvider = "onshape";
        UserConfigStore.Save(config, path);

        var loaded = UserConfigStore.Load(path);

        loaded.Should().NotBeNull();
        loaded!.ActiveProvider.Should().Be("onshape");
    }

    [Fact]
    public void Load_ReturnsNull_WhenFileMissing()
    {
        var path = Path.Combine(_tempDir, "missing.json");
        UserConfigStore.Load(path).Should().BeNull();
    }

    [Fact]
    public void Load_ThrowsJsonException_OnCorruptFile()
    {
        var path = Path.Combine(_tempDir, "bad.json");
        File.WriteAllText(path, "{ not json");

        var act = () => UserConfigStore.Load(path);
        act.Should().Throw<JsonException>();
    }

    [Fact]
    public void Save_WritesValidJson()
    {
        var path = Path.Combine(_tempDir, "out.json");
        var config = UserConfigStore.CreateDefault();
        config.Providers["onshape"].Enabled = true;

        UserConfigStore.Save(config, path);

        File.Exists(path).Should().BeTrue();
        var text = File.ReadAllText(path);
        text.Should().Contain("\"version\"");
        text.Should().Contain("\"onshape\"");
    }

    [Fact]
    public void Save_IsAtomic_CreatesBakAndNoTornReads()
    {
        var path = Path.Combine(_tempDir, "atomic.json");
        var first = UserConfigStore.CreateDefault();
        first.ActiveProvider = "inventor";
        UserConfigStore.Save(first, path);

        var second = UserConfigStore.CreateDefault();
        second.ActiveProvider = "onshape";
        UserConfigStore.Save(second, path);

        File.Exists(path + ".bak").Should().BeTrue();
        var loaded = UserConfigStore.Load(path);
        loaded!.ActiveProvider.Should().Be("onshape");
        File.Exists(path + ".tmp").Should().BeFalse();
    }

    [Fact]
    public void CreateDefault_MatchesInvariant()
    {
        var config = UserConfigStore.CreateDefault();
        config.Version.Should().Be(1);
        config.ActiveProvider.Should().BeNull();
        config.Providers.Should().ContainKeys("onshape", "inventor", "solidworks");
        config.Agents.Should().BeEmpty();
    }

    [Fact]
    public void McpCadCore_HasNoNewPackageReferences()
    {
        var csproj = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "..",
            "src", "McpCad.Core", "McpCad.Core.csproj"));

        if (!File.Exists(csproj))
        {
            csproj = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "src", "McpCad.Core", "McpCad.Core.csproj"));
        }

        File.Exists(csproj).Should().BeTrue($"Expected csproj at {csproj}");
        var content = File.ReadAllText(csproj);
        content.Should().NotContain("PackageReference");
    }

    [Fact]
    public void EmptyAgentsArray_IsValid()
    {
        var path = Path.Combine(_tempDir, "empty-agents.json");
        File.WriteAllText(path, """
            {
              "version": 1,
              "active_provider": null,
              "providers": {
                "onshape": { "enabled": false, "credentials": null },
                "inventor": { "enabled": false, "credentials": null },
                "solidworks": { "enabled": false, "credentials": null }
              },
              "agents": []
            }
            """);

        UserConfigStore.Load(path).Should().NotBeNull();
    }

    [Fact]
    public void ActiveProviderNull_IsValid()
    {
        var config = UserConfigStore.CreateDefault();
        config.ActiveProvider.Should().BeNull();
        var path = Path.Combine(_tempDir, "null-active.json");
        UserConfigStore.Save(config, path);
        UserConfigStore.Load(path)!.ActiveProvider.Should().BeNull();
    }

    [Fact]
    public void Save_LeavesNoTmpOrphan_AfterSuccessfulWrite()
    {
        var path = Path.Combine(_tempDir, "clean-tmp.json");
        UserConfigStore.Save(UserConfigStore.CreateDefault(), path);
        Directory.GetFiles(_tempDir, "*.tmp").Should().BeEmpty();
    }

    [Fact]
    public void ConcurrentReadsDuringWrite_SeeOldOrNewNeverPartial()
    {
        var path = Path.Combine(_tempDir, "concurrent.json");
        UserConfigStore.Save(UserConfigStore.CreateDefault(), path);

        var errors = new List<Exception>();
        var barrier = new Barrier(3);

        void Reader()
        {
            try
            {
                barrier.SignalAndWait();
                for (var i = 0; i < 50; i++)
                {
                    var c = UserConfigStore.Load(path);
                    if (c is null)
                        throw new InvalidOperationException("unexpected null");
                    if (c.Version != 1)
                        throw new InvalidOperationException("bad version");
                }
            }
            catch (Exception ex)
            {
                lock (errors) errors.Add(ex);
            }
        }

        var t1 = new Thread(Reader);
        var t2 = new Thread(Reader);
        t1.Start();
        t2.Start();

        barrier.SignalAndWait();
        for (var i = 0; i < 30; i++)
        {
            var cfg = UserConfigStore.CreateDefault();
            cfg.ActiveProvider = i % 2 == 0 ? "onshape" : "inventor";
            UserConfigStore.Save(cfg, path);
        }

        t1.Join();
        t2.Join();
        errors.Should().BeEmpty();
    }
}