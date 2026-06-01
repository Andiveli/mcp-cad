using McpCad.Core;
using McpCad.Inventor;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var builder = Host.CreateApplicationBuilder(args);

// Route all logging to stderr so it doesn't interfere with MCP stdio transport
builder.Logging.AddConsole(consoleLogOptions =>
{
    consoleLogOptions.LogToStandardErrorThreshold = LogLevel.Trace;
});

// Register the Inventor-backed ICadProvider as a singleton.
// The driver connects lazily — no Inventor required at startup.
builder.Services.AddSingleton<InventorDriver>();
builder.Services.AddSingleton<ICadProvider, InventorProvider>();

// Register MCP server with stdio transport.
// WithToolsFromAssembly() will discover [McpServerToolType] classes
// from referenced projects automatically.
builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithToolsFromAssembly();

await builder.Build().RunAsync();