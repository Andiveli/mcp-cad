namespace McpCad.Tests.Integration;

/// <summary>
/// Integration tests for sketch operations against a real Inventor instance.
/// These tests require Inventor and are skipped in CI.
/// </summary>
public class SketchIntegrationTests
{
    [Fact(Skip = "Requires Inventor")]
    public void SketchCreate_OnXYPlane_ReturnsSketchName()
    {
        // TODO: Create sketch on XY plane, verify name
    }

    [Fact(Skip = "Requires Inventor")]
    public void SketchLine_DrawsLine_InActiveSketch()
    {
        // TODO: Draw line, verify entity index
    }

    [Fact(Skip = "Requires Inventor")]
    public void SketchCircle_WithTag_StoresTagInTagStore()
    {
        // TODO: Draw circle with tag, verify tag resolution
    }

    [Fact(Skip = "Requires Inventor")]
    public void SketchLine_ConnectMode_ChainsEndpoints()
    {
        // TODO: Verify connected lines share endpoints
    }
}