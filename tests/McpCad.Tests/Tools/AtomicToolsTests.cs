using McpCad.Tests.Mocks;
using McpCad.Tools;

namespace McpCad.Tests.Tools;

public class AtomicToolsTests
{
    private readonly MockInventorProvider _mock = new();
    private readonly AtomicTools _tools;

    public AtomicToolsTests()
    {
        _tools = new AtomicTools(_mock);
    }

    // Helper to check if a provider method was called
    private bool WasCalled(string methodName)
        => _mock.CallLog.Any(c => c.Method == methodName);

    // ── Connection tools ─────────────────────────────────────────────

    [Fact]
    public void InventorConnect_DelegatesToProvider()
    {
        var result = _tools.inventor_connect();

        Assert.True((bool)result["success"]!);
        Assert.True(WasCalled("Connect"));
    }

    [Fact]
    public void InventorDisconnect_DelegatesToProvider()
    {
        var result = _tools.inventor_disconnect();

        Assert.True((bool)result["success"]!);
        Assert.True(WasCalled("Disconnect"));
    }

    [Fact]
    public void InventorHealth_DelegatesToProvider()
    {
        var result = _tools.inventor_health();

        Assert.True((bool)result["success"]!);
        Assert.True(WasCalled("Health"));
    }

    // ── Document tools ────────────────────────────────────────────────

    [Fact]
    public void DocOpen_DelegatesToProvider()
    {
        var result = _tools.doc_open("test.ipt");

        Assert.True((bool)result["success"]!);
        Assert.Equal("test.ipt", _mock.CallLog[0].Args["path"]);
    }

    [Fact]
    public void DocNewPart_DelegatesToProvider()
    {
        var result = _tools.doc_new_part();
        Assert.True((bool)result["success"]!);
    }

    [Fact]
    public void DocNewAssembly_DelegatesToProvider()
    {
        var result = _tools.doc_new_assembly();
        Assert.True((bool)result["success"]!);
    }

    [Fact]
    public void DocSave_DelegatesToProvider()
    {
        var result = _tools.doc_save();
        Assert.True((bool)result["success"]!);
    }

    [Fact]
    public void DocSaveAs_DelegatesToProvider()
    {
        var result = _tools.doc_save_as("output.ipt");

        Assert.True((bool)result["success"]!);
        Assert.Equal("output.ipt", _mock.CallLog[0].Args["path"]);
    }

    [Fact]
    public void DocClose_DelegatesToProvider()
    {
        var result = _tools.doc_close();
        Assert.True((bool)result["success"]!);
    }

    // ── Sketch tools ──────────────────────────────────────────────────

    [Fact]
    public void SketchCreate_DelegatesToProvider()
    {
        var result = _tools.sketch_create("XZ");

        Assert.True((bool)result["success"]!);
        Assert.Equal("XZ", _mock.CallLog[0].Args["plane"]);
    }

    [Fact]
    public void SketchLine_DelegatesToProvider()
    {
        var result = _tools.sketch_line(0, 0, 10, 5);

        Assert.True((bool)result["success"]!);
        Assert.Equal(0.0, _mock.CallLog[0].Args["x1"]);
        Assert.Equal(10.0, _mock.CallLog[0].Args["x2"]);
    }

    [Fact]
    public void SketchLine_WithTag_DelegatesCorrectly()
    {
        var result = _tools.sketch_line(0, 0, 10, 5, tag: "eje");

        Assert.True((bool)result["success"]!);
        Assert.Equal("eje", _mock.CallLog[0].Args["tag"]);
    }

    [Fact]
    public void SketchCircle_DelegatesToProvider()
    {
        var result = _tools.sketch_circle(5, 5, 3);

        Assert.True((bool)result["success"]!);
        Assert.Equal(5.0, _mock.CallLog[0].Args["cx"]);
        Assert.Equal(3.0, _mock.CallLog[0].Args["radius"]);
    }

    [Fact]
    public void SketchArc_DelegatesToProvider()
    {
        var result = _tools.sketch_arc(0, 0, 5, 0, 90);
        Assert.True((bool)result["success"]!);
    }

    [Fact]
    public void SketchRectangle_DelegatesToProvider()
    {
        var result = _tools.sketch_rectangle(0, 0, 10, 10);
        Assert.True((bool)result["success"]!);
    }

    [Fact]
    public void SketchDimension_DelegatesToProvider()
    {
        var result = _tools.sketch_dimension("linear", "1", "2", value: 25);

        Assert.True((bool)result["success"]!);
        Assert.Equal("linear", _mock.CallLog[0].Args["mode"]);
    }

    [Fact]
    public void SketchPoint_DelegatesToProvider()
    {
        var result = _tools.sketch_point(5, 10);
        Assert.True((bool)result["success"]!);
    }

    [Fact]
    public void SketchSpline_DelegatesToProvider()
    {
        var result = _tools.sketch_spline("0,0,5,5,10,0");
        Assert.True((bool)result["success"]!);
    }

    [Fact]
    public void SketchEllipse_DelegatesToProvider()
    {
        var result = _tools.sketch_ellipse(0, 0, 5, 3);
        Assert.True((bool)result["success"]!);
    }

    [Fact]
    public void SketchCircularPattern_DelegatesToProvider()
    {
        var result = _tools.sketch_circular_pattern("1", "2", 6);
        Assert.True((bool)result["success"]!);
    }

    [Fact]
    public void SketchRectangularPattern_DelegatesToProvider()
    {
        var result = _tools.sketch_rectangular_pattern("1", "2", 3, 5.0);
        Assert.True((bool)result["success"]!);
    }

    [Fact]
    public void SketchOffset_DelegatesToProvider()
    {
        var result = _tools.sketch_offset("1", 0, 5);
        Assert.True((bool)result["success"]!);
    }

    [Fact]
    public void SketchMove_DelegatesToProvider()
    {
        var result = _tools.sketch_move("1", 10, 0);
        Assert.True((bool)result["success"]!);
    }

    [Fact]
    public void SketchRotate_DelegatesToProvider()
    {
        var result = _tools.sketch_rotate("1", 0, 0, 90);
        Assert.True((bool)result["success"]!);
    }

    [Fact]
    public void SketchDelete_DelegatesToProvider()
    {
        var result = _tools.sketch_delete();
        Assert.True((bool)result["success"]!);
    }

    [Fact]
    public void SketchConstraint_DelegatesToProvider()
    {
        var result = _tools.sketch_constraint("parallel", "1", "2");
        Assert.True((bool)result["success"]!);
    }

    [Fact]
    public void SketchTrim_DelegatesToProvider()
    {
        var result = _tools.sketch_trim("1", "2");
        Assert.True((bool)result["success"]!);
    }

    [Fact]
    public void SketchScale_DelegatesToProvider()
    {
        var result = _tools.sketch_scale("1", 0, 0, 2.0);
        Assert.True((bool)result["success"]!);
    }

    [Fact]
    public void SketchMirror_DelegatesToProvider()
    {
        var result = _tools.sketch_mirror("1", "2");
        Assert.True((bool)result["success"]!);
    }

    // ── Feature tools ────────────────────────────────────────────────

    [Fact]
    public void Extrude_DelegatesToProvider()
    {
        var result = _tools.extrude("1", 5.0);

        Assert.True((bool)result["success"]!);
        Assert.Equal("1", _mock.CallLog[0].Args["profile"]);
        Assert.Equal(5.0, _mock.CallLog[0].Args["distance"]);
    }

    [Fact]
    public void Revolve_DelegatesToProvider()
    {
        var result = _tools.revolve("1", "@eje");

        Assert.True((bool)result["success"]!);
        Assert.Equal("1", _mock.CallLog[0].Args["profile"]);
        Assert.Equal("@eje", _mock.CallLog[0].Args["axis"]);
    }

    [Fact]
    public void Fillet_DelegatesToProvider()
    {
        var result = _tools.fillet("1,3", 2.0);
        Assert.True((bool)result["success"]!);
    }

    [Fact]
    public void Chamfer_DelegatesToProvider()
    {
        var result = _tools.chamfer("1", 1.5);
        Assert.True((bool)result["success"]!);
    }

    [Fact]
    public void CircularPattern_DelegatesToProvider()
    {
        var result = _tools.circular_pattern("1", "2", 6);
        Assert.True((bool)result["success"]!);
    }

    [Fact]
    public void Hole_DelegatesToProvider()
    {
        var result = _tools.hole(5, 5, 1.0, 2.0);
        Assert.True((bool)result["success"]!);
    }

    [Fact]
    public void Thread_DelegatesToProvider()
    {
        var result = _tools.thread("1", "ANSI Unified Screw Threads");
        Assert.True((bool)result["success"]!);
    }

    [Fact]
    public void InspectEdges_DelegatesToProvider()
    {
        var result = _tools.inspect_edges();
        Assert.True((bool)result["success"]!);
    }

    // ── Parameter tools ───────────────────────────────────────────────

    [Fact]
    public void ParamList_DelegatesToProvider()
    {
        var result = _tools.param_list();
        Assert.True((bool)result["success"]!);
    }

    [Fact]
    public void ParamGet_DelegatesToProvider()
    {
        var result = _tools.param_get("d0");
        Assert.True((bool)result["success"]!);
    }

    [Fact]
    public void ParamSet_DelegatesToProvider()
    {
        var result = _tools.param_set("d0", 25.0);
        Assert.True((bool)result["success"]!);
    }

    [Fact]
    public void ParamSetExpression_DelegatesToProvider()
    {
        var result = _tools.param_set_expression("d0", "d1 * 2");
        Assert.True((bool)result["success"]!);
    }

    // ── Property tools ────────────────────────────────────────────────

    [Fact]
    public void IPropertyGet_DelegatesToProvider()
    {
        var result = _tools.iproperty_get("Title");
        Assert.True((bool)result["success"]!);
    }

    [Fact]
    public void IPropertySet_DelegatesToProvider()
    {
        var result = _tools.iproperty_set("Title", "Test Part");
        Assert.True((bool)result["success"]!);
    }

    [Fact]
    public void IPropertySummary_DelegatesToProvider()
    {
        var result = _tools.iproperty_summary();
        Assert.True((bool)result["success"]!);
    }

    [Fact]
    public void IPropertyCustomGet_DelegatesToProvider()
    {
        var result = _tools.iproperty_custom_get("MyProp");
        Assert.True((bool)result["success"]!);
    }

    [Fact]
    public void IPropertyCustomSet_DelegatesToProvider()
    {
        var result = _tools.iproperty_custom_set("MyProp", "value");
        Assert.True((bool)result["success"]!);
    }

    // ── Export tools ───────────────────────────────────────────────────

    [Fact]
    public void ExportStep_DelegatesToProvider()
    {
        var result = _tools.export_step("output.stp");
        Assert.True((bool)result["success"]!);
    }

    [Fact]
    public void ExportStl_DelegatesToProvider()
    {
        var result = _tools.export_stl("output.stl");
        Assert.True((bool)result["success"]!);
    }

    [Fact]
    public void ExportPdf_DelegatesToProvider()
    {
        var result = _tools.export_pdf("output.pdf");
        Assert.True((bool)result["success"]!);
    }

    [Fact]
    public void ExportDxf_DelegatesToProvider()
    {
        var result = _tools.export_dxf("output.dxf");
        Assert.True((bool)result["success"]!);
    }
}