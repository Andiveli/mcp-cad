using McpCad.Core;
using McpCad.Inventor;
using McpCad.Tools;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Reflection;

var builder = Host.CreateApplicationBuilder(args);

// Route all logging to stderr so it doesn't interfere with MCP stdio transport
builder.Logging.AddConsole(consoleLogOptions =>
{
    consoleLogOptions.LogToStandardErrorThreshold = LogLevel.Trace;
});

// Config-driven provider selection (Cad:Provider). Default "Inventor" for zero breakage.
// Valid: "Inventor" | "SolidWorks" (case-insensitive).
var cadSection = builder.Configuration.GetSection("Cad");
string providerName = cadSection.GetValue<string>("Provider") ?? "Inventor";
providerName = providerName?.Trim() ?? "Inventor";

bool isSolidWorks = string.Equals(providerName, "SolidWorks", StringComparison.OrdinalIgnoreCase);
bool isInventor = string.Equals(providerName, "Inventor", StringComparison.OrdinalIgnoreCase) || string.IsNullOrWhiteSpace(providerName);

if (isSolidWorks)
{
    // SolidWorks path via reflection so the server compiles when SW csproj is conditioned out.
    try
    {
        var swAsm = Assembly.Load("McpCad.SolidWorks");
        var driverType = swAsm.GetType("McpCad.SolidWorks.SolidWorksDriver", throwOnError: true);
        var provType = swAsm.GetType("McpCad.SolidWorks.SolidWorksProvider", throwOnError: true);
        builder.Services.AddSingleton(driverType);
        builder.Services.AddSingleton(typeof(IMechanicalCadProvider), provType);
    }
    catch (Exception ex)
    {
        throw new InvalidOperationException($"SolidWorks provider assembly not available (build with SolidWorks interop or run on machine with SolidWorks). {ex.Message}");
    }
}
else if (isInventor)
{
    // Inventor (default path)
    builder.Services.AddSingleton<InventorDriver>();
    builder.Services.AddSingleton<IMechanicalCadProvider, InventorProvider>();
}
else
{
    throw new InvalidOperationException($"Invalid Cad:Provider '{providerName}'. Valid: Inventor, SolidWorks");
}

builder.Services.AddSingleton<ICadProvider>(sp => sp.GetRequiredService<IMechanicalCadProvider>());

// Register MCP server with stdio transport.
builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithTools<AtomicTools>()
    .WithTools<SkillTools>();

var app = builder.Build();

// Provider-aware auto-connect on startup
var cfg = app.Services.GetRequiredService<IConfiguration>();
bool autoConnect = false;
if (isInventor)
{
    autoConnect = cfg.GetValue<bool>("Inventor:AutoConnect");
}
else if (isSolidWorks)
{
    autoConnect = cfg.GetValue<bool>("SolidWorks:AutoConnect") || cfg.GetValue<bool>("Cad:AutoConnect");
}

if (autoConnect)
{
    if (isInventor)
    {
        var driver = app.Services.GetRequiredService<InventorDriver>();
        driver.Connect();
    }
    else if (isSolidWorks)
    {
        var driver = app.Services.GetRequiredService<SolidWorks.SolidWorksDriver>();
        driver.Connect();
    }
}

await app.RunAsync();
