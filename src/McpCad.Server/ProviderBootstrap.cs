using McpCad.Core;
using McpCad.Core.Exceptions;
using McpCad.Core.Modeling;
using McpCad.Onshape;
using McpCad.Onshape.Modeling;
using McpCad.Tools;
using McpCad.Tools.Onshape;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace McpCad.Server;

public static partial class ProviderBootstrap
{
    public const string DefaultActiveProvider = "inventor";

    public static string ResolveActiveProvider(IConfiguration configuration)
    {
        var active = configuration["Provider:Active"];
        if (string.IsNullOrWhiteSpace(active))
        {
            return DefaultActiveProvider;
        }

        return active.Trim();
    }

    public static void RegisterCadProvider(IServiceCollection services, IConfiguration configuration)
    {
        var active = ResolveActiveProvider(configuration);
        var normalized = active.ToLowerInvariant();

        switch (normalized)
        {
#if WINDOWS
            case "inventor":
                RegisterInventor(services, configuration);
                return;
#else
            case "inventor":
                throw new ConfigurationException(
                "Provider:Active 'inventor' requires Windows with Autodesk Inventor. Use 'onshape' on Linux.");
#endif
            case "onshape":
                RegisterOnshape(services, configuration);
                return;
            default:
                throw new ConfigurationException(
                    $"Unknown Provider:Active value '{active}'. Supported: inventor, onshape.");
        }
    }

    public static void ValidateToolRegistration(string activeProvider)
    {
        var normalized = activeProvider.ToLowerInvariant();
#if WINDOWS
        if (normalized == "inventor")
        {
            ToolRegistrationValidator.EnsureNoCollisions(typeof(AtomicTools), typeof(SkillTools));
            return;
        }
#endif

        if (normalized == "onshape")
        {
            ToolRegistrationValidator.EnsureNoCollisions(typeof(OnshapeAtomicTools), typeof(OnshapeSkillTools));
        }
    }

    private static void RegisterOnshape(IServiceCollection services, IConfiguration configuration)
    {
        var section = configuration.GetSection("Onshape");
        var baseUrl = section["BaseUrl"] ?? OnshapeClient.DefaultBaseUrl;
        var accessKey = section["AccessKey"] ?? Environment.GetEnvironmentVariable("ONSHAPE_ACCESS_KEY") ?? "";
        var secretKey = section["SecretKey"] ?? Environment.GetEnvironmentVariable("ONSHAPE_SECRET_KEY") ?? "";
        var secretKeyId = section["SecretKeyId"] ?? Environment.GetEnvironmentVariable("ONSHAPE_SECRET_KEY_ID") ?? "";

        services.AddHttpClient(nameof(OnshapeClient), client =>
        {
            client.BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/");
        }).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });

            services.AddSingleton(sp =>
            {
                var factory = sp.GetRequiredService<IHttpClientFactory>();
                var http = factory.CreateClient(nameof(OnshapeClient));
                return new OnshapeClient(http, accessKey, secretKey, baseUrl);
            });

        services.AddSingleton(sp =>
        {
            var driver = new OnshapeDriver(sp.GetRequiredService<OnshapeClient>());
            var configuredWvm = section["DefaultWvm"];
            if (configuredWvm is not null)
            {
                driver.DefaultWvm = configuredWvm;
            }

            return driver;
        });
        services.AddSingleton<OnshapeProvider>();
        services.AddSingleton<IPartModelingContextFactory>(sp => new OnshapePartModelingContextFactory(
            sp.GetRequiredService<OnshapeClient>(), sp.GetRequiredService<OnshapeDriver>()));
        services.AddSingleton<ICadWebProvider>(sp => sp.GetRequiredService<OnshapeProvider>());
        services.AddSingleton<ICadProvider>(sp => sp.GetRequiredService<OnshapeProvider>());
        services.AddSingleton<IMechanicalCadProvider, NullMechanicalCadProvider>();
        services.AddSingleton<OnshapeAtomicTools>();
        services.AddSingleton<OnshapeSkillTools>();
    }
}
