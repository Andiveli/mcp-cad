#if WINDOWS
using McpCad.Core;
using McpCad.Inventor;
using McpCad.Tools;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace McpCad.Server;

public static partial class ProviderBootstrap
{
    private static void RegisterInventor(IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<InventorDriver>();
        services.AddSingleton<IMechanicalCadProvider, InventorProvider>();
        services.AddSingleton<ICadProvider>(sp => sp.GetRequiredService<IMechanicalCadProvider>());
        services.AddSingleton<AtomicTools>();
        services.AddSingleton<SkillTools>();
    }
}
#endif