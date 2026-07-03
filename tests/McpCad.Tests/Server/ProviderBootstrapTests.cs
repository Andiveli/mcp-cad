using McpCad.Core;
using McpCad.Core.Exceptions;
using McpCad.Server;
#if MCP_CAD_WINDOWS
using McpCad.Inventor;
#endif
using McpCad.Tools;
using McpCad.Tools.Onshape;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace McpCad.Tests.Server;

public class ProviderBootstrapTests
{
    [Fact]
    public void ResolveActiveProvider_Defaults_To_Inventor_When_Missing()
    {
        var config = new ConfigurationBuilder().Build();
        Assert.Equal("inventor", ProviderBootstrap.ResolveActiveProvider(config));
    }

#if MCP_CAD_WINDOWS
    [Fact]
    public void RegisterCadProvider_Inventor_Resolves_AtomicTools_Not_Onshape()
    {
        var services = new ServiceCollection();
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Provider:Active"] = "inventor",
            })
            .Build();

        ProviderBootstrap.RegisterCadProvider(services, config);
        var sp = services.BuildServiceProvider();

        Assert.IsType<AtomicTools>(sp.GetRequiredService<AtomicTools>());
        Assert.IsType<InventorProvider>(sp.GetRequiredService<IMechanicalCadProvider>());
        Assert.Throws<InvalidOperationException>(() => sp.GetRequiredService<OnshapeAtomicTools>());
    }
#else
    [Fact]
    public void RegisterCadProvider_Inventor_OnNonWindows_Throws_ConfigurationException()
    {
        var services = new ServiceCollection();
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Provider:Active"] = "inventor",
            })
            .Build();

        Assert.Throws<ConfigurationException>(() => ProviderBootstrap.RegisterCadProvider(services, config));
    }
#endif

    [Fact]
    public void RegisterCadProvider_Onshape_Resolves_NullMechanical_And_OnshapeTools()
    {
        var services = new ServiceCollection();
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Provider:Active"] = "onshape",
                ["Onshape:BaseUrl"] = "https://cad.onshape.com",
            })
            .Build();

        ProviderBootstrap.RegisterCadProvider(services, config);
        var sp = services.BuildServiceProvider();

        Assert.IsType<NullMechanicalCadProvider>(sp.GetRequiredService<IMechanicalCadProvider>());
        Assert.IsType<OnshapeAtomicTools>(sp.GetRequiredService<OnshapeAtomicTools>());
        Assert.IsAssignableFrom<ICadWebProvider>(sp.GetRequiredService<ICadWebProvider>());
    }

    [Fact]
    public void RegisterCadProvider_Unknown_Throws_ConfigurationException()
    {
        var services = new ServiceCollection();
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Provider:Active"] = "Fusion360" })
            .Build();

        Assert.Throws<ConfigurationException>(() => ProviderBootstrap.RegisterCadProvider(services, config));
    }

    [Fact]
    public void NullMechanicalCadProvider_SketchCreate_Returns_SuccessFalse_In_Onshape_Mode()
    {
        var services = new ServiceCollection();
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Provider:Active"] = "onshape",
                ["Onshape:BaseUrl"] = "https://cad.onshape.com",
            })
            .Build();

        ProviderBootstrap.RegisterCadProvider(services, config);
        var sp = services.BuildServiceProvider();
        var mechanical = sp.GetRequiredService<IMechanicalCadProvider>();

        var result = mechanical.SketchCreate("XY");

        Assert.False((bool)result["success"]!);
        Assert.NotNull(result["error"]);
    }
}