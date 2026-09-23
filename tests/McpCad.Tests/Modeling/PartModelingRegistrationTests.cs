using McpCad.Core.Modeling;
using McpCad.Onshape.Modeling;
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
        provider.GetRequiredService<McpCad.Tools.Onshape.OnshapeSkillTools>().Should().NotBeNull();
    }

    [Theory]
    [InlineData("w")]
    [InlineData("v")]
    [InlineData("m")]
    [InlineData("unsupported")]
    public void RegisterCadProvider_Onshape_ExposesTheEffectiveConfiguredWvmThroughItsFactoryCompatibilitySeam(string wvm)
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Provider:Active"] = "onshape",
                ["Onshape:BaseUrl"] = "https://cad.onshape.com",
                ["Onshape:DefaultWvm"] = wvm,
            })
            .Build();

        ProviderBootstrap.RegisterCadProvider(services, configuration);
        var provider = services.BuildServiceProvider();

        var factory = provider.GetRequiredService<IPartModelingContextFactory>();

        factory.Should().BeAssignableTo<IOnshapePartModelingContextFactoryCompatibility>()
            .Which.EffectiveWvm.Should().Be(wvm);
    }

    [Theory]
    [InlineData("w", true)]
    [InlineData("v", false)]
    [InlineData("m", false)]
    [InlineData("W", false)]
    [InlineData("", false)]
    [InlineData(" ", false)]
    [InlineData(" w", false)]
    [InlineData("w ", false)]
    [InlineData("unknown", false)]
    public void OnshapeFactoryWorkspaceCompatibility_OnlyAcceptsExactWorkspaceOrdinal(string? wvm, bool expected) =>
        OnshapePartModelingContextFactory.IsWorkspaceCompatibleWvm(wvm).Should().Be(expected);

    [Fact]
    public void OnshapeReceipt_PreservesNativeStageIdsOutsidePortableModelingContracts()
    {
        var receipt = new OnshapePartModelingReceipt("native-sketch", "native-extrude");

        receipt.SketchNativeFeatureId.Should().Be("native-sketch");
        receipt.ExtrusionNativeFeatureId.Should().Be("native-extrude");
        typeof(IPartModelingContext).GetProperties()
            .Select(property => property.Name)
            .Should().NotContain(name => name.Contains("Native", StringComparison.Ordinal));
    }
}
