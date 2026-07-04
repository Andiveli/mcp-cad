using McpCad.Core.Models;
using Microsoft.Extensions.Configuration;

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

    /// <summary>User <c>config.json</c> wins when <see cref="UserConfig.ActiveProvider"/> is set.</summary>
    public static string ResolveEffectiveActiveProvider(IConfiguration configuration, UserConfig? userConfig)
    {
        if (!string.IsNullOrWhiteSpace(userConfig?.ActiveProvider))
            return userConfig.ActiveProvider.Trim();

        var legacyCad = configuration["Cad:Provider"];
        if (!string.IsNullOrWhiteSpace(legacyCad))
        {
            return legacyCad.Trim() switch
            {
                "SolidWorks" => "solidworks",
                "Inventor" => "inventor",
                _ => legacyCad.Trim().ToLowerInvariant(),
            };
        }

        return ResolveActiveProvider(configuration);
    }
}