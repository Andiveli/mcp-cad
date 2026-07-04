using McpCad.Core;
using McpCad.Server;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace McpCad.Tests.Server;

public class ProviderRegistrationTests
{
    [Fact]
    public void RegisterAllProviders_Does_Not_Register_IMechanicalCadProvider_Singleton()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Provider:Active"] = "onshape",
                ["Onshape:AccessKey"] = "ak",
                ["Onshape:SecretKey"] = "sk",
            })
            .Build();

        ProviderBootstrap.RegisterAllProviders(services, configuration, null);
        var sp = services.BuildServiceProvider();

        Assert.Null(sp.GetService<IMechanicalCadProvider>());
    }

    [Fact]
    public void ResolveEffectiveActiveProvider_Maps_Cad_Provider_SolidWorks()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Cad:Provider"] = "SolidWorks" })
            .Build();

        var active = ProviderBootstrap.ResolveEffectiveActiveProvider(configuration, userConfig: null);

        Assert.Equal("solidworks", active);
    }

    [Fact]
    public void ResolveEffectiveActiveProvider_Maps_Cad_Provider_Inventor()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Cad:Provider"] = "Inventor" })
            .Build();

        var active = ProviderBootstrap.ResolveEffectiveActiveProvider(configuration, userConfig: null);

        Assert.Equal("inventor", active);
    }
}