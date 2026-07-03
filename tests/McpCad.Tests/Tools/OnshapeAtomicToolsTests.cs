using McpCad.Core.Exceptions;
using McpCad.Tests.Mocks;
using McpCad.Tools.Onshape;

namespace McpCad.Tests.Tools;

public class OnshapeAtomicToolsTests
{
    [Fact]
    public void create_sketch_Catches_CadProviderException()
    {
        var mock = MockOnshapeProvider.WithError("quota exceeded");
        var tools = new OnshapeAtomicTools(mock);
        var addr = new McpCad.Core.Models.CadElementAddress("d", "w", null, "e").Serialize();

        var result = tools.create_sketch(addr);

        Assert.False((bool)result["success"]!);
        Assert.Equal("quota exceeded", result["error"]);
    }

    [Fact]
    public void onshape_health_Returns_Success_From_Mock()
    {
        var mock = new MockOnshapeProvider();
        var tools = new OnshapeAtomicTools(mock);

        var result = tools.onshape_health();

        Assert.True((bool)result["success"]!);
    }
}