using McpCad.Core.Models;
using McpCad.Tests.Mocks;
using McpCad.Tools;
using McpCad.Tools.Onshape;
using ModelContextProtocol.Protocol;

namespace McpCad.Tests.Tools;

public sealed class OnshapeCaptureViewToolsTests
{
    private const string MinimalPngBase64 = "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVQIHWP4z8DwHwAFgAI/ScL1ZQAAAABJRU5ErkJggg==";
    private static string AddressJson() => new CadElementAddress("doc-1", "ws-1", null, "e-part").Serialize();

    [Fact]
    public void onshape_capture_view_UsesDefaultsAndReturnsNativeMcpImageContent()
    {
        var mock = new MockOnshapeProvider();
        var tools = new OnshapeAtomicTools(mock);

        var result = tools.onshape_capture_view(AddressJson());

        result.IsError.Should().NotBe(true);
        result.Content.Should().ContainSingle().Which.Should().BeOfType<ImageContentBlock>()
            .Which.MimeType.Should().Be("image/png");
        result.StructuredContent!.Value.GetProperty("image_base64").GetString().Should().Be(MinimalPngBase64);
        result.StructuredContent!.Value.GetProperty("mime_type").GetString().Should().Be("image/png");
        var call = mock.CallLog.Should().ContainSingle(entry => entry.Method == "CaptureView").Subject;
        call.Args["view"].Should().Be("iso");
        call.Args["width"].Should().Be(800);
        call.Args["height"].Should().Be(600);
        call.Args["edges"].Should().Be("show");
    }

    [Fact]
    public void onshape_capture_view_IsRegisteredAsAnMcpTool()
    {
        ToolRegistrationValidator.GetToolMethodNames(typeof(OnshapeAtomicTools))
            .Should().Contain("onshape_capture_view");
    }

    [Theory]
    [InlineData("ISO", "HIDE", 1, 4096, "iso", "hide")]
    [InlineData("Top", "Show", 4096, 1, "top", "show")]
    public void onshape_capture_view_NormalizesCaseAndAcceptsBoundaryDimensions(
        string view, string edges, int width, int height, string expectedView, string expectedEdges)
    {
        var mock = new MockOnshapeProvider();
        var tools = new OnshapeAtomicTools(mock);

        var result = tools.onshape_capture_view(AddressJson(), view, width, height, edges);

        result.IsError.Should().NotBe(true);
        var call = mock.CallLog.Should().ContainSingle(entry => entry.Method == "CaptureView").Subject;
        call.Args["view"].Should().Be(expectedView);
        call.Args["width"].Should().Be(width);
        call.Args["height"].Should().Be(height);
        call.Args["edges"].Should().Be(expectedEdges);
    }

    [Theory]
    [InlineData("front", 800, 600, "show")]
    [InlineData("iso", 0, 600, "show")]
    [InlineData("iso", 4097, 600, "show")]
    [InlineData("iso", 800, 0, "show")]
    [InlineData("iso", 800, 4097, "show")]
    [InlineData("iso", 800, 600, "outline")]
    public void onshape_capture_view_RejectsUnsupportedOptionsWithoutCallingProvider(string view, int width, int height, string edges)
    {
        var mock = new MockOnshapeProvider();
        var tools = new OnshapeAtomicTools(mock);

        var result = tools.onshape_capture_view(AddressJson(), view, width, height, edges);

        result.IsError.Should().BeTrue();
        mock.CallLog.Should().BeEmpty();
    }
}
