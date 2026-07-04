using McpCad.Core;
using McpCad.Core.Exceptions;
using McpCad.Core.Models;
using McpCad.Onshape;
using McpCad.Tools;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace McpCad.Server;

public static partial class ProviderBootstrap
{
    /// <summary>Phase 2: lazy multi-provider registry and <see cref="IProviderRouter"/>.</summary>
    public static void RegisterAllProviders(
        IServiceCollection services,
        IConfiguration configuration,
        UserConfig? userConfig)
    {
        RegisterOnshapeInfrastructure(services, configuration, userConfig);
#if WINDOWS
        RegisterInventorInfrastructure(services, configuration);
#endif

        services.AddSingleton<IProviderRouter>(sp =>
        {
            var registry = BuildLazyProviderRegistry(sp, configuration, userConfig);
            var defaultProvider = ResolveEffectiveActiveProvider(configuration, userConfig);
            return new ProviderRouter(registry, defaultProvider, userConfig);
        });

        services.AddSingleton<AtomicTools>();
        services.AddSingleton<SkillTools>();
        services.AddSingleton<WebCadTools>();
        services.AddSingleton<SelectProviderTool>();
    }

    public static IReadOnlyDictionary<string, Lazy<ICadProvider>> BuildLazyProviderRegistry(
        IServiceProvider sp,
        IConfiguration configuration,
        UserConfig? userConfig)
    {
        var registry = new Dictionary<string, Lazy<ICadProvider>>(StringComparer.OrdinalIgnoreCase)
        {
            ["onshape"] = new Lazy<ICadProvider>(
                () => sp.GetRequiredService<OnshapeProvider>(),
                LazyThreadSafetyMode.ExecutionAndPublication),
            ["solidworks"] = new Lazy<ICadProvider>(
                () => CreateSolidWorksProvider(),
                LazyThreadSafetyMode.ExecutionAndPublication),
        };

#if WINDOWS
        registry["inventor"] = new Lazy<ICadProvider>(
            () => sp.GetRequiredService<McpCad.Inventor.InventorProvider>(),
            LazyThreadSafetyMode.ExecutionAndPublication);
#else
        registry["inventor"] = new Lazy<ICadProvider>(
            () => throw new ProviderConnectionException(
                "Provider 'inventor' requires Windows with Autodesk Inventor. Use 'onshape' on Linux."),
            LazyThreadSafetyMode.ExecutionAndPublication);
#endif

        return registry;
    }

    private static ICadProvider CreateSolidWorksProvider()
    {
#if WINDOWS
        throw new ProviderConnectionException(
            "SolidWorks provider is not implemented yet. Selection is saved for future compatibility.");
#else
        throw new ProviderConnectionException("SolidWorks requires Windows.");
#endif
    }

    private static void RegisterOnshapeInfrastructure(
        IServiceCollection services,
        IConfiguration configuration,
        UserConfig? userConfig)
    {
        var section = configuration.GetSection("Onshape");
        var baseUrl = section["BaseUrl"] ?? OnshapeClient.DefaultBaseUrl;
        var accessKey = section["AccessKey"] ?? Environment.GetEnvironmentVariable("ONSHAPE_ACCESS_KEY") ?? "";
        var secretKey = section["SecretKey"] ?? Environment.GetEnvironmentVariable("ONSHAPE_SECRET_KEY") ?? "";
        var secretKeyId = section["SecretKeyId"] ?? Environment.GetEnvironmentVariable("ONSHAPE_SECRET_KEY_ID") ?? "";

        if (userConfig?.Providers.TryGetValue("onshape", out var onshapeCfg) == true
            && onshapeCfg.Credentials is { } creds)
        {
            if (creds.TryGetValue("access_key", out var ak) && !string.IsNullOrWhiteSpace(ak))
                accessKey = ak;
            if (creds.TryGetValue("secret_key", out var sk) && !string.IsNullOrWhiteSpace(sk))
                secretKey = sk;
            if (creds.TryGetValue("secret_key_id", out var skid) && !string.IsNullOrWhiteSpace(skid))
                secretKeyId = skid;
        }

        services.AddHttpClient(nameof(OnshapeClient), client =>
        {
            client.BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/");
        });

        services.AddSingleton(sp =>
        {
            var factory = sp.GetRequiredService<IHttpClientFactory>();
            var http = factory.CreateClient(nameof(OnshapeClient));
            return new OnshapeClient(http, accessKey, secretKey, baseUrl);
        });

        services.AddSingleton(sp =>
        {
            var client = sp.GetRequiredService<OnshapeClient>();
            var driver = new OnshapeDriver(client);
            if (!string.IsNullOrWhiteSpace(accessKey) && !string.IsNullOrWhiteSpace(secretKey))
                driver.ApplyStartupCredentials(accessKey, secretKeyId ?? "", secretKey);
            return driver;
        });

        services.AddSingleton<OnshapeProvider>();
        services.AddSingleton<ICadWebProvider>(sp => sp.GetRequiredService<OnshapeProvider>());
    }

#if WINDOWS
    private static void RegisterInventorInfrastructure(IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<McpCad.Inventor.InventorDriver>();
        services.AddSingleton<McpCad.Inventor.InventorProvider>();
        services.AddSingleton<IMechanicalCadProvider>(sp => sp.GetRequiredService<McpCad.Inventor.InventorProvider>());
    }
#endif

    /// <summary>MCP tool collision check for unified converged registration (PR 2b).</summary>
    public static void ValidateAllProvidersToolRegistration(string activeProvider)
    {
        _ = activeProvider;
        ToolRegistrationValidator.EnsureNoCollisions(
        typeof(AtomicTools),
        typeof(SkillTools),
        typeof(WebCadTools),
        typeof(SelectProviderTool));
    }
}