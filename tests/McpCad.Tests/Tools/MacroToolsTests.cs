using McpCad.Tests.Mocks;
using McpCad.Tools;

namespace McpCad.Tests.Tools;

public class MacroToolsTests
{
    private readonly MockInventorProvider _mock = new();
    private readonly MacroTools _tools;

    public MacroToolsTests()
    {
        _tools = new MacroTools(_mock);
    }

    private bool WasCalled(string methodName)
        => _mock.CallLog.Any(c => c.Method == methodName);

    // ── 4.1 JSON parsing ────────────────────────────────────────────────

    [Fact]
    public void ParseSketchJson_ValidEntities_ReturnsItems()
    {
        var json = @"[{""type"":""line"",""x1"":0,""y1"":0,""x2"":10,""y2"":0},{""type"":""circle"",""cx"":5,""cy"":5,""radius"":3}]";
        var (items, error) = MacroTools.ParseSketchJson<SketchEntity>(json, "sketch_entities");
        Assert.NotNull(items);
        Assert.Equal(2, items.Count);
        Assert.Null(error);
    }

    [Fact]
    public void ParseSketchJson_MalformedJson_ReturnsError()
    {
        var (items, error) = MacroTools.ParseSketchJson<SketchEntity>("not json", "sketch_entities");
        Assert.Null(items);
        Assert.NotNull(error);
        Assert.False((bool)error["success"]!);
        Assert.Contains("Invalid JSON", error["error"]!.ToString());
    }

    // ── 4.2 ask_before_modify guard ─────────────────────────────────────

    [Fact]
    public void AskBeforeModify_FeatureCount0_Proceeds()
    {
        _mock.SetGetFeatureTreeResult(new Dictionary<string, object?>
        {
            ["success"] = true,
            ["features"] = new List<Dictionary<string, object?>>(),
            ["feature_count"] = 0
        });

        var result = _tools.macro_god_part(ask_before_modify: true);

        // No early return confirmation — guard passed with 0 features
        Assert.False(result.ContainsKey("needs_confirmation"));
        // success=false because no work was done (no inputs provided)
        Assert.False((bool)result["success"]!);
        // Verify phase always runs (GetFeatureTree and Capture called at least once)
        Assert.True(WasCalled("GetFeatureTree"));
        Assert.True(WasCalled("CaptureViewportImage"));
    }

    [Fact]
    public void AskBeforeModify_FeatureCount1_ReturnsConfirmationEnvelope()
    {
        _mock.SetGetFeatureTreeResult(new Dictionary<string, object?>
        {
            ["success"] = true,
            ["features"] = new List<Dictionary<string, object?>>
            {
                new() { ["name"] = "Extrude1" }
            },
            ["feature_count"] = 1
        });

        var result = _tools.macro_god_part(ask_before_modify: true);

        Assert.True((bool)result["success"]!);
        Assert.True((bool)result["needs_confirmation"]!);
        Assert.Contains("1 existing features", result["message"]!.ToString());
        var current = (Dictionary<string, object?>)result["current_state"]!;
        Assert.Equal(1, current["feature_count"]);
        // Guard should have called Health + GetFeatureTree; no mutation should occur
        Assert.True(WasCalled("Health"));
        Assert.True(WasCalled("GetFeatureTree"));
        Assert.False(WasCalled("DocNewPart"));
        Assert.False(WasCalled("SketchCreate"));
    }

    // ── 4.3 per-phase partial failure ───────────────────────────────────

    [Fact]
    public void PartialFailure_SketchOk_FeatureFails_ReportsInPhaseStatus()
    {
        // Valid rect for sketch
        var sketchJson = @"[{""type"":""rect"",""x1"":0,""y1"":0,""x2"":10,""y2"":10}]";

        // Make feature fail
        _mock.SetExtrudeResult(new Dictionary<string, object?>
        {
            ["success"] = false,
            ["error"] = "invalid extrude params"
        });

        var result = _tools.macro_god_part(
            ask_before_modify: false,
            sketch_entities: sketchJson,
            feature_type: "extrude",
            feature_distance: 5.0
        );

        Assert.True((bool)result["success"]!);
        Assert.Contains("Feature 'extrude' failed", string.Join(";", (List<string>)result["warnings"]!));

        var phaseStatus = (Dictionary<string, object?>)result["phase_status"]!;
        var sketchStatus = phaseStatus["sketch"] as MacroPhaseStatus;
        Assert.NotNull(sketchStatus);
        Assert.True(sketchStatus.Success);
        Assert.Equal("sketch", sketchStatus.Phase);
        Assert.Equal(1, sketchStatus.EntityCount);

        var featureStatus = phaseStatus["feature"] as MacroPhaseStatus;
        Assert.NotNull(featureStatus);
        Assert.False(featureStatus.Success);
        Assert.Equal("feature", featureStatus.Phase);
        Assert.Equal("extrude", featureStatus.FeatureType);
        Assert.NotNull(featureStatus.Error);
    }

    // ── 4.4 full pipeline ───────────────────────────────────────────────

    [Fact]
    public void FullPipeline_RectCircle_Extrude_Fillet_ProducesCompleteEnvelope()
    {
        var sketchJson = @"[{""type"":""rect"",""x1"":0,""y1"":0,""x2"":10,""y2"":10},{""type"":""circle"",""cx"":5,""cy"":5,""radius"":3}]";
        var modifyJson = @"[{""op"":""fillet"",""edges"":""1"",""radius"":0.5}]";

        var result = _tools.macro_god_part(
            ask_before_modify: false,
            sketch_entities: sketchJson,
            feature_type: "extrude",
            feature_distance: 5.0,
            modify_3d: modifyJson
        );

        Assert.True((bool)result["success"]!);
        Assert.True((bool)result["geometry_created"]!);

        var phaseStatus = (Dictionary<string, object?>)result["phase_status"]!;
        Assert.True(((MacroPhaseStatus)phaseStatus["sketch"]!).Success);
        Assert.True(((MacroPhaseStatus)phaseStatus["feature"]!).Success);
        Assert.True(((MacroPhaseStatus)phaseStatus["modify_3d"]!).Success);

        // Verify data always present (best-effort)
        Assert.NotNull(result["tree"]);
        Assert.NotNull(result["bounding_box"]);
        var viewport = (List<Dictionary<string, object?>?>)result["viewport_images"]!;
        Assert.True(viewport.Count >= 1); // at least Iso or Top succeeded
        var warnings = (List<string>)result["warnings"]!;
        Assert.NotNull(warnings); // clean run may have zero warnings
    }

    // ── 4.5 polygon helper ──────────────────────────────────────────────

    [Fact]
    public void GeneratePolygonLines_6Sides_Returns6Segments()
    {
        var lines = MacroTools.GeneratePolygonLines(0, 0, 5, 6);
        Assert.Equal(6, lines.Count);
        // Each segment should connect to the next (closed)
        for (int i = 0; i < lines.Count; i++)
        {
            int next = (i + 1) % lines.Count;
            Assert.Equal(lines[i].X2, lines[next].X1, 6);
            Assert.Equal(lines[i].Y2, lines[next].Y1, 6);
        }
    }
}
