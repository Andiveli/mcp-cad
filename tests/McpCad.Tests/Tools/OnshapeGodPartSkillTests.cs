using System.Reflection;
using System.Text.Json;
using McpCad.Core;
using McpCad.Core.Models;
using McpCad.Tools.Onshape;
using ModelContextProtocol.Protocol;

namespace McpCad.Tests.Tools;

public class OnshapeGodPartModelingTests
{
    [Theory]
    [InlineData(0, 0, 0, 5)]
    [InlineData(0, 0, 10, 0)]
    public void skill_onshape_god_part_RejectsDegenerateRectanglesBeforeProviderMutation(double x1, double y1, double x2, double y2)
    {
        var provider = RecordingProvider.Create([]);
        var result = new OnshapeSkillTools(provider.Provider).skill_onshape_god_part(AddressJson(), x1, y1, x2, y2, 2);
        result.IsError.Should().BeTrue();
        Structured(result)["failedPhase"].Should().Be("validation");
        provider.Calls.Should().BeEmpty();
    }

    [Fact]
    public void skill_onshape_god_part_AcceptsNestedFeatureIdsAsProviderDefenseInDepth()
    {
        var provider = RecordingProvider.Create([NestedSuccess("live-sketch-1"), NestedSuccess("live-extrude-1")], [CaptureSuccess(), CaptureSuccess()]);
        var result = Structured(new OnshapeSkillTools(provider.Provider).skill_onshape_god_part(AddressJson(), 0, 0, 10, 5, 2));
        result["success"].Should().Be(true);
        result["finalFeatureId"].Should().Be("live-extrude-1");
        var extrudeCall = provider.Calls.Single(call => call.Method == nameof(ICadWebProvider.FeatureAdd));
        using var payload = JsonDocument.Parse(Assert.IsType<string>(extrudeCall.Arguments[3]));
        payload.RootElement.GetProperty("feature").GetProperty("parameters").EnumerateArray().Single(parameter => parameter.GetProperty("parameterId").GetString() == "entities").GetProperty("queries")[0].GetProperty("featureId").GetString().Should().Be("live-sketch-1");
    }

    [Fact]
    public void skill_onshape_god_part_ComposesFeaturesAndUsesLatestFeatureAsPatternAndMirrorSource()
    {
        var provider = RecordingProvider.Create([Success("sketch-1"), Success("extrude-1"), Success("fillet-1"), Success("pattern-1"), Success("mirror-1")], [CaptureSuccess(), CaptureSuccess()]);
        var result = Structured(new OnshapeSkillTools(provider.Provider).skill_onshape_god_part(AddressJson(), 0, 0, 10, 5, 2, """{"fillet":{"edgeIds":["edge-1"],"radiusCm":0.2},"circularPattern":{"axisFeatureId":"axis-1","instanceCount":4,"totalAngleDeg":360},"mirror":{"plane":"Top"}}"""));
        result["success"].Should().Be(true); result["finalFeatureId"].Should().Be("mirror-1");
        AssertFeatureListSource(provider.Calls.Where(call => call.Method == nameof(ICadWebProvider.FeatureAdd)).ElementAt(2), "fillet-1");
        AssertFeatureListSource(provider.Calls.Where(call => call.Method == nameof(ICadWebProvider.FeatureAdd)).ElementAt(3), "pattern-1");
    }

    [Fact]
    public void skill_onshape_god_part_ComposesChamferShellAndRectangularPattern()
    {
        var provider = RecordingProvider.Create([Success("sketch-1"), Success("extrude-1"), Success("chamfer-1"), Success("shell-1"), Success("linear-1")], [CaptureSuccess(), CaptureSuccess()]);
        var result = Structured(new OnshapeSkillTools(provider.Provider).skill_onshape_god_part(AddressJson(), 0, 0, 10, 5, 2, """{"chamfer":{"edgeIds":["edge-1"],"distanceCm":0.2},"shell":{"faceIds":["face-1"],"thicknessCm":0.1},"rectangularPattern":{"directionOneFeatureId":"direction-1","distanceCm":2,"instanceCount":3}}"""));
        result["success"].Should().Be(true); result["finalFeatureId"].Should().Be("linear-1");
        provider.Calls.Where(call => call.Method == nameof(ICadWebProvider.FeatureAdd)).Select(call => call.Arguments[1]).Should().Equal("extrude", "chamfer", "shell", "linearPattern");
        AssertFeatureListSource(provider.Calls.Where(call => call.Method == nameof(ICadWebProvider.FeatureAdd)).Last(), "shell-1");
    }

    [Theory]
    [InlineData("""{"fillet":{"radiusCm":0.2}}""")]
    [InlineData("""{"mirror":{"plane":"Top","plane":"Front"}}""")]
    public void skill_onshape_god_part_InvalidOptionsFailBeforeProviderMutation(string options) => AssertValidationNoCalls(options);

    [Theory]
    [InlineData("""{"profile":null}""")]
    [InlineData("""{"profile":{}}""")]
    [InlineData("""{"profile":[{"x_cm":0,"y_cm":0,"z_cm":0},{"x_cm":1,"y_cm":0},{"x_cm":0,"y_cm":1}]}""")]
    [InlineData("""{"profile":[{"x_cm":"0","y_cm":0},{"x_cm":1,"y_cm":0},{"x_cm":0,"y_cm":1}]}""")]
    [InlineData("""{"profile":[{"x_cm":0,"x_cm":1,"y_cm":0},{"x_cm":1,"y_cm":0},{"x_cm":0,"y_cm":1}]}""")]
    public void skill_onshape_god_part_RejectsInvalidProfileJsonBeforeProviderMutation(string options) => AssertValidationNoCalls(options);

    [Fact]
    public void skill_onshape_god_part_RejectsInvalidProfileGeometryBeforeProviderMutation() =>
        AssertValidationNoCalls("""{"profile":[{"x_cm":0,"y_cm":0},{"x_cm":1,"y_cm":0},{"x_cm":1,"y_cm":0}]}""");

    [Theory]
    [InlineData("""[{"x_cm":0,"y_cm":0},{"x_cm":4,"y_cm":0},{"x_cm":2,"y_cm":3}]""", 3)]
    [InlineData("""[{"x_cm":0,"y_cm":0},{"x_cm":4,"y_cm":0},{"x_cm":4,"y_cm":4},{"x_cm":2,"y_cm":2},{"x_cm":0,"y_cm":4}]""", 5)]
    public void skill_onshape_god_part_UsesValidStraightLineProfile(string profile, int lineCount)
    {
        var provider = RecordingProvider.Create([Success("sketch-1"), Success("extrude-1")], [CaptureSuccess(), CaptureSuccess()]);
        var result = Structured(new OnshapeSkillTools(provider.Provider).skill_onshape_god_part(AddressJson(), 0, 0, 0, 0, 2, $$"""{"profile":{{profile}}}"""));
        result["success"].Should().Be(true);
        SketchPayload(provider).GetProperty("feature").GetProperty("entities").GetArrayLength().Should().Be(lineCount);
        SketchPayload(provider).GetProperty("feature").GetProperty("constraints").GetArrayLength().Should().Be(lineCount);
    }

    [Fact]
    public void skill_onshape_god_part_AcceptsRepeatedClosingProfilePoint()
    {
        var provider = RecordingProvider.Create([Success("sketch-1"), Success("extrude-1")], [CaptureSuccess(), CaptureSuccess()]);
        var result = Structured(new OnshapeSkillTools(provider.Provider).skill_onshape_god_part(AddressJson(), 0, 0, 0, 0, 2, """{"profile":[{"x_cm":0,"y_cm":0},{"x_cm":4,"y_cm":0},{"x_cm":2,"y_cm":3},{"x_cm":0,"y_cm":0}]}"""));
        result["success"].Should().Be(true);
        JsonSerializer.Serialize(provider.Calls.Single(call => call.Method == nameof(ICadWebProvider.SketchReplace)).Arguments[1]).Should().Contain("closed-profile-line-2").And.NotContain("closed-profile-line-3");
    }

    [Fact]
    public void skill_onshape_god_part_UsesStrictSingleArcProfile()
        {
            var provider = RecordingProvider.Create([Success("sketch-1"), Success("extrude-1")], [CaptureSuccess(), CaptureSuccess()]);
            var result = Structured(new OnshapeSkillTools(provider.Provider).skill_onshape_god_part(AddressJson(), 0, 0, 0, 0, 2,
                """{"profile":{"start":{"x_cm":0,"y_cm":0},"segments":[{"type":"arc","through":{"x_cm":1,"y_cm":-1},"end":{"x_cm":2,"y_cm":0}},{"type":"line","end":{"x_cm":2,"y_cm":2}},{"type":"line","end":{"x_cm":0,"y_cm":2}},{"type":"line","end":{"x_cm":0,"y_cm":0}}]}}"""));

            result["success"].Should().Be(true);
            var sketch = SketchPayload(provider).GetProperty("feature");
            sketch.GetProperty("entities").GetArrayLength().Should().Be(4);
            sketch.GetProperty("entities")[0].GetProperty("geometry").GetProperty("btType").GetString().Should().Be("BTCurveGeometryCircle-115");
            sketch.GetProperty("constraints").GetArrayLength().Should().Be(4);
        }

        [Theory]
        [InlineData("""{"profile":{"start":null,"segments":[]}}""", "profile.start must be an object.")]
        [InlineData("""{"profile":{"start":{"x_cm":0,"y_cm":0},"segments":[{"type":"line","end":{"x_cm":0,"y_cm":0},"extra":true}]}}""", "Unknown option 'extra'.")]
        [InlineData("""{"profile":{"start":{"x_cm":0,"y_cm":0},"start":{"x_cm":1,"y_cm":0},"segments":[]}}""", "Duplicate option 'start'.")]
        [InlineData("""{"profile":{"segments":[]}}""", "profile.start is required.")]
        public void skill_onshape_god_part_RejectsMalformedStrictProfileBeforeProviderMutation(string options, string error) => AssertValidationNoCalls(options, error);

        [Fact]
        public void skill_onshape_god_part_UsesStrictFourArcProfile()
        {
            var provider = RecordingProvider.Create([Success("sketch-1"), Success("extrude-1")], [CaptureSuccess(), CaptureSuccess()]);
            var result = Structured(new OnshapeSkillTools(provider.Provider).skill_onshape_god_part(AddressJson(), 0, 0, 0, 0, 2, RoundedRectangleOptions()));

            result["success"].Should().Be(true);
            var sketch = SketchPayload(provider).GetProperty("feature");
            sketch.GetProperty("entities").GetArrayLength().Should().Be(8);
            sketch.GetProperty("entities").EnumerateArray().Count(entity => entity.GetProperty("geometry").GetProperty("btType").GetString() == "BTCurveGeometryCircle-115").Should().Be(4);
            sketch.GetProperty("constraints").GetArrayLength().Should().Be(8);
        }

        [Fact]
        public void skill_onshape_god_part_AcceptsEightArcsAtBoundary()
        {
            var provider = RecordingProvider.Create([Success("sketch-1"), Success("extrude-1")], [CaptureSuccess(), CaptureSuccess()]);
            Structured(new OnshapeSkillTools(provider.Provider).skill_onshape_god_part(AddressJson(), 0, 0, 0, 0, 2, ArcRingOptions(8)))["success"].Should().Be(true);
            SketchPayload(provider).GetProperty("feature").GetProperty("entities").GetArrayLength().Should().Be(8);
        }

        [Fact]
        public void skill_onshape_god_part_RejectsNineArcsBeforeProviderMutation() =>
            AssertValidationNoCalls(ArcRingOptions(9), "1 to 8 arcs");

        [Fact]
        public void skill_onshape_god_part_UsesStrictTwoArcProfile()
    {
        var provider = RecordingProvider.Create([Success("sketch-1"), Success("extrude-1")], [CaptureSuccess(), CaptureSuccess()]);
        var result = Structured(new OnshapeSkillTools(provider.Provider).skill_onshape_god_part(AddressJson(), 0, 0, 0, 0, 2, """{"profile":{"start":{"x_cm":0,"y_cm":0},"segments":[{"type":"arc","through":{"x_cm":1,"y_cm":-1},"end":{"x_cm":2,"y_cm":0}},{"type":"line","end":{"x_cm":2,"y_cm":4}},{"type":"arc","through":{"x_cm":1,"y_cm":5},"end":{"x_cm":0,"y_cm":4}},{"type":"line","end":{"x_cm":0,"y_cm":0}}]}}"""));
        result["success"].Should().Be(true);
        var sketch = SketchPayload(provider).GetProperty("feature");
            sketch.GetProperty("entities").GetArrayLength().Should().Be(4);
            sketch.GetProperty("entities").EnumerateArray().Count(entity => entity.GetProperty("geometry").GetProperty("btType").GetString() == "BTCurveGeometryCircle-115").Should().Be(2);
            sketch.GetProperty("constraints").GetArrayLength().Should().Be(4);
    }

    [Fact]
    public void skill_onshape_god_part_RejectsZeroArcsBeforeProviderMutation() =>
            AssertValidationNoCalls("""{"profile":{"start":{"x_cm":0,"y_cm":0},"segments":[{"type":"line","end":{"x_cm":2,"y_cm":0}},{"type":"line","end":{"x_cm":0,"y_cm":0}}]}}""", "1 to 8 arcs");

        [Fact]
        public void skill_onshape_god_part_PreservesRectangleFallbackPayload()
    {
        var provider = RecordingProvider.Create([Success("sketch-1"), Success("extrude-1")], [CaptureSuccess(), CaptureSuccess()]);
        Structured(new OnshapeSkillTools(provider.Provider).skill_onshape_god_part(AddressJson(), 0, 0, 10, 5, 2))["success"].Should().Be(true);
        SketchPayload(provider).GetProperty("feature").GetProperty("entities").GetArrayLength().Should().Be(4);
        SketchPayload(provider).GetProperty("feature").GetProperty("constraints").GetArrayLength().Should().Be(0);
    }

    [Fact]
    public void skill_onshape_god_part_StopsAfterFailedDependentFeatureAndReportsPartialCompletion()
    {
        var provider = RecordingProvider.Create([Success("sketch-1"), Success("extrude-1"), Failure("fillet rejected")]);
        var result = Structured(new OnshapeSkillTools(provider.Provider).skill_onshape_god_part(AddressJson(), 0, 0, 10, 5, 2, """{"fillet":{"edgeIds":["edge-1"],"radiusCm":0.2},"mirror":{"plane":"Top"}}"""));
        result["failedPhase"].Should().Be("fillet"); result["finalFeatureId"].Should().Be("extrude-1"); provider.Calls.Should().HaveCount(3);
    }

    private static string RoundedRectangleOptions() =>
        """{"profile":{"start":{"x_cm":1,"y_cm":0},"segments":[{"type":"line","end":{"x_cm":9,"y_cm":0}},{"type":"arc","through":{"x_cm":9.707106781186548,"y_cm":0.2928932188134524},"end":{"x_cm":10,"y_cm":1}},{"type":"line","end":{"x_cm":10,"y_cm":5}},{"type":"arc","through":{"x_cm":9.707106781186548,"y_cm":5.707106781186548},"end":{"x_cm":9,"y_cm":6}},{"type":"line","end":{"x_cm":1,"y_cm":6}},{"type":"arc","through":{"x_cm":0.2928932188134524,"y_cm":5.707106781186548},"end":{"x_cm":0,"y_cm":5}},{"type":"line","end":{"x_cm":0,"y_cm":1}},{"type":"arc","through":{"x_cm":0.2928932188134524,"y_cm":0.2928932188134524},"end":{"x_cm":1,"y_cm":0}}]}}""";

    private static string ArcRingOptions(int count) => JsonSerializer.Serialize(new
    {
        profile = new
        {
            start = new { x_cm = 5d, y_cm = 0d },
            segments = Enumerable.Range(1, count).Select(index =>
            {
                var start = (index - 1) * 2 * Math.PI / count;
                var end = index * 2 * Math.PI / count;
                return new
                {
                    type = "arc",
                    through = new { x_cm = 5 * Math.Cos((start + end) / 2), y_cm = 5 * Math.Sin((start + end) / 2) },
                    end = new { x_cm = index == count ? 5d : 5 * Math.Cos(end), y_cm = index == count ? 0d : 5 * Math.Sin(end) },
                };
            }),
        },
    });

    private static void AssertValidationNoCalls(string options, string? expectedError = null)
    {
        var provider = RecordingProvider.Create([]);
        var result = new OnshapeSkillTools(provider.Provider).skill_onshape_god_part(AddressJson(), 0, 0, 10, 5, 2, options);
        result.IsError.Should().BeTrue(); Structured(result)["failedPhase"].Should().Be("validation"); provider.Calls.Should().BeEmpty();
            if (expectedError is not null) Structured(result)["error"].Should().BeOfType<string>().Which.Should().Contain(expectedError);
    }
    private static JsonElement SketchPayload(RecordingProvider provider)
    {
        var request = Assert.IsType<Dictionary<string, object?>>(provider.Calls.Single(call => call.Method == nameof(ICadWebProvider.SketchReplace)).Arguments[1]);
        return JsonDocument.Parse(JsonSerializer.Serialize(request)).RootElement.Clone();
    }
    private static Dictionary<string, object?> Structured(CallToolResult result) => Assert.IsType<Dictionary<string, object?>>(Normalize(result.StructuredContent!.Value));
    private static object? Normalize(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.Object => value.EnumerateObject().ToDictionary(property => property.Name, property => Normalize(property.Value)), JsonValueKind.Array => value.EnumerateArray().Select(Normalize).ToList(),
        JsonValueKind.String => value.GetString(), JsonValueKind.True => true, JsonValueKind.False => false, JsonValueKind.Null => null, JsonValueKind.Number when value.TryGetInt64(out var integer) => integer, JsonValueKind.Number => value.GetDouble(), _ => throw new InvalidOperationException($"Unexpected JSON value: {value.ValueKind}"),
    };
    private static void AssertFeatureListSource((string Method, object?[] Arguments) call, string expectedFeatureId)
    {
        using var payload = JsonDocument.Parse(Assert.IsType<string>(call.Arguments[3]));
        payload.RootElement.GetProperty("feature").GetProperty("parameters").EnumerateArray().Single(parameter => parameter.GetProperty("parameterId").GetString() == "instanceFunction").GetProperty("featureIds")[0].GetString().Should().Be(expectedFeatureId);
    }
    private static Dictionary<string, object?> Success(string featureId) => new() { ["success"] = true, ["featureId"] = featureId };
    private static Dictionary<string, object?> NestedSuccess(string featureId) => new() { ["success"] = true, ["feature"] = new Dictionary<string, object?> { ["featureId"] = featureId } };
    private static Dictionary<string, object?> Failure(string error) => new() { ["success"] = false, ["error"] = error };
    private static Dictionary<string, object?> CaptureSuccess() => new() { ["success"] = true, ["image_base64"] = "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVQIHWP4z8DwHwAFgAI/ScL1ZQAAAABJRU5ErkJggg==", ["mime_type"] = "image/png" };
    private static string AddressJson() => new CadElementAddress("doc", "workspace", null, "element").Serialize();

    private class RecordingProvider : DispatchProxy
    {
        private readonly Queue<Dictionary<string, object?>> _results = []; private readonly Queue<Dictionary<string, object?>> _captures = [];
        public List<(string Method, object?[] Arguments)> Calls { get; } = []; public ICadWebProvider Provider { get; private set; } = null!;
        public static RecordingProvider Create(IEnumerable<Dictionary<string, object?>> results, IEnumerable<Dictionary<string, object?>>? captures = null)
        {
            var provider = DispatchProxy.Create<ICadWebProvider, RecordingProvider>(); var recording = (RecordingProvider)(object)provider; recording.Provider = provider;
            foreach (var result in results) recording._results.Enqueue(result); foreach (var capture in captures ?? []) recording._captures.Enqueue(capture); return recording;
        }
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            var arguments = args ?? []; Calls.Add((targetMethod!.Name, arguments));
            return targetMethod.Name switch { nameof(ICadWebProvider.SketchReplace) or nameof(ICadWebProvider.FeatureAdd) => _results.Dequeue(), nameof(ICadWebProvider.CaptureView) => _captures.Dequeue(), _ => throw new NotSupportedException($"Unexpected provider call: {targetMethod.Name}"), };
        }
    }
}
