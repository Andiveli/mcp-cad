using McpCad.Core.Models;
using McpCad.Tests.Mocks;
using McpCad.Tools.Onshape;

namespace McpCad.Tests.Tools;

public class OnshapeAtomicToolsModelingTests
{
    private static string SampleAddress() =>
        new CadElementAddress("doc-1", "ws-1", null, "e-part").Serialize();

    [Fact]
    public void onshape_sketch_rectangle_Calls_SketchReplace_WithEntities()
    {
        var mock = new MockOnshapeProvider();
        var tools = new OnshapeAtomicTools(mock);

        var result = tools.onshape_sketch_rectangle(SampleAddress(), 0, 0, 5, 5);

        result["success"].Should().Be(true);
        mock.CallLog.Should().Contain(c => c.Method == "SketchReplace");
        var sketchCall = mock.CallLog.First(c => c.Method == "SketchReplace");
        sketchCall.Args["hasSketchRequest"].Should().Be(true);
    }

    [Fact]
    public void onshape_extrude_Calls_FeatureAdd_WithEnvelope()
    {
        var mock = new MockOnshapeProvider();
        var tools = new OnshapeAtomicTools(mock);

        var result = tools.onshape_extrude(SampleAddress(), "sketch-1", 2.5, "positive", "new");

        result["success"].Should().Be(true);
        mock.CallLog.Should().Contain(c => c.Method == "FeatureAdd");
        mock.CallLog.First(c => c.Method == "FeatureAdd").Args["hasEnvelope"].Should().Be(true);
    }

    [Fact]
    public void onshape_feature_list_Calls_FeatureList()
    {
        var mock = new MockOnshapeProvider();
        var tools = new OnshapeAtomicTools(mock);

        var result = tools.onshape_feature_list(SampleAddress());

        result["success"].Should().Be(true);
        mock.CallLog.Should().ContainSingle(c => c.Method == "FeatureList");
    }

    [Fact]
    public void onshape_sketch_rectangle_InvalidAddress_ReturnsError()
    {
        var mock = new MockOnshapeProvider();
        var tools = new OnshapeAtomicTools(mock);

        var result = tools.onshape_sketch_rectangle("not-json", 0, 0, 1, 1);

        result["success"].Should().Be(false);
        result.Should().ContainKey("error");
    }

    [Fact]
    public void P0EndToEnd_MockProvider_SketchListExtrude()
    {
        var mock = new MockOnshapeProvider();
        var tools = new OnshapeAtomicTools(mock);
        var addr = SampleAddress();

        tools.onshape_sketch_rectangle(addr, 0, 0, 10, 10).Should().ContainKey("success");
        tools.onshape_feature_list(addr).Should().ContainKey("success");
        tools.onshape_extrude(addr, "sketch-1", 1.0).Should().ContainKey("success");

        mock.CallLog.Select(c => c.Method).Should().ContainInOrder("SketchReplace", "FeatureList", "FeatureAdd");
    }

    [Theory]
    [InlineData("revolve")]
    [InlineData("fillet")]
    [InlineData("chamfer")]
    [InlineData("hole")]
    public void P1Tool_CallsFeatureAdd_WithExpectedEnvelope(string featureType)
    {
        var mock = new MockOnshapeProvider();
        var tools = new OnshapeAtomicTools(mock);
        var address = SampleAddress();

        var result = featureType switch
        {
            "revolve" => tools.onshape_revolve(address, "sketch-1", 180),
            "fillet" => tools.onshape_fillet(address, ["edge-1"], 0.5),
            "chamfer" => tools.onshape_chamfer(address, ["edge-1"], 0.3),
            "hole" => tools.onshape_hole(address, "vertex-1", 0.6, 2),
            _ => throw new ArgumentOutOfRangeException(nameof(featureType)),
        };

        result["success"].Should().Be(true);
        var call = mock.CallLog.Should().ContainSingle(c => c.Method == "FeatureAdd").Subject;
        call.Args["featureType"].Should().Be(featureType);
        call.Args["hasEnvelope"].Should().Be(true);
    }

    [Fact]
    public void onshape_asm_list_instances_Calls_AssemblyListInstances_AndReturnsEmptyInstances()
    {
        var mock = new MockOnshapeProvider();
        var tools = new OnshapeAtomicTools(mock);

        var result = tools.onshape_asm_list_instances(SampleAddress());

        result["success"].Should().Be(true);
        result["instances"].Should().BeOfType<object[]>().Which.Should().BeEmpty();
        mock.CallLog.Should().ContainSingle(c => c.Method == "AssemblyListInstances");
    }

    [Fact]
    public void onshape_asm_create_instance_Calls_AssemblyCreateInstance_WithPartReferences()
    {
        var mock = new MockOnshapeProvider();
        var tools = new OnshapeAtomicTools(mock);

        var result = tools.onshape_asm_create_instance(SampleAddress(), "part-doc-1", "part-element-1");

        result["success"].Should().Be(true);
        var call = mock.CallLog.Should().ContainSingle(c => c.Method == "AssemblyCreateInstance").Subject;
        call.Args["partDocumentId"].Should().Be("part-doc-1");
        call.Args["partElementId"].Should().Be("part-element-1");
    }

    [Fact]
    public void onshape_asm_delete_instance_Calls_AssemblyDeleteInstance()
    {
        var mock = new MockOnshapeProvider();
        var tools = new OnshapeAtomicTools(mock);

        var result = tools.onshape_asm_delete_instance(SampleAddress(), "instance-1");

        result["success"].Should().Be(true);
        var call = mock.CallLog.Should().ContainSingle(c => c.Method == "AssemblyDeleteInstance").Subject;
        call.Args["instanceId"].Should().Be("instance-1");
    }

    [Fact]
    public void onshape_asm_delete_instance_WhenProviderRejectsUnknownInstance_ReturnsError()
    {
        var tools = new OnshapeAtomicTools(MockOnshapeProvider.WithError("Instance does not exist."));

        var result = tools.onshape_asm_delete_instance(SampleAddress(), "missing-instance");

        result["success"].Should().Be(false);
        result["error"].Should().Be("Instance does not exist.");
    }
}
