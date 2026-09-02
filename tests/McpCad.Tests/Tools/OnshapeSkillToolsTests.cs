using System.Reflection;
using System.Text.Json;
using McpCad.Core;
using McpCad.Core.Models;
using McpCad.Tools.Onshape;

namespace McpCad.Tests.Tools;

public class OnshapeSkillToolsTests
{
    [Fact]
    public void skill_sketch_and_extrude_ComposesRectangleSketchAndLinkedExtrude()
    {
        var provider = RecordingProvider.Create(
            sketchResult: new Dictionary<string, object?> { ["success"] = true, ["featureId"] = "sketch-42" },
            extrudeResult: new Dictionary<string, object?> { ["success"] = true, ["featureId"] = "extrude-99" });
        var tools = new OnshapeSkillTools(provider.Provider);

        var result = tools.skill_sketch_and_extrude(
            AddressJson(), 10, 20, 30, 40, 12.5, "Front", "negative", "add");

        Assert.True((bool)result["success"]!);
        var sketch = Assert.IsType<Dictionary<string, object?>>(result["sketch"]);
        Assert.True((bool)sketch["success"]!);
        Assert.Equal("sketch-42", sketch["featureId"]);
        var extrude = Assert.IsType<Dictionary<string, object?>>(result["extrude"]);
        Assert.True((bool)extrude["success"]!);
        Assert.Equal("extrude-99", extrude["featureId"]);

        var sketchCall = Assert.Single(provider.Calls.Where(call => call.Method == nameof(ICadWebProvider.SketchReplace)));
        var sketchRequest = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(sketchCall.Arguments[1]);
        var sketchJson = JsonSerializer.SerializeToDocument(sketchRequest);
        var feature = sketchJson.RootElement.GetProperty("feature");
        feature.GetProperty("parameters")[0].GetProperty("queries")[0].GetProperty("deterministicIds")[0].GetString().Should().Be("JCC");
        feature.GetProperty("entities").GetArrayLength().Should().Be(4);
        feature.GetProperty("entities")[0].GetProperty("geometry").GetProperty("pntX").GetDouble().Should().BeApproximately(0.2, 1e-9);

        var extrudeCall = Assert.Single(provider.Calls.Where(call => call.Method == nameof(ICadWebProvider.FeatureAdd)));
        Assert.Equal("extrude", extrudeCall.Arguments[1]);
        using var extrudeJson = JsonDocument.Parse(Assert.IsType<string>(extrudeCall.Arguments[3]));
        var parameters = extrudeJson.RootElement.GetProperty("feature").GetProperty("parameters");
        parameters.EnumerateArray().Single(parameter => parameter.GetProperty("parameterId").GetString() == "depth")
            .GetProperty("expression").GetString().Should().Be("0.125 m");
        parameters.EnumerateArray().Single(parameter => parameter.GetProperty("parameterId").GetString() == "direction")
            .GetProperty("value").GetString().Should().Be("negative");
        parameters.EnumerateArray().Single(parameter => parameter.GetProperty("parameterId").GetString() == "operationType")
            .GetProperty("value").GetString().Should().Be("ADD");
        var entities = parameters.EnumerateArray().Single(parameter =>
            parameter.GetProperty("parameterId").GetString() == "entities");
        var sketchRegionQuery = entities.GetProperty("queries")[0];
        sketchRegionQuery.GetProperty("btType").GetString().Should().Be("BTMIndividualSketchRegionQuery-140");
        sketchRegionQuery.GetProperty("featureId").GetString().Should().Be("sketch-42");
    }

    [Fact]
    public void skill_sketch_and_extrude_AcceptsNestedFeatureIdsAsProviderDefenseInDepth()
    {
        var provider = RecordingProvider.Create(
            sketchResult: new Dictionary<string, object?>
            {
                ["success"] = true,
                ["feature"] = new Dictionary<string, object?> { ["featureId"] = "live-sketch-42" },
            },
            extrudeResult: new Dictionary<string, object?>
            {
                ["success"] = true,
                ["feature"] = new Dictionary<string, object?> { ["featureId"] = "live-extrude-99" },
            });

        var result = new OnshapeSkillTools(provider.Provider).skill_sketch_and_extrude(AddressJson(), 0, 0, 10, 10, 5);

        Assert.True((bool)result["success"]!);
        Assert.Equal("live-extrude-99", Assert.IsType<Dictionary<string, object?>>(result["extrude"])["featureId"]);
        var extrudeCall = Assert.Single(provider.Calls.Where(call => call.Method == nameof(ICadWebProvider.FeatureAdd)));
        using var payload = JsonDocument.Parse(Assert.IsType<string>(extrudeCall.Arguments[3]));
        payload.RootElement.GetProperty("feature").GetProperty("parameters").EnumerateArray()
            .Single(parameter => parameter.GetProperty("parameterId").GetString() == "entities")
            .GetProperty("queries")[0].GetProperty("featureId").GetString().Should().Be("live-sketch-42");
    }

    [Fact]
    public void skill_sketch_and_extrude_WhenExtrudeFails_ReportsPhaseFailure()
    {
        var provider = RecordingProvider.Create(
            sketchResult: new Dictionary<string, object?> { ["success"] = true, ["featureId"] = "sketch-42" },
            extrudeResult: new Dictionary<string, object?> { ["success"] = false, ["error"] = "extrude rejected" });
        var tools = new OnshapeSkillTools(provider.Provider);

        var result = tools.skill_sketch_and_extrude(AddressJson(), 0, 0, 10, 10, 5);

        Assert.False((bool)result["success"]!);
        Assert.True((bool)Assert.IsType<Dictionary<string, object?>>(result["sketch"])["success"]!);
        var extrude = Assert.IsType<Dictionary<string, object?>>(result["extrude"]);
        Assert.False((bool)extrude["success"]!);
        var extrudeResult = Assert.IsType<Dictionary<string, object?>>(extrude["result"]);
        Assert.Equal("extrude rejected", extrudeResult["error"]);
        Assert.Single(provider.Calls.Where(call => call.Method == nameof(ICadWebProvider.FeatureAdd)));
    }

    [Fact]
    public void skill_sketch_and_extrude_WhenSketchFails_SkipsExtrude()
    {
        var provider = RecordingProvider.Create(
            sketchResult: new Dictionary<string, object?> { ["success"] = false, ["error"] = "sketch rejected" },
            extrudeResult: new Dictionary<string, object?> { ["success"] = true });
        var tools = new OnshapeSkillTools(provider.Provider);

        var result = tools.skill_sketch_and_extrude(AddressJson(), 0, 0, 10, 10, 5);

        Assert.False((bool)result["success"]!);
        Assert.False((bool)Assert.IsType<Dictionary<string, object?>>(result["sketch"])["success"]!);
        var extrude = Assert.IsType<Dictionary<string, object?>>(result["extrude"]);
        Assert.False((bool)extrude["success"]!);
        Assert.True((bool)extrude["skipped"]!);
        Assert.DoesNotContain(provider.Calls, call => call.Method == nameof(ICadWebProvider.FeatureAdd));
    }

    [Fact]
    public void skill_sketch_and_extrude_WhenSketchHasNoFeatureId_SkipsExtrude()
    {
        var provider = RecordingProvider.Create(
            sketchResult: new Dictionary<string, object?> { ["success"] = true },
            extrudeResult: new Dictionary<string, object?> { ["success"] = true });
        var tools = new OnshapeSkillTools(provider.Provider);

        var result = tools.skill_sketch_and_extrude(AddressJson(), 0, 0, 10, 10, 5);

        Assert.False((bool)result["success"]!);
        Assert.Contains("feature ID", result["error"]!.ToString());
        Assert.True((bool)Assert.IsType<Dictionary<string, object?>>(result["extrude"])["skipped"]!);
        Assert.DoesNotContain(provider.Calls, call => call.Method == nameof(ICadWebProvider.FeatureAdd));
    }

    private static string AddressJson() => new CadElementAddress("doc", "workspace", null, "element").Serialize();

    private class RecordingProvider : DispatchProxy
    {
        public List<(string Method, object?[] Arguments)> Calls { get; } = [];
        public ICadWebProvider Provider { get; private set; } = null!;
        private Dictionary<string, object?> _sketchResult = null!;
        private Dictionary<string, object?> _extrudeResult = null!;

        public static RecordingProvider Create(
            Dictionary<string, object?> sketchResult,
            Dictionary<string, object?> extrudeResult)
        {
            var provider = DispatchProxy.Create<ICadWebProvider, RecordingProvider>();
            var recordingProvider = (RecordingProvider)(object)provider;
            recordingProvider.Provider = provider;
            recordingProvider._sketchResult = sketchResult;
            recordingProvider._extrudeResult = extrudeResult;
            return recordingProvider;
        }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            var arguments = args ?? [];
            Calls.Add((targetMethod!.Name, arguments));
            return targetMethod.Name switch
            {
                nameof(ICadWebProvider.SketchReplace) => _sketchResult,
                nameof(ICadWebProvider.FeatureAdd) => _extrudeResult,
                _ => throw new NotSupportedException($"Unexpected provider call: {targetMethod.Name}"),
            };
        }
    }
}
