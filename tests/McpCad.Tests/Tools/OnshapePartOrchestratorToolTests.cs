using System.Reflection;
using McpCad.Core;
using McpCad.Tools.Onshape;
using Moq;

namespace McpCad.Tests.Tools;

public sealed class OnshapePartOrchestratorToolTests
{
    private const string Address = """{"documentId":"doc","workspaceOrVersionId":"ws","elementId":"el"}""";

    [Fact]
    public void onshape_orchestrate_part_ReflectsExactTwoStringSignature()
    {
        var method = ToolMethod();

        method.Should().NotBeNull();
        method!.ReturnType.Name.Should().Be("CallToolResult");
        method.GetParameters().Select(parameter => (parameter.Name, parameter.ParameterType))
            .Should().Equal(("address_json", typeof(string)), ("plan_json", typeof(string)));
    }

    [Fact]
    public void onshape_orchestrate_part_ProjectsPreflightErrorWithoutProviderCallsForLateInvalidPlan()
    {
        var provider = new Mock<ICadWebProvider>(MockBehavior.Strict);

        var result = Invoke(provider.Object, Plan(Sketch("first"), """{"id":"late","kind":"sketch","plane":"Top","profile":{"type":"circle","cx_cm":0,"cy_cm":0,"radius_cm":1},"unexpected":true}"""));

        result.IsError.Should().BeTrue();
        result.StructuredContent!.Value.GetProperty("error").GetProperty("code").GetString().Should().Be("INVALID_PLAN_SCHEMA");
        provider.Invocations.Should().BeEmpty();
    }

    [Fact]
    public void onshape_orchestrate_part_RejectsMalformedAddressWithoutProviderCalls()
    {
        var provider = new Mock<ICadWebProvider>(MockBehavior.Strict);

        var result = Invoke(provider.Object, Plan(Sketch("first")), "{" );

        result.IsError.Should().BeTrue();
        result.StructuredContent!.Value.GetProperty("error").GetProperty("code").GetString().Should().Be("INVALID_JSON");
        provider.Invocations.Should().BeEmpty();
    }

    [Fact]
    public void onshape_orchestrate_part_ComposesInjectedProviderAndProjectsSuccessfulExecution()
    {
        var provider = new Mock<ICadWebProvider>(MockBehavior.Strict);
        provider.Setup(mock => mock.SketchReplace(It.IsAny<McpCad.Core.Models.CadElementAddress>(), It.IsAny<IReadOnlyDictionary<string, object?>?>()))
            .Returns(new Dictionary<string, object?> { ["success"] = true, ["featureId"] = "native-sketch" });

        var result = Invoke(provider.Object, Plan(Sketch("first")));

        result.IsError.Should().NotBe(true);
        result.StructuredContent!.Value.GetProperty("status").GetString().Should().Be("completed");
        result.StructuredContent!.Value.GetProperty("stages")[0].GetProperty("nativeFeatureId").GetString().Should().Be("native-sketch");
        provider.Verify(mock => mock.SketchReplace(It.IsAny<McpCad.Core.Models.CadElementAddress>(), It.IsAny<IReadOnlyDictionary<string, object?>?>()), Times.Once);
    }

    [Fact]
    public void onshape_orchestrate_part_SeparatesCaptureFailureAndStopsAfterModelingFailure()
    {
        var captureProvider = new Mock<ICadWebProvider>(MockBehavior.Strict);
        captureProvider.Setup(mock => mock.SketchReplace(It.IsAny<McpCad.Core.Models.CadElementAddress>(), It.IsAny<IReadOnlyDictionary<string, object?>?>()))
            .Returns(new Dictionary<string, object?> { ["success"] = true, ["featureId"] = "native-sketch" });
        captureProvider.Setup(mock => mock.CaptureView(It.IsAny<McpCad.Core.Models.CadElementAddress>(), "iso", 800, 600, "show"))
            .Returns(new Dictionary<string, object?> { ["success"] = false, ["error"] = "capture failed" });

        var capture = Invoke(captureProvider.Object, Plan(Sketch("first"), Checkpoint("checkpoint", "first")));

        capture.IsError.Should().BeTrue();
        capture.StructuredContent!.Value.GetProperty("status").GetString().Should().Be("completed_with_capture_failures");
        capture.StructuredContent!.Value.GetProperty("modeling").GetProperty("status").GetString().Should().Be("completed");

        var modelingProvider = new Mock<ICadWebProvider>(MockBehavior.Strict);
        modelingProvider.Setup(mock => mock.SketchReplace(It.IsAny<McpCad.Core.Models.CadElementAddress>(), It.IsAny<IReadOnlyDictionary<string, object?>?>()))
            .Returns(new Dictionary<string, object?> { ["success"] = false, ["error"] = "modeling failed" });

        var modeling = Invoke(modelingProvider.Object, Plan(Sketch("first"), Sketch("later")));

        modeling.IsError.Should().BeTrue();
        modeling.StructuredContent!.Value.GetProperty("status").GetString().Should().Be("partial_modeling_failure");
        modeling.StructuredContent!.Value.GetProperty("stages")[1].GetProperty("modelingStatus").GetString().Should().Be("not_attempted");
        modelingProvider.Verify(mock => mock.SketchReplace(It.IsAny<McpCad.Core.Models.CadElementAddress>(), It.IsAny<IReadOnlyDictionary<string, object?>?>()), Times.Once);
    }

    private static ModelContextProtocol.Protocol.CallToolResult Invoke(ICadWebProvider provider, string plan, string address = Address) =>
        (ModelContextProtocol.Protocol.CallToolResult)ToolMethod()!.Invoke(new OnshapeSkillTools(provider), [address, plan])!;

    private static MethodInfo? ToolMethod() => typeof(OnshapeSkillTools).GetMethod("onshape_orchestrate_part", BindingFlags.Public | BindingFlags.Instance);
    private static string Plan(params string[] stages) => "{\"schemaVersion\":1,\"stages\":[" + string.Join(',', stages) + "]}";
    private static string Sketch(string id) => "{\"id\":\"" + id + "\",\"kind\":\"sketch\",\"plane\":\"Top\",\"profile\":{\"type\":\"circle\",\"cx_cm\":0,\"cy_cm\":0,\"radius_cm\":1}}";
    private static string Checkpoint(string id, string afterStageId) => "{\"id\":\"" + id + "\",\"kind\":\"checkpoint\",\"afterStageId\":\"" + afterStageId + "\",\"view\":\"iso\"}";
}
