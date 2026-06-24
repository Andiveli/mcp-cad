namespace McpCad.Tests.Integration;

using McpCad.Inventor;
using McpCad.Inventor.Managers;

/// <summary>
/// Integration tests for sketch operations against a real Inventor instance.
/// These tests require Inventor and are skipped in CI.
/// </summary>
public class SketchIntegrationTests
{
    [Fact(Skip = "Requires Inventor — run manually")]
    public void SketchCreate_OnXYPlane_ReturnsSketchName() { }

    [Fact(Skip = "Requires Inventor — run manually")]
    public void SketchLine_DrawsLine_InActiveSketch() { }

    [Fact(Skip = "Requires Inventor — run manually")]
    public void SketchCircle_WithTag_StoresTagInTagStore() { }

    [Fact(Skip = "Requires Inventor — run manually")]
    public void SketchLine_ConnectMode_ChainsEndpoints() { }

    /// <summary>
    /// Integration test for the offset fix: verifies that SketchOffset
    /// no longer throws E_FAIL when entities are wrapped with Dispatch.
    /// 
    /// Prerequisite: Inventor must be running.
    /// </summary>
    [Fact]
    public void SketchOffset_WithWrappedEntities_Succeeds()
    {
        // Arrange: connect to running Inventor
        var driver = new InventorDriver();
        driver.Connect();

        // Create a new part document (using DocumentManager to avoid interop refs)
        var docMgr = new DocumentManager(driver);
        var docResult = docMgr.DocNewPart();
        Assert.True((bool)docResult["success"]!, "DocNewPart should succeed");

        var sketchMgr = new SketchManager(driver);

        // Create sketch on XY plane
        var sketchResult = sketchMgr.SketchCreate("XY");
        Assert.True((bool)sketchResult["success"]!, "SketchCreate should succeed");

        // Draw a circle at origin, radius 3
        var circleResult = sketchMgr.SketchCircle(0, 0, 3);
        Assert.True((bool)circleResult["success"]!, "SketchCircle should succeed");

        // Act: offset the circle through point (5, 0)
        // This should NOT throw E_FAIL after the Dispatch-wrapper fix
        var offsetResult = sketchMgr.SketchOffset("1", 5, 0);

        // Assert
        Assert.True((bool)offsetResult["success"]!, 
            $"Offset should succeed with Dispatch-wrapped entities. Error: {offsetResult.GetValueOrDefault("error")}");
        Assert.Equal("offset", offsetResult["operation"]);
    }

    /// <summary>
    /// TDD placeholder (strict TDD): documents + will exercise ReadSketchData + SketchReader.
    /// Run manually with Inventor open after a sketch with entities exists.
    /// Captured data must be consumable by macro_god_part sketch_entities.
    /// </summary>
    [Fact(Skip = "Requires Inventor — run manually")]
    public void ReadSketchData_ReturnsEntities_AndParameters()
    {
        var driver = new InventorDriver();
        driver.Connect();
        var provider = new InventorProvider(driver); // real impl under test
        // Assume a sketch exists (created by prior test or manual); index 1
        var read = provider.ReadSketchData(1);
        Assert.True((bool)read["success"]!, $"ReadSketchData should succeed. Error: {read.GetValueOrDefault("error")}");
        Assert.NotNull(read["entities"]);
    }
}
