using System.Reflection;
using McpCad.Core;
using McpCad.Core.Models;
using McpCad.Server;
using McpCad.Tools;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Server;

namespace McpCad.Tests.Tools;

public class SelectProviderToolTests : IDisposable
{
    public SelectProviderToolTests()
    {
        CadExecutionContext.ResetForTests();
    }

    public void Dispose() => CadExecutionContext.ResetForTests();

    [Fact]
    public void SelectProvider_Inventor_Sets_CadExecutionContext_SelectedProvider()
    {
        var tool = new SelectProviderTool();

        var result = tool.mcp_cad_select_provider("inventor");

        ((bool)result["success"]!).Should().BeTrue();
        CadExecutionContext.Current.SelectedProvider.Should().Be("inventor");
    }

    [Fact]
    public void SelectProvider_Onshape_Sets_Context()
    {
        var tool = new SelectProviderTool();

        var result = tool.mcp_cad_select_provider("onshape");

        ((bool)result["success"]!).Should().BeTrue();
        CadExecutionContext.Current.SelectedProvider.Should().Be("onshape");
    }

    [Fact]
    public void SelectProvider_Unknown_Returns_Error()
    {
        var tool = new SelectProviderTool();

        var result = tool.mcp_cad_select_provider("unknown");

        ((bool)result["success"]!).Should().BeFalse();
        result["error"].Should().NotBeNull();
        CadExecutionContext.Current.SelectedProvider.Should().BeNull();
    }

    [Fact]
    public void Subsequent_Tool_Reads_Context_From_AsyncLocal()
    {
        var tool = new SelectProviderTool();
        tool.mcp_cad_select_provider("solidworks");

        string? read = CadExecutionContext.Current.SelectedProvider;
        read.Should().Be("solidworks");
    }

    [Fact]
    public void SelectProvider_Does_Not_Persist_To_UserConfig_File()
    {
        var dir = Path.Combine(Path.GetTempPath(), "mcp-cad-select-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, "config.json");
        var config = UserConfigStore.CreateDefault();
        config.ActiveProvider = "onshape";
        UserConfigStore.Save(config, path);

        var tool = new SelectProviderTool();
        tool.mcp_cad_select_provider("inventor");

        UserConfigStore.Load(path)!.ActiveProvider.Should().Be("onshape");

        try { Directory.Delete(dir, recursive: true); } catch { /* ignore */ }
    }

    [Fact]
    public void SelectProviderTool_Has_McpServerTool_On_Select_Method()
    {
        var method = typeof(SelectProviderTool).GetMethod(
            nameof(SelectProviderTool.mcp_cad_select_provider),
            BindingFlags.Public | BindingFlags.Instance);

        method.Should().NotBeNull();
        method!.GetCustomAttribute<McpServerToolAttribute>().Should().NotBeNull();
    }

    [Fact]
    public void RegisterAllProviders_Registers_SelectProviderTool()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Provider:Active"] = "onshape",
                ["Onshape:BaseUrl"] = "https://cad.onshape.com",
            })
            .Build();

        ProviderBootstrap.RegisterAllProviders(services, configuration, userConfig: null);
        var sp = services.BuildServiceProvider();

        sp.GetRequiredService<SelectProviderTool>().Should().NotBeNull();
    }
}