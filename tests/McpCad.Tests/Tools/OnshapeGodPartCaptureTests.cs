using System.Reflection;
using System.Text.Json;
using McpCad.Core;
using McpCad.Core.Models;
using McpCad.Tools.Onshape;
using ModelContextProtocol.Protocol;

namespace McpCad.Tests.Tools;

public class OnshapeGodPartCaptureTests
{
    private const string MinimalPngBase64 = "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVQIHWP4z8DwHwAFgAI/ScL1ZQAAAABJRU5ErkJggg==";

    [Fact]
    public void skill_onshape_god_part_ReturnsIsoThenTopImagesWithCaptureMetadataAfterModeling()
    {
        var provider = RecordingProvider.Create([Success("sketch-1"), Success("extrude-1")], [CaptureSuccess(), CaptureSuccess()]);
        var result = new OnshapeSkillTools(provider.Provider).skill_onshape_god_part(AddressJson(), 0, 0, 10, 5, 2);

        result.IsError.Should().NotBe(true);
        result.Content.Should().HaveCount(2);
        result.Content.Should().OnlyContain(content => content is ImageContentBlock);
        var structured = Structured(result);
        structured["success"].Should().Be(true);
        structured["modelingSuccess"].Should().Be(true);
        structured["captureSuccess"].Should().Be(true);
        var captures = Assert.IsType<List<object?>>(structured["captures"]);
        captures.Select(capture => Assert.IsType<Dictionary<string, object?>>(capture)["view"]).Should().Equal("iso", "top");
        provider.Calls.Select(call => call.Method).Should().Equal(nameof(ICadWebProvider.SketchReplace), nameof(ICadWebProvider.FeatureAdd), nameof(ICadWebProvider.CaptureView), nameof(ICadWebProvider.CaptureView));
    }

    [Fact]
    public void skill_onshape_god_part_SkipsCapturesWhenModelingFails()
    {
        var provider = RecordingProvider.Create([Success("sketch-1"), Failure("extrude rejected")]);
        var result = new OnshapeSkillTools(provider.Provider).skill_onshape_god_part(AddressJson(), 0, 0, 10, 5, 2);

        result.IsError.Should().BeTrue();
        var structured = Structured(result);
        structured["modelingSuccess"].Should().Be(false);
        structured["captureSuccess"].Should().BeNull();
        provider.Calls.Should().NotContain(call => call.Method == nameof(ICadWebProvider.CaptureView));
    }

    [Fact]
    public void skill_onshape_god_part_AttemptsTopWhenIsoCaptureFails()
    {
        var provider = RecordingProvider.Create([Success("sketch-1"), Success("extrude-1")], [Failure("iso unavailable"), CaptureSuccess()]);
        var result = new OnshapeSkillTools(provider.Provider).skill_onshape_god_part(AddressJson(), 0, 0, 10, 5, 2);

        result.IsError.Should().BeTrue();
        result.Content.OfType<ImageContentBlock>().Should().ContainSingle();
        var captures = Assert.IsType<List<object?>>(Structured(result)["captures"]);
        Assert.False((bool)Assert.IsType<Dictionary<string, object?>>(captures[0])["success"]!);
        Assert.True((bool)Assert.IsType<Dictionary<string, object?>>(captures[1])["success"]!);
        provider.Calls.Count(call => call.Method == nameof(ICadWebProvider.CaptureView)).Should().Be(2);
    }

    [Theory]
    [InlineData(0, 600, "show")]
    [InlineData(800, 4097, "show")]
    [InlineData(800, 600, "outline")]
    public void skill_onshape_god_part_RejectsInvalidCaptureOptionsWithStablePreflightSchemaBeforeProviderMutation(int width, int height, string edges)
    {
        var provider = RecordingProvider.Create([]);
        var result = new OnshapeSkillTools(provider.Provider).skill_onshape_god_part(AddressJson(), 0, 0, 10, 5, 2, capture_width: width, capture_height: height, capture_edges: edges);

        result.IsError.Should().BeTrue();
        var structured = Structured(result);
        structured["success"].Should().Be(false);
        structured["modelingSuccess"].Should().Be(false);
        structured["captureSuccess"].Should().Be(false);
        structured["failedPhase"].Should().Be("validation");
        structured["captures"].Should().BeAssignableTo<List<object?>>().Which.Should().BeEmpty();
        structured["phases"].Should().BeAssignableTo<Dictionary<string, object?>>();
        structured["error"].Should().BeOfType<string>();
        provider.Calls.Should().BeEmpty();
    }

    private static Dictionary<string, object?> Structured(CallToolResult result) => Assert.IsType<Dictionary<string, object?>>(Normalize(result.StructuredContent!.Value));
    private static object? Normalize(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.Object => value.EnumerateObject().ToDictionary(property => property.Name, property => Normalize(property.Value)),
        JsonValueKind.Array => value.EnumerateArray().Select(Normalize).ToList(),
        JsonValueKind.String => value.GetString(), JsonValueKind.True => true, JsonValueKind.False => false, JsonValueKind.Null => null,
        JsonValueKind.Number when value.TryGetInt64(out var integer) => integer, JsonValueKind.Number => value.GetDouble(),
        _ => throw new InvalidOperationException($"Unexpected JSON value: {value.ValueKind}"),
    };
    private static Dictionary<string, object?> Success(string featureId) => new() { ["success"] = true, ["featureId"] = featureId };
    private static Dictionary<string, object?> Failure(string error) => new() { ["success"] = false, ["error"] = error };
    private static Dictionary<string, object?> CaptureSuccess() => new() { ["success"] = true, ["image_base64"] = MinimalPngBase64, ["mime_type"] = "image/png" };
    private static string AddressJson() => new CadElementAddress("doc", "workspace", null, "element").Serialize();

    private class RecordingProvider : DispatchProxy
    {
        private readonly Queue<Dictionary<string, object?>> _results = [];
        private readonly Queue<Dictionary<string, object?>> _captures = [];
        public List<(string Method, object?[] Arguments)> Calls { get; } = [];
        public ICadWebProvider Provider { get; private set; } = null!;
        public static RecordingProvider Create(IEnumerable<Dictionary<string, object?>> results, IEnumerable<Dictionary<string, object?>>? captures = null)
        {
            var provider = DispatchProxy.Create<ICadWebProvider, RecordingProvider>(); var recording = (RecordingProvider)(object)provider; recording.Provider = provider;
            foreach (var result in results) recording._results.Enqueue(result); foreach (var capture in captures ?? []) recording._captures.Enqueue(capture); return recording;
        }
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            var arguments = args ?? []; Calls.Add((targetMethod!.Name, arguments));
            return targetMethod.Name switch
            {
                nameof(ICadWebProvider.SketchReplace) or nameof(ICadWebProvider.FeatureAdd) => _results.Dequeue(),
                nameof(ICadWebProvider.CaptureView) => _captures.Dequeue(), _ => throw new NotSupportedException($"Unexpected provider call: {targetMethod.Name}"),
            };
        }
    }
}
