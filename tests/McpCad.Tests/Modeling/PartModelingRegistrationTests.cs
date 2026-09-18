using McpCad.Core.Modeling;
using McpCad.Server;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace McpCad.Tests.Modeling;

public class PartModelingRegistrationTests
{
    [Fact]
    public void RegisterCadProvider_Onshape_RegistersFactoryThatCreatesFreshUnsupportedContexts()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Provider:Active"] = "onshape",
                ["Onshape:BaseUrl"] = "https://cad.onshape.com",
            })
            .Build();

        ProviderBootstrap.RegisterCadProvider(services, configuration);
        var provider = services.BuildServiceProvider();

        var factory = provider.GetRequiredService<IPartModelingContextFactory>();
        var first = factory.Bind(new("document", "workspace", null, "element"));
        var second = factory.Bind(new("document", "workspace", null, "element"));

        first.Should().NotBeSameAs(second);
        first.Capabilities.Should().Be(new PartModelingCapabilities(
            OptionalCapabilityStatus.Unsupported,
            OptionalCapabilityStatus.Unsupported));
    }

    [Fact]
    public void RegisterCadProvider_Onshape_KeepsExistingToolFamilyAlongsideModelingFactory()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Provider:Active"] = "onshape",
                ["Onshape:BaseUrl"] = "https://cad.onshape.com",
            })
            .Build();

        ProviderBootstrap.RegisterCadProvider(services, configuration);
        var provider = services.BuildServiceProvider();

        provider.GetRequiredService<McpCad.Tools.Onshape.OnshapeAtomicTools>().Should().NotBeNull();
        provider.GetRequiredService<IPartModelingContextFactory>().Should().NotBeNull();
    }
}
