#if MCP_CAD_WINDOWS
using McpCad.Core;
using McpCad.Core.Exceptions;
using McpCad.Tests.Mocks;
using McpCad.Tools;

namespace McpCad.Tests.Tools;

public class ErrorHandlingTests
{
    /// <summary>
    /// Verifies that InventorConnectionException is caught and translated to
    /// { "success": false, "error": "..." } by the tool layer (D7).
    /// </summary>
    [Fact]
    public void AtomicTools_Catches_InventorConnectionException()
    {
        var mock = new MockInventorProvider();
        mock.SetConnectResult(new Dictionary<string, object?>
        {
            ["success"] = false,
            ["error"] = "Inventor is not running",
        });
        var tools = new AtomicTools(new MechanicalRouterAdapter(mock));

        var result = tools.inventor_connect();

        Assert.False((bool)result["success"]!);
        Assert.Equal("Inventor is not running", result["error"]);
    }

    /// <summary>
    /// Verifies that InventorComException caught at the tool layer produces
    /// a standardized error result.
    /// </summary>
    [Fact]
    public void InventorComException_Produces_ErrorResult()
    {
        var mock = new ThrowingMockProvider(
            new InventorComException("Edge index 99 does not exist."));
        var tools = new AtomicTools(new MechanicalRouterAdapter(mock));

        var result = tools.fillet("99", 2.0);

        Assert.False((bool)result["success"]!);
        Assert.Contains("Edge index 99", result["error"]!.ToString());
    }

    /// <summary>
    /// Verifies that generic exceptions are caught and produce
    /// { "success": false, "error": "Unexpected error: ..." }.
    /// </summary>
    [Fact]
    public void GenericException_Produces_UnexpectedErrorResult()
    {
        var mock = new ThrowingMockProvider(new Exception("Something broke"));
        var tools = new AtomicTools(new MechanicalRouterAdapter(mock));

        var result = tools.extrude("1", 5.0);

        Assert.False((bool)result["success"]!);
        Assert.Contains("Unexpected error", result["error"]!.ToString());
        Assert.Contains("Something broke", result["error"]!.ToString());
    }

    [Fact]
    public void SketchCreate_Error_Returns_False_Success()
    {
        var mock = MockInventorProvider.WithError("No active document");
        var tools = new AtomicTools(new MechanicalRouterAdapter(mock));

        var result = tools.sketch_create("XY");

        Assert.False((bool)result["success"]!);
        Assert.Equal("No active document", result["error"]);
    }

    [Fact]
    public void ParamGet_Error_Returns_False_Success()
    {
        var mock = MockInventorProvider.WithError("Parameter not found");
        var tools = new AtomicTools(new MechanicalRouterAdapter(mock));

        var result = tools.param_get("nonexistent");

        Assert.False((bool)result["success"]!);
        Assert.Equal("Parameter not found", result["error"]);
    }

    [Theory]
    [InlineData(typeof(InventorConnectionException), false)]
    [InlineData(typeof(InventorComException), false)]
    [InlineData(typeof(Exception), false)]
    public void AllExceptionTypes_Produce_Error_Result(Type exceptionType, bool expectedSuccess)
    {
        var ex = (Exception)Activator.CreateInstance(exceptionType, "test error")!;
        var mock = new ThrowingMockProvider(ex);
        var tools = new AtomicTools(new MechanicalRouterAdapter(mock));

        var result = tools.inventor_connect();

        Assert.Equal(expectedSuccess, result["success"]);
        Assert.NotNull(result["error"]);
    }

    [Fact]
    public void SkillTools_Catches_InventorComException()
    {
        var mock = new ThrowingMockProvider(
            new InventorComException("Profile not found"));
        var tools = new SkillTools(new MechanicalRouterAdapter(mock));

        var result = tools.skill_sketch("XY");

        Assert.False((bool)result["success"]!);
        Assert.Contains("Profile not found", result["error"]!.ToString());
    }

    [Fact]
    public void WorkPlane_Error_Returns_False_Success()
    {
        var mock = MockInventorProvider.WithError("Invalid geometry reference");
        var tools = new AtomicTools(new MechanicalRouterAdapter(mock));

        var result = tools.work_plane("offset_from_plane", "bad_ref");

        Assert.False((bool)result["success"]!);
        Assert.Equal("Invalid geometry reference", result["error"]);
    }

    [Fact]
    public void WorkAxis_ComException_Produces_ErrorResult()
    {
        var mock = new ThrowingMockProvider(
            new InventorComException("Edge index 99 does not exist."));
        var tools = new AtomicTools(new MechanicalRouterAdapter(mock));

        var result = tools.work_axis("along_edge", "99");

        Assert.False((bool)result["success"]!);
        Assert.Contains("Edge index 99", result["error"]!.ToString());
    }

    [Fact]
    public void WorkPoint_ComException_Produces_ErrorResult()
    {
        var mock = new ThrowingMockProvider(
            new InventorComException("Plane reference not found"));
        var tools = new AtomicTools(new MechanicalRouterAdapter(mock));

        var result = tools.work_point("intersection", "bad_plane");

        Assert.False((bool)result["success"]!);
        Assert.Contains("Plane reference not found", result["error"]!.ToString());
    }

    [Fact]
    public void WorkPoint_Error_Returns_False_Success()
    {
        var mock = MockInventorProvider.WithError("Missing required parameter: z");
        var tools = new AtomicTools(new MechanicalRouterAdapter(mock));

        var result = tools.work_point("at_coordinates", "10", "5");

        Assert.False((bool)result["success"]!);
        Assert.Equal("Missing required parameter: z", result["error"]);
    }

    [Fact]
    public void Combine_ComException_Produces_ErrorResult()
    {
        var mock = new ThrowingMockProvider(
            new InventorComException("Tool body index 99 does not exist."));
        var tools = new AtomicTools(new MechanicalRouterAdapter(mock));

        var result = tools.combine("1", "99", "cut");

        Assert.False((bool)result["success"]!);
        Assert.Contains("Tool body index 99", result["error"]!.ToString());
    }

    [Fact]
    public void Combine_Error_Returns_False_Success()
    {
        var mock = MockInventorProvider.WithError("No active document");
        var tools = new AtomicTools(new MechanicalRouterAdapter(mock));

        var result = tools.combine("1", "2");

        Assert.False((bool)result["success"]!);
        Assert.Equal("No active document", result["error"]);
    }

    [Fact]
    public void Shell_ComException_Produces_ErrorResult()
    {
        var mock = new ThrowingMockProvider(
            new InventorComException("Face index 99 does not exist."));
        var tools = new AtomicTools(new MechanicalRouterAdapter(mock));

        var result = tools.shell("99", 0.2);

        Assert.False((bool)result["success"]!);
        Assert.Contains("Face index 99", result["error"]!.ToString());
    }

    [Fact]
    public void Shell_Error_Returns_False_Success()
    {
        var mock = MockInventorProvider.WithError("No active document");
        var tools = new AtomicTools(new MechanicalRouterAdapter(mock));

        var result = tools.shell("1,2", 0.3);

        Assert.False((bool)result["success"]!);
        Assert.Equal("No active document", result["error"]);
    }

    [Fact]
    public void Thicken_ComException_Produces_ErrorResult()
    {
        var mock = new ThrowingMockProvider(
            new InventorComException("Face index 99 does not exist."));
        var tools = new AtomicTools(new MechanicalRouterAdapter(mock));

        var result = tools.thicken("99", 0.2);

        Assert.False((bool)result["success"]!);
        Assert.Contains("Face index 99", result["error"]!.ToString());
    }

    [Fact]
    public void Thicken_Error_Returns_False_Success()
    {
        var mock = MockInventorProvider.WithError("No active document");
        var tools = new AtomicTools(new MechanicalRouterAdapter(mock));

        var result = tools.thicken("1", 0.2);

        Assert.False((bool)result["success"]!);
        Assert.Equal("No active document", result["error"]);
    }

    [Fact]
    public void Split_ComException_Produces_ErrorResult()
    {
        var mock = new ThrowingMockProvider(
            new InventorComException("Work plane index 99 does not exist."));
        var tools = new AtomicTools(new MechanicalRouterAdapter(mock));

        var result = tools.split("99", "both");

        Assert.False((bool)result["success"]!);
        Assert.Contains("Work plane index 99", result["error"]!.ToString());
    }

    [Fact]
    public void Split_Error_Returns_False_Success()
    {
        var mock = MockInventorProvider.WithError("No active document");
        var tools = new AtomicTools(new MechanicalRouterAdapter(mock));

        var result = tools.split("1", "both");

        Assert.False((bool)result["success"]!);
        Assert.Equal("No active document", result["error"]);
    }

    [Fact]
    public void Draft_ComException_Produces_ErrorResult()
    {
        var mock = new ThrowingMockProvider(
            new InventorComException("Face index 99 does not exist."));
        var tools = new AtomicTools(new MechanicalRouterAdapter(mock));

        var result = tools.draft("99", 5.0);

        Assert.False((bool)result["success"]!);
        Assert.Contains("Face index 99", result["error"]!.ToString());
    }

    [Fact]
    public void Draft_Error_Returns_False_Success()
    {
        var mock = MockInventorProvider.WithError("No active document");
        var tools = new AtomicTools(new MechanicalRouterAdapter(mock));

        var result = tools.draft("1,2", 5.0);

        Assert.False((bool)result["success"]!);
        Assert.Equal("No active document", result["error"]);
    }
}
#endif