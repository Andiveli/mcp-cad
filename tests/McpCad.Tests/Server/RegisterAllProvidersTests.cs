using McpCad.Core;
using McpCad.Core.Exceptions;
using McpCad.Core.Models;
using McpCad.Server;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace McpCad.Tests.Server;

public class RegisterAllProvidersTests
{
    [Fact]
    public void RegisterAllProviders_Registers_IProviderRouter_And_All_Names()
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

        ProviderBootstrap.RegisterAllProviders(services, configuration, userConfig: null);
        var sp = services.BuildServiceProvider();
        var router = sp.GetRequiredService<IProviderRouter>();

        router.AvailableProviders.Should().BeEquivalentTo("inventor", "onshape", "solidworks");
        router.CurrentProvider.Should().Be("onshape");
    }

#if !MCP_CAD_WINDOWS
    [Fact]
    public void RegisterAllProviders_Inventor_Resolve_On_Linux_Throws_Platform_Error()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Provider:Active"] = "onshape" })
            .Build();
        ProviderBootstrap.RegisterAllProviders(services, configuration, null);
        var sp = services.BuildServiceProvider();
        var router = sp.GetRequiredService<IProviderRouter>();

        var act = () => router.Resolve("inventor");
        act.Should().Throw<ProviderConnectionException>()
            .WithMessage("*Windows*");
    }
#endif

    [Fact]
    public void RegisterAllProviders_SolidWorks_Resolve_Throws_Not_Implemented_Or_Platform()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Provider:Active"] = "onshape" })
            .Build();
        ProviderBootstrap.RegisterAllProviders(services, configuration, null);
        var sp = services.BuildServiceProvider();
        var router = sp.GetRequiredService<IProviderRouter>();

        var act = () => router.Resolve("solidworks");
        act.Should().Throw<ProviderConnectionException>();
    }
}
