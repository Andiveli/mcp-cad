using System.Text.Json;
using McpCad.Core;
using McpCad.Core.Models;

namespace McpCad.Tests.Core.Models;

public class UserConfigTests
{
    private static readonly JsonSerializerOptions Options = UserConfigJson.Options;

    [Fact]
    public void Deserializes_ValidJson_WithVersionActiveProviderProvidersAgents()
    {
        const string json = """
            {
              "version": 1,
              "active_provider": "onshape",
              "providers": {
                "onshape": { "enabled": true, "credentials": { "access_key": "ak" } },
                "inventor": { "enabled": false, "credentials": null },
                "solidworks": { "enabled": false, "credentials": null }
              },
              "agents": [{ "name": "OpenCode", "url": "http://localhost:51777" }]
            }
            """;

        var config = JsonSerializer.Deserialize<UserConfig>(json, Options);

        config.Should().NotBeNull();
        config!.Version.Should().Be(1);
        config.ActiveProvider.Should().Be("onshape");
        config.Providers.Should().ContainKey("onshape");
        config.Providers["onshape"].Enabled.Should().BeTrue();
        config.Providers["onshape"].Credentials!["access_key"].Should().Be("ak");
        config.Agents.Should().HaveCount(1);
        config.Agents[0].Name.Should().Be("OpenCode");
        config.Agents[0].Url.Should().Be("http://localhost:51777");
    }

    [Fact]
    public void ProviderConfig_HasEnabledAndNullableCredentials()
    {
        var pc = new ProviderConfig { Enabled = true, Credentials = new Dictionary<string, string> { ["k"] = "v" } };
        pc.Enabled.Should().BeTrue();
        pc.Credentials!["k"].Should().Be("v");

        var empty = new ProviderConfig { Enabled = false, Credentials = null };
        empty.Credentials.Should().BeNull();
    }

    [Fact]
    public void AgentConfig_HasNameAndUrl()
    {
        var a = new AgentConfig { Name = "Pi", Url = "http://x" };
        a.Name.Should().Be("Pi");
        a.Url.Should().Be("http://x");
    }

    [Fact]
    public void UnknownProviderKeys_PreservedOnRoundTrip()
    {
        const string json = """
            {
              "version": 1,
              "active_provider": null,
              "providers": {
                "onshape": { "enabled": false, "credentials": null },
                "future_cad": { "enabled": true, "credentials": null }
              },
              "agents": []
            }
            """;

        var config = JsonSerializer.Deserialize<UserConfig>(json, Options);
        config!.Providers.Should().ContainKey("future_cad");

        var round = JsonSerializer.Serialize(config, Options);
        var again = JsonSerializer.Deserialize<UserConfig>(round, Options);
        again!.Providers.Should().ContainKey("future_cad");
    }

    [Fact]
    public void UnknownVersion_LoadsWithoutThrow()
    {
        const string json = """
            {
              "version": 99,
              "active_provider": null,
              "providers": {},
              "agents": []
            }
            """;

        UserConfig? deserialized = null;
        var act = () => deserialized = JsonSerializer.Deserialize<UserConfig>(json, Options);
        act.Should().NotThrow();
        deserialized!.Version.Should().Be(99);
    }

    [Fact]
    public void CreateDefault_ReturnsExpectedShape()
    {
        var config = UserConfigStore.CreateDefault();

        config.Version.Should().Be(1);
        config.ActiveProvider.Should().BeNull();
        config.Providers.Keys.Should().BeEquivalentTo(["onshape", "inventor", "solidworks"]);
        foreach (var key in new[] { "onshape", "inventor", "solidworks" })
        {
            config.Providers[key].Enabled.Should().BeFalse();
            config.Providers[key].Credentials.Should().BeNull();
        }

        config.Agents.Should().BeEmpty();
    }

    [Fact]
    public void RoundTrip_SerializeDeserialize_IsEquivalent()
    {
        var original = UserConfigStore.CreateDefault();
        original.ActiveProvider = "inventor";
        original.Providers["onshape"].Enabled = true;
        original.Providers["onshape"].Credentials = new Dictionary<string, string>
        {
            ["access_key"] = "a",
            ["secret_key"] = "s"
        };
        original.Agents.Add(new AgentConfig { Name = "VS Code", Url = "vscode://mcp" });

        var json = JsonSerializer.Serialize(original, Options);
        var restored = JsonSerializer.Deserialize<UserConfig>(json, Options);

        restored.Should().BeEquivalentTo(original);
    }
}