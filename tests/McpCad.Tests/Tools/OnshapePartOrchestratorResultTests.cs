using System.Collections;
using System.Reflection;
using System.Text.Json;
using McpCad.Core;
using McpCad.Tools.Onshape;
using ModelContextProtocol.Protocol;
using Moq;

namespace McpCad.Tests.Tools;

public class OnshapePartOrchestratorResultTests
{
    private static readonly Assembly Tools = typeof(OnshapeSkillTools).Assembly;
    private const string Address = """{"documentId":"doc","workspaceOrVersionId":"ws","elementId":"el"}""";
    private const string Png = "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVQIHWP4z8DwHwAFgAI/ScL1ZQAAAABJRU5ErkJggg==";

    [Fact]
    public void Build_ProjectsExactCompletedEnvelopeAndOrderedCheckpointImages()
    {
        var provider = Provider();
        provider.Setup(mock => mock.SketchReplace(It.IsAny<McpCad.Core.Models.CadElementAddress>(), It.IsAny<IReadOnlyDictionary<string, object?>?>()))
            .Returns(Success("sketch-native"));
        provider.Setup(mock => mock.FeatureAdd(It.IsAny<McpCad.Core.Models.CadElementAddress>(), "extrude", null, It.IsAny<string?>()))
            .Returns(Success("extrude-native"));
        provider.SetupSequence(mock => mock.CaptureView(It.IsAny<McpCad.Core.Models.CadElementAddress>(), It.IsAny<string>(), 800, 600, "show"))
            .Returns(CaptureSuccess("iso", new Dictionary<string, object?> { ["nested"] = new Dictionary<string, object?> { ["source"] = "camera" } }))
            .Returns(CaptureSuccess("top"));

        var result = Build(Execute(provider.Object, Plan(Sketch("s"), Checkpoint("iso", "s", "iso"), Extrude("e", "s"), Checkpoint("top", "e", "top"))));
        var root = Root(result);

        result.IsError.Should().NotBe(true);
        result.Content.Should().HaveCount(2).And.OnlyContain(block => block is ImageContentBlock);
        root.Keys.Should().BeEquivalentTo("schemaVersion", "status", "modeling", "stages", "captures");
        root["schemaVersion"].Should().Be(1L);
        root["status"].Should().Be("completed");
        var modeling = Object(root["modeling"]);
        modeling.Keys.Should().BeEquivalentTo("status", "lastSuccessfulFeatureId", "failedStageId", "error");
        modeling.Should().Contain("status", "completed").And.Contain("lastSuccessfulFeatureId", "extrude-native").And.Contain("failedStageId", null).And.Contain("error", null);
        var stages = Array(root["stages"]);
        stages.Select(stage => Object(stage)["id"]).Should().Equal("s", "iso", "e", "top");
        stages.Select(stage => Object(stage)["sequence"]).Should().Equal(0L, 1L, 2L, 3L);
        stages.Should().OnlyContain(stage => Object(stage).Keys.OrderBy(key => key).SequenceEqual(new[] { "captureStatus", "error", "id", "kind", "modelingStatus", "nativeFeatureId", "sequence", "stageStatus" }));
        Object(stages[1])["modelingStatus"].Should().Be("not_applicable");
        Object(stages[1])["captureStatus"].Should().Be("completed");
        Object(stages[1])["nativeFeatureId"].Should().BeNull();
        Object(stages[1])["error"].Should().BeNull();
        var captures = Object(root["captures"]);
        captures.Keys.Should().BeEquivalentTo("status", "results");
        captures["status"].Should().Be("completed");
        var captureResults = Array(captures["results"]);
        captureResults.Select(capture => Object(capture)["stageId"]).Should().Equal("iso", "top");
        Object(captureResults[0]).Keys.Should().BeEquivalentTo("stageId", "view", "status", "evidence", "error");
        Object(Object(captureResults[0])["evidence"])["nested"].Should().BeOfType<Dictionary<string, object?>>().Which["source"].Should().Be("camera");
        Object(captureResults[0])["error"].Should().BeNull();
    }

    [Fact]
    public void Build_SeparatesCaptureFailureAndInvalidImageFromCompletedModeling()
    {
        var provider = Provider();
        provider.Setup(mock => mock.SketchReplace(It.IsAny<McpCad.Core.Models.CadElementAddress>(), It.IsAny<IReadOnlyDictionary<string, object?>?>()))
            .Returns(Success("sketch-native"));
        provider.SetupSequence(mock => mock.CaptureView(It.IsAny<McpCad.Core.Models.CadElementAddress>(), It.IsAny<string>(), 800, 600, "show"))
            .Returns(new Dictionary<string, object?> { ["success"] = false, ["error"] = "camera unavailable" })
            .Returns(new Dictionary<string, object?> { ["success"] = true, ["image_base64"] = "not-base64" });

        var result = Build(Execute(provider.Object, Plan(Sketch("s"), Checkpoint("failed", "s", "iso"), Checkpoint("invalid", "s", "top"))));
        var root = Root(result);
        var captures = Object(root["captures"]);
        var captureResults = Array(captures["results"]);

        result.IsError.Should().BeTrue();
        result.Content.Should().BeEmpty();
        root["status"].Should().Be("completed_with_capture_failures");
        Object(root["modeling"])["status"].Should().Be("completed");
        captures["status"].Should().Be("partial_failure");
        captureResults.Select(capture => Object(capture)["status"]).Should().Equal("failed", "failed");
        captureResults.Should().OnlyContain(capture => Object(capture)["evidence"] == null);
        Object(captureResults[0])["error"].Should().BeOfType<Dictionary<string, object?>>().Which["code"].Should().Be("PROVIDER_CAPTURE_FAILED");
        Object(captureResults[1])["error"].Should().BeOfType<Dictionary<string, object?>>().Which["code"].Should().Be("INVALID_CAPTURE_IMAGE");
        Array(root["stages"]).Should().OnlyContain(stage => Object(stage)["error"] == null);
    }

    [Fact]
    public void Build_ProjectsModelingFailureWithExplicitSkippedCheckpointAndLaterModeling()
    {
        var provider = Provider();
        provider.Setup(mock => mock.SketchReplace(It.IsAny<McpCad.Core.Models.CadElementAddress>(), It.IsAny<IReadOnlyDictionary<string, object?>?>()))
            .Returns(Success("sketch-native"));
        provider.Setup(mock => mock.FeatureAdd(It.IsAny<McpCad.Core.Models.CadElementAddress>(), "extrude", null, It.IsAny<string?>()))
            .Returns(new Dictionary<string, object?> { ["success"] = false, ["error"] = "rejected" });

        var result = Build(Execute(provider.Object, Plan(Sketch("s"), Extrude("e", "s"), Checkpoint("after-e", "e", "iso"), Sketch("later"))));
        var root = Root(result);
        var stages = Array(root["stages"]).Select(Object).ToArray();

        result.IsError.Should().BeTrue();
        result.Content.Should().BeEmpty();
        root["status"].Should().Be("partial_modeling_failure");
        Object(root["modeling"]).Should().Contain("status", "failed").And.Contain("lastSuccessfulFeatureId", "sketch-native").And.Contain("failedStageId", "e");
        Object(Object(root["modeling"])["error"])["code"].Should().Be("PROVIDER_MODELING_FAILED");
        stages.Select(stage => stage["stageStatus"]).Should().Equal("completed", "failed", "skipped", "skipped");
        stages[2].Should().Contain("modelingStatus", "not_applicable").And.Contain("captureStatus", "not_attempted").And.Contain("error", null);
        stages[3].Should().Contain("modelingStatus", "not_attempted").And.Contain("captureStatus", "not_applicable").And.Contain("error", null);
        Object(root["captures"])["status"].Should().Be("none");
        Array(Object(root["captures"])["results"]).Should().BeEmpty();
        provider.Verify(mock => mock.CaptureView(It.IsAny<McpCad.Core.Models.CadElementAddress>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public void PreflightError_ProjectsCompactTypedErrorWithoutExecutionEnvelope()
    {
        var error = Activator.CreateInstance(Type("OrchestrationError"), "INVALID_PLAN_SCHEMA", "stages[1].kind is unsupported")!;
        var result = (CallToolResult)Type("OnshapeOrchestrationResultBuilder").GetMethod("PreflightError")!.Invoke(null, [error])!;
        var root = Root(result);

        result.IsError.Should().BeTrue();
        result.Content.Should().ContainSingle().Which.Should().BeOfType<TextContentBlock>();
        root.Keys.Should().BeEquivalentTo("error");
        Object(root["error"]).Should().Contain("code", "INVALID_PLAN_SCHEMA").And.Contain("message", "stages[1].kind is unsupported");
    }

    [Fact]
    public void Build_ProjectsCompletedModelingWithoutCheckpoints()
    {
        var provider = Provider();
        provider.Setup(mock => mock.SketchReplace(It.IsAny<McpCad.Core.Models.CadElementAddress>(), It.IsAny<IReadOnlyDictionary<string, object?>?>()))
            .Returns(Success("sketch-native"));

        var result = Build(Execute(provider.Object, Plan(Sketch("s"))));
        var root = Root(result);

        result.IsError.Should().NotBe(true);
        result.Content.Should().BeEmpty();
        root["status"].Should().Be("completed");
        Object(root["modeling"])["lastSuccessfulFeatureId"].Should().Be("sketch-native");
        Object(root["captures"])["status"].Should().Be("none");
        Array(Object(root["captures"])["results"]).Should().BeEmpty();
    }

    [Fact]
    public void Build_ProjectsNoCheckpointAndMissingNativeIdAsModelingFailure()
    {
        var provider = Provider();
        provider.Setup(mock => mock.SketchReplace(It.IsAny<McpCad.Core.Models.CadElementAddress>(), It.IsAny<IReadOnlyDictionary<string, object?>?>()))
            .Returns(new Dictionary<string, object?> { ["success"] = true });

        var result = Build(Execute(provider.Object, Plan(Sketch("missing"))));
        var root = Root(result);
        var stage = Object(Array(root["stages"]).Single());

        result.IsError.Should().BeTrue();
        root["status"].Should().Be("partial_modeling_failure");
        Object(root["modeling"])["lastSuccessfulFeatureId"].Should().BeNull();
        stage["nativeFeatureId"].Should().BeNull();
        Object(stage["error"])["code"].Should().Be("MISSING_NATIVE_FEATURE_ID");
        Object(root["captures"])["status"].Should().Be("none");
    }

    private static Mock<ICadWebProvider> Provider() => new(MockBehavior.Strict);
    private static CallToolResult Build(object state) => (CallToolResult)Type("OnshapeOrchestrationResultBuilder").GetMethod("Build")!.Invoke(null, [state])!;
    private static object Execute(ICadWebProvider provider, string plan)
    {
        var parsed = Type("OnshapePartPlanParser").GetMethod("Parse", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)!.Invoke(null, [Address, plan])!;
        var validated = Type("OnshapePartPlanValidator").GetMethod("Validate", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)!.Invoke(null, [Item(parsed, "Item1"), Item(parsed, "Item2")])!;
        var executor = Activator.CreateInstance(Type("OnshapePartPlanExecutor"), provider)!;
        return Type("OnshapePartPlanExecutor").GetMethod("Execute")!.Invoke(executor, [validated])!;
    }

    private static Dictionary<string, object?> Root(CallToolResult result) => Object(Normalize(result.StructuredContent!.Value));
    private static object? Normalize(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.Object => value.EnumerateObject().ToDictionary(property => property.Name, property => Normalize(property.Value)),
        JsonValueKind.Array => value.EnumerateArray().Select(Normalize).ToList(),
        JsonValueKind.String => value.GetString(), JsonValueKind.True => true, JsonValueKind.False => false, JsonValueKind.Null => null,
        JsonValueKind.Number when value.TryGetInt64(out var integer) => integer, JsonValueKind.Number => value.GetDouble(),
        _ => throw new InvalidOperationException($"Unexpected JSON value: {value.ValueKind}"),
    };
    private static Dictionary<string, object?> Object(object? value) => Assert.IsType<Dictionary<string, object?>>(value);
    private static List<object?> Array(object? value) => Assert.IsType<List<object?>>(value);
    private static Dictionary<string, object?> Success(string nativeId) => new() { ["success"] = true, ["featureId"] = nativeId };
    private static Dictionary<string, object?> CaptureSuccess(string view, Dictionary<string, object?>? metadata = null)
    {
        var capture = metadata ?? [];
        capture["success"] = true;
        capture["view"] = view;
        capture["image_base64"] = Png;
        return capture;
    }
    private static string Plan(params string[] stages) => "{\"schemaVersion\":1,\"stages\":[" + string.Join(',', stages) + "]}";
    private static string Sketch(string id) => "{\"id\":\"" + id + "\",\"kind\":\"sketch\",\"plane\":\"Top\",\"profile\":{\"type\":\"circle\",\"cx_cm\":0,\"cy_cm\":0,\"radius_cm\":1}}";
    private static string Extrude(string id, string sketchId) => "{\"id\":\"" + id + "\",\"kind\":\"extrude\",\"sketchId\":\"" + sketchId + "\",\"operation\":\"new\",\"depth_cm\":2,\"direction\":\"positive\"}";
    private static string Checkpoint(string id, string afterStageId, string view) => "{\"id\":\"" + id + "\",\"kind\":\"checkpoint\",\"afterStageId\":\"" + afterStageId + "\",\"view\":\"" + view + "\"}";
    private static Type Type(string name) => Tools.GetType("McpCad.Tools.Onshape.Orchestration." + name, true)!;
    private static object Item(object tuple, string name) => tuple.GetType().GetField(name)!.GetValue(tuple)!;
}
