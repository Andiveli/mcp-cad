using McpCad.Core;
using McpCad.Core.Models;
using McpCad.Server;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace McpCad.Tests.Server;

public class ConfigLoadTests
{
    [Fact]
    public void ResolveEffectiveActiveProvider_Uses_UserConfig_When_Onshape_Set()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Provider:Active"] = "inventor",
            })
            .Build();

        var userConfig = new UserConfig { ActiveProvider = "onshape" };

        var effective = ProviderBootstrap.ResolveEffectiveActiveProvider(configuration, userConfig);

        effective.Should().Be("onshape");
    }

    [Fact]
    public void ResolveEffectiveActiveProvider_Falls_Back_To_Appsettings_When_Config_Missing()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Provider:Active"] = "onshape",
            })
            .Build();

        var effective = ProviderBootstrap.ResolveEffectiveActiveProvider(configuration, userConfig: null);

        effective.Should().Be("onshape");
    }

    [Fact]
    public void ResolveEffectiveActiveProvider_Falls_Back_When_ActiveProvider_Null()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Provider:Active"] = "onshape",
            })
            .Build();

        var userConfig = new UserConfig { ActiveProvider = null };

        var effective = ProviderBootstrap.ResolveEffectiveActiveProvider(configuration, userConfig);

        effective.Should().Be("onshape");
    }

    [Fact]
    public void ResolveEffectiveActiveProvider_UserConfig_Wins_Over_Appsettings()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Provider:Active"] = "inventor",
            })
            .Build();

        var userConfig = new UserConfig { ActiveProvider = "onshape" };

        var effective = ProviderBootstrap.ResolveEffectiveActiveProvider(configuration, userConfig);

        effective.Should().NotBe(ProviderBootstrap.ResolveActiveProvider(configuration));
        effective.Should().Be("onshape");
    }

    [Fact]
    public void RegisterCadProvider_Onshape_Uses_Onshape_Credentials_From_UserConfig()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Provider:Active"] = "inventor",
                ["Onshape:BaseUrl"] = "https://cad.onshape.com",
                ["Onshape:AccessKey"] = "from-appsettings",
            })
            .Build();

        var userConfig = new UserConfig
        {
            ActiveProvider = "onshape",
            Providers =
            {
                ["onshape"] = new ProviderConfig
                {
                    Enabled = true,
                    Credentials = new Dictionary<string, string>
                    {
                        ["access_key"] = "user-access",
                        ["secret_key"] = "user-secret",
                        ["secret_key_id"] = "user-skid",
                    },
                },
            },
        };

        ProviderBootstrap.RegisterAllProviders(services, configuration, userConfig);
        var sp = services.BuildServiceProvider();
        var driver = sp.GetRequiredService<McpCad.Onshape.OnshapeDriver>();

        driver.AccessKey.Should().Be("user-access");
    }
}