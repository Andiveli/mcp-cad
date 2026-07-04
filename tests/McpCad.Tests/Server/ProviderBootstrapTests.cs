using McpCad.Core;
using McpCad.Core.Exceptions;
using McpCad.Core.Models;
using McpCad.Server;
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
    public void RegisterAllProviders_Inventor_Default_Resolves_Router_And_Unified_Tools()
    {
        var services = new ServiceCollection();
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Provider:Active"] = "inventor",
            })
            .Build();

        ProviderBootstrap.RegisterAllProviders(services, config, userConfig: null);
        var sp = services.BuildServiceProvider();

        Assert.IsType<AtomicTools>(sp.GetRequiredService<AtomicTools>());
        Assert.IsType<SkillTools>(sp.GetRequiredService<SkillTools>());
        Assert.Throws<InvalidOperationException>(() => sp.GetRequiredService<OnshapeAtomicTools>());
        var router = sp.GetRequiredService<IProviderRouter>();
        Assert.Equal("inventor", router.CurrentProvider);
        Assert.IsType<McpCad.Inventor.InventorProvider>(router.Resolve("inventor"));
    }
#else
    [Fact]
    public void RegisterAllProviders_Inventor_As_Default_On_Linux_Does_Not_Throw_At_Registration()
    {
        var services = new ServiceCollection();
        var config = new ConfigurationBuilder().Build();

        ProviderBootstrap.RegisterAllProviders(services, config, null);
        var sp = services.BuildServiceProvider();
        var router = sp.GetRequiredService<IProviderRouter>();

        Assert.Equal("inventor", router.CurrentProvider);
        Assert.Throws<ProviderConnectionException>(() => router.Resolve("inventor"));
    }
#endif

    [Fact]
    public void RegisterAllProviders_Onshape_Resolves_WebProvider_And_Unified_Tools_Not_Legacy_OnshapeTools()
    {
        var services = new ServiceCollection();
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Provider:Active"] = "onshape",
                ["Onshape:BaseUrl"] = "https://cad.onshape.com",
            })
            .Build();

        ProviderBootstrap.RegisterAllProviders(services, config, null);
        var sp = services.BuildServiceProvider();

        Assert.Null(sp.GetService<IMechanicalCadProvider>());
        Assert.Throws<InvalidOperationException>(() => sp.GetRequiredService<OnshapeAtomicTools>());
        Assert.IsType<AtomicTools>(sp.GetRequiredService<AtomicTools>());
        var router = sp.GetRequiredService<IProviderRouter>();
        Assert.IsAssignableFrom<ICadWebProvider>(router.Resolve("onshape"));
    }

    [Fact]
    public void RegisterAllProviders_Unknown_Active_In_Config_Still_Registers_Router()
    {
        var services = new ServiceCollection();
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Provider:Active"] = "Fusion360" })
            .Build();

        ProviderBootstrap.RegisterAllProviders(services, config, null);
        var sp = services.BuildServiceProvider();
        var router = sp.GetRequiredService<IProviderRouter>();

        Assert.Equal("fusion360", router.CurrentProvider);
    }

    [Fact]
    public void Onshape_Provider_Via_Router_Does_Not_Implement_IMechanicalCadProvider()
    {
        var services = new ServiceCollection();
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Provider:Active"] = "onshape",
                ["Onshape:BaseUrl"] = "https://cad.onshape.com",
            })
            .Build();

        ProviderBootstrap.RegisterAllProviders(services, config, null);
        var sp = services.BuildServiceProvider();
        var router = sp.GetRequiredService<IProviderRouter>();
        var onshape = router.Resolve("onshape");

        Assert.False(onshape is IMechanicalCadProvider);
    }
}