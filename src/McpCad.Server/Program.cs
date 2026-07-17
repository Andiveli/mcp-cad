using McpCad.Server;
using McpCad.Tools.Onshape;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
#if WINDOWS
using McpCad.Tools;
#endif

var builder = Host.CreateApplicationBuilder(args);
builder.Configuration.AddUserSecrets<McpCad.Server.ProgramMarker>(optional: true);

var userConfigOverlay = new Dictionary<string, string?>(StringComparer.Ordinal);
UserMcpCadConfig.TryLoad()?.ApplyToConfiguration(userConfigOverlay);
if (userConfigOverlay.Count > 0)
{
    builder.Configuration.AddInMemoryCollection(userConfigOverlay);
}

builder.Logging.AddConsole(consoleLogOptions =>
{
    consoleLogOptions.LogToStandardErrorThreshold = LogLevel.Trace;
});

var activeProvider = ProviderBootstrap.ResolveActiveProvider(builder.Configuration);
ProviderBootstrap.RegisterCadProvider(builder.Services, builder.Configuration);
ProviderBootstrap.ValidateToolRegistration(activeProvider);

var mcp = builder.Services.AddMcpServer().WithStdioServerTransport();

if (activeProvider.Equals("onshape", StringComparison.OrdinalIgnoreCase))
{
    mcp.WithTools<OnshapeAtomicTools>().WithTools<OnshapeSkillTools>();
}
#if WINDOWS
else if (activeProvider.Equals("inventor", StringComparison.OrdinalIgnoreCase))
{
    mcp.WithTools<AtomicTools>().WithTools<SkillTools>();
}
#endif

var app = builder.Build();

#if WINDOWS
if (activeProvider.Equals("inventor", StringComparison.OrdinalIgnoreCase)
    && app.Services.GetRequiredService<IConfiguration>().GetValue<bool>("Inventor:AutoConnect"))
{
    var driver = app.Services.GetRequiredService<McpCad.Inventor.InventorDriver>();
    driver.Connect();
}
#endif

await app.RunAsync();