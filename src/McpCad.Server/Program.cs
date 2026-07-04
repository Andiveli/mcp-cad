using McpCad.Core;
using McpCad.Server;
using McpCad.Tools;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var builder = Host.CreateApplicationBuilder(args);
builder.Configuration.AddUserSecrets<McpCad.Server.ProgramMarker>(optional: true);

builder.Logging.AddConsole(consoleLogOptions =>
{
    consoleLogOptions.LogToStandardErrorThreshold = LogLevel.Trace;
});

var userConfig = UserConfigStore.Load();
var activeProvider = ProviderBootstrap.ResolveEffectiveActiveProvider(builder.Configuration, userConfig);
ProviderBootstrap.RegisterAllProviders(builder.Services, builder.Configuration, userConfig);
ProviderBootstrap.ValidateAllProvidersToolRegistration(activeProvider);

var mcp = builder.Services.AddMcpServer().WithStdioServerTransport();
mcp.WithTools<AtomicTools>()
    .WithTools<SkillTools>()
    .WithTools<WebCadTools>()
    .WithTools<SelectProviderTool>();

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