using System.Reflection;
using System.Text.Json;
using McpCad.Core.Models;
using McpCad.Tests.Mocks;
using McpCad.Tools.Onshape;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace McpCad.Tests.Tools;

public sealed class OnshapeCaptureViewMcpAdapterTests
{
    [Fact]
    public async Task onshape_capture_view_McpAdapterReturnsNativeImageContentAndStructuredMetadata()
    {
        var tools = new OnshapeAtomicTools(new MockOnshapeProvider());
        var method = typeof(OnshapeAtomicTools).GetMethod(nameof(OnshapeAtomicTools.onshape_capture_view), BindingFlags.Public | BindingFlags.Instance)!;
        var adapter = McpServerTool.Create(method, tools);
        await using var transport = new StreamServerTransport(Stream.Null, Stream.Null, "test", null);
        await using var server = McpServer.Create(transport, new McpServerOptions());
        var request = new RequestContext<CallToolRequestParams>(
            server,
            new JsonRpcRequest { Method = RequestMethods.ToolsCall },
            new CallToolRequestParams
            {
                Name = "onshape_capture_view",
                Arguments = new Dictionary<string, JsonElement>
                {
                    ["address_json"] = JsonSerializer.SerializeToElement(new CadElementAddress("doc-1", "ws-1", null, "e-part").Serialize()),
                },
            });

        var result = await adapter.InvokeAsync(request);

        result.Content.Should().ContainSingle().Which.Should().BeOfType<ImageContentBlock>()
            .Which.MimeType.Should().Be("image/png");
        result.StructuredContent!.Value.GetProperty("image_base64").GetString()
            .Should().Be("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVQIHWP4z8DwHwAFgAI/ScL1ZQAAAABJRU5ErkJggg==");
    }
}
