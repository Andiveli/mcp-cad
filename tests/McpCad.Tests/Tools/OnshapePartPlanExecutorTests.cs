using System.Collections;
using System.Reflection;
using System.Text.Json;
using McpCad.Core;
using McpCad.Tools.Onshape;
using Moq;

namespace McpCad.Tests.Tools;

public class OnshapePartPlanExecutorTests
{
    private static readonly Assembly Tools = typeof(OnshapeSkillTools).Assembly;
    private const string Address = """{"documentId":"doc","workspaceOrVersionId":"ws","elementId":"el"}""";

    [Fact]
    public void Execute_PreservesDeclarationOrderBindsOpaqueSketchIdAndBuildsMeterExtrude()
    {
        var calls = new List<string>();
        var provider = Provider(calls);
        provider.Setup(mock => mock.SketchReplace(It.IsAny<McpCad.Core.Models.CadElementAddress>(), It.IsAny<IReadOnlyDictionary<string, object?>?>()))
            .Callback(() => calls.Add("sketch"))
            .Returns(new Dictionary<string, object?> { ["success"] = true, ["featureId"] = " sketch native " });
        provider.Setup(mock => mock.FeatureAdd(It.IsAny<McpCad.Core.Models.CadElementAddress>(), "extrude", null, It.IsAny<string?>()))
            .Callback<McpCad.Core.Models.CadElementAddress, string, IReadOnlyList<Dictionary<string, object?>>?, string?>((_, _, _, body) =>
            {
                calls.Add("extrude");
                using var json = JsonDocument.Parse(body!);
                var parameters = json.RootElement.GetProperty("feature").GetProperty("parameters");
                parameters[0].GetProperty("expression").GetString().Should().Be("0.02 m");
                parameters[1].GetProperty("value").GetString().Should().Be("positive");
                parameters[2].GetProperty("value").GetString().Should().Be("NEW");
                parameters[3].GetProperty("queries")[0].GetProperty("featureId").GetString().Should().Be(" sketch native ");
            })
            .Returns(new Dictionary<string, object?> { ["success"] = true, ["featureId"] = " ", ["feature"] = new Dictionary<string, object?> { ["featureId"] = "extrude-native" } });

        var state = Execute(provider.Object, Plan(Sketch("s"), Extrude("e", "s")));

        calls.Should().Equal("sketch", "extrude");
        Stages(state).Select(stage => Value<string>(stage, "NativeFeatureId")).Should().Equal(" sketch native ", "extrude-native");
    }

    [Fact]
    public void Execute_StopsAfterFirstModelingFailureAndDoesNotRetryRollbackOrCaptureLaterStages()
    {
        var calls = new List<string>();
        var provider = Provider(calls);
        provider.Setup(mock => mock.SketchReplace(It.IsAny<McpCad.Core.Models.CadElementAddress>(), It.IsAny<IReadOnlyDictionary<string, object?>?>()))
            .Callback(() => calls.Add("sketch"))
            .Returns(new Dictionary<string, object?> { ["success"] = false, ["error"] = "microversion timeout; mutation outcome is ambiguous" });

        var state = Execute(provider.Object, Plan(Sketch("s"), Checkpoint("c"), Sketch("later")));

        calls.Should().Equal("sketch");
        Stages(state).Select(stage => Value<object>(stage, "ModelingStatus").ToString()).Should().Equal("Failed", "NotApplicable", "NotAttempted");
        Stages(state).Select(stage => Value<object>(stage, "CaptureStatus").ToString()).Should().Equal("NotApplicable", "NotAttempted", "NotApplicable");
        provider.Verify(mock => mock.FeatureAdd(It.IsAny<McpCad.Core.Models.CadElementAddress>(), It.IsAny<string>(), It.IsAny<IReadOnlyList<Dictionary<string, object?>>?>(), It.IsAny<string?>()), Times.Never);
        provider.Verify(mock => mock.CaptureView(It.IsAny<McpCad.Core.Models.CadElementAddress>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public void Execute_ContinuesAfterCaptureFailuresAndRecordsOnlyValidBase64InDeclarationOrder()
    {
        var calls = new List<string>();
        var provider = Provider(calls);
        provider.Setup(mock => mock.SketchReplace(It.IsAny<McpCad.Core.Models.CadElementAddress>(), It.IsAny<IReadOnlyDictionary<string, object?>?>()))
            .Callback(() => calls.Add("sketch"))
            .Returns(new Dictionary<string, object?> { ["success"] = true, ["featureId"] = "sketch-native" });
        provider.Setup(mock => mock.FeatureAdd(It.IsAny<McpCad.Core.Models.CadElementAddress>(), "extrude", null, It.IsAny<string?>()))
            .Callback(() => calls.Add("extrude"))
            .Returns(new Dictionary<string, object?> { ["success"] = true, ["featureId"] = "extrude-native" });
        provider.SetupSequence(mock => mock.CaptureView(It.IsAny<McpCad.Core.Models.CadElementAddress>(), "iso", 800, 600, "show"))
            .Returns(() => { calls.Add("capture:iso"); return new Dictionary<string, object?> { ["success"] = true, ["image_base64"] = " " }; })
            .Returns(() => { calls.Add("capture:iso"); return new Dictionary<string, object?> { ["success"] = true, ["image_base64"] = "not base64" }; })
            .Returns(() => { calls.Add("capture:iso"); return new Dictionary<string, object?> { ["success"] = true, ["image_base64"] = Convert.ToBase64String([1, 2, 3]) }; });

        var state = Execute(provider.Object, Plan(Sketch("s"), Checkpoint("first"), Checkpoint("second"), Checkpoint("third"), Extrude("e", "s")));

        calls.Should().Equal("sketch", "capture:iso", "capture:iso", "capture:iso", "extrude");
        Captures(state).Select(capture => Value<object>(capture, "Status").ToString()).Should().Equal("Failed", "Failed", "Completed");
        Stages(state).Last().GetType().GetProperty("StageStatus")!.GetValue(Stages(state).Last())!.ToString().Should().Be("Completed");
    }

    [Fact]
    public void Execute_TreatsMissingIdsFalseResultsAndProviderExceptionsAsActiveStageFailuresWithoutRetry()
    {
        var calls = new List<string>();
        var provider = Provider(calls);
        provider.SetupSequence(mock => mock.SketchReplace(It.IsAny<McpCad.Core.Models.CadElementAddress>(), It.IsAny<IReadOnlyDictionary<string, object?>?>()))
            .Returns(() => { calls.Add("sketch"); return new Dictionary<string, object?> { ["success"] = true }; })
            .Returns(() => { calls.Add("sketch"); throw new TimeoutException("transport timeout"); });

        var missing = Execute(provider.Object, Plan(Sketch("missing"), Sketch("later")));
        Value<object>(Stages(missing).First(), "Error").ToString().Should().Contain("MISSING_NATIVE_FEATURE_ID");
        calls.Should().Equal("sketch");

        var thrown = Execute(provider.Object, Plan(Sketch("throws")));
        Value<object>(Stages(thrown).First(), "Error").ToString().Should().Contain("PROVIDER_MODELING_FAILED");
        calls.Should().Equal("sketch", "sketch");
    }

    private static Mock<ICadWebProvider> Provider(List<string> calls) => new(MockBehavior.Strict);

    private static object Execute(ICadWebProvider provider, string plan)
    {
        var parsed = Parser().Invoke(null, [Address, plan])!;
        var validated = Validator().Invoke(null, [Item(parsed, "Item1"), Item(parsed, "Item2")])!;
        var executor = Activator.CreateInstance(Type("OnshapePartPlanExecutor"), provider)!;
        return Type("OnshapePartPlanExecutor").GetMethod("Execute")!.Invoke(executor, [validated])!;
    }

    private static object[] Stages(object state) => ((IEnumerable)state.GetType().GetProperty("Stages")!.GetValue(state)!).Cast<object>().ToArray();
    private static object[] Captures(object state) => ((IEnumerable)state.GetType().GetProperty("Captures")!.GetValue(state)!).Cast<object>().ToArray();
    private static T Value<T>(object value, string property) => (T)value.GetType().GetProperty(property)!.GetValue(value)!;
    private static string Plan(params string[] stages) => "{\"schemaVersion\":1,\"stages\":[" + string.Join(',', stages) + "]}";
    private static string Sketch(string id) => "{\"id\":\"" + id + "\",\"kind\":\"sketch\",\"plane\":\"Top\",\"profile\":{\"type\":\"circle\",\"cx_cm\":0,\"cy_cm\":0,\"radius_cm\":1}}";
    private static string Extrude(string id, string sketchId) => "{\"id\":\"" + id + "\",\"kind\":\"extrude\",\"sketchId\":\"" + sketchId + "\",\"operation\":\"new\",\"depth_cm\":2,\"direction\":\"positive\"}";
    private static string Checkpoint(string id) => "{\"id\":\"" + id + "\",\"kind\":\"checkpoint\",\"afterStageId\":\"s\",\"view\":\"iso\"}";
    private static Type Type(string name) => Tools.GetType("McpCad.Tools.Onshape.Orchestration." + name, true)!;
    private static MethodInfo Parser() => Type("OnshapePartPlanParser").GetMethod("Parse", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)!;
    private static MethodInfo Validator() => Type("OnshapePartPlanValidator").GetMethod("Validate", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)!;
    private static object Item(object tuple, string name) => tuple.GetType().GetField(name)!.GetValue(tuple)!;
}
