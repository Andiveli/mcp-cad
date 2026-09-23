using System.Reflection;
using McpCad.Core;
using McpCad.Core.Modeling;
using McpCad.Onshape.Modeling;
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
    public void onshape_orchestrate_part_AcceptsInjectedFactoryWithoutChangingLegacyRouting()
    {
        var provider = new Mock<ICadWebProvider>(MockBehavior.Strict);
        provider.Setup(mock => mock.SketchReplace(It.IsAny<McpCad.Core.Models.CadElementAddress>(), It.IsAny<IReadOnlyDictionary<string, object?>?>()))
            .Returns(new Dictionary<string, object?> { ["success"] = true, ["featureId"] = "native-sketch" });
        var factory = new Mock<IPartModelingContextFactory>(MockBehavior.Strict);
        var tools = new OnshapeSkillTools(provider.Object, factory.Object);

        var result = Invoke(tools, Plan(Sketch("first")));

        result.StructuredContent!.Value.GetProperty("stages")[0].GetProperty("nativeFeatureId").GetString().Should().Be("native-sketch");
        factory.Verify(mock => mock.Bind(It.IsAny<McpCad.Core.Models.CadElementAddress>()), Times.Never);
    }

    [Theory]
    [InlineData("rectangle")]
    [InlineData("circle")]
    public void onshape_orchestrate_part_RoutesEligibleWorkspaceProfilesThroughOneContextAndProjectsNativeIds(string profile)
    {
        var provider = new Mock<ICadWebProvider>(MockBehavior.Strict);
        var context = new WorkspaceContext();
        var factory = new WorkspaceFactory("w", context);
        var tools = new OnshapeSkillTools(provider.Object, factory);

        var result = Invoke(tools, Plan(profile == "rectangle" ? Rectangle("s") : Sketch("s"), Extrude("e", "s")));

        context.Calls.Should().Equal("sketch", "extrude");
        factory.BindCount.Should().Be(1);
        provider.Invocations.Should().BeEmpty();
        result.StructuredContent!.Value.GetProperty("stages")[0].GetProperty("nativeFeatureId").GetString().Should().Be("context-sketch");
        result.StructuredContent!.Value.GetProperty("stages")[1].GetProperty("nativeFeatureId").GetString().Should().Be("context-extrude");
    }

    [Theory]
    [InlineData(OperationStatus.Failed)]
    [InlineData(OperationStatus.Uncertain)]
    public void onshape_orchestrate_part_StopsWithoutExtrusionAfterFailedOrUncertainContextSketch(OperationStatus sketchStatus)
    {
        var provider = new Mock<ICadWebProvider>(MockBehavior.Strict);
        var context = new WorkspaceContext(sketchStatus);
        var tools = new OnshapeSkillTools(provider.Object, new WorkspaceFactory("w", context));

        var result = Invoke(tools, Plan(Sketch("s"), Extrude("e", "s")));

        context.Calls.Should().Equal("sketch");
        provider.Invocations.Should().BeEmpty();
        result.IsError.Should().BeTrue();
        result.StructuredContent!.Value.GetProperty("stages")[0].GetProperty("stageStatus").GetString().Should().Be("failed");
        result.StructuredContent!.Value.GetProperty("stages")[1].GetProperty("modelingStatus").GetString().Should().Be("not_attempted");
    }

    [Theory]
    [MemberData(nameof(ContextSketchFailures))]
    public void onshape_orchestrate_part_StopsAfterEveryFailedUncertainThrownOrReceiptlessContextSketch(
        OperationStatus sketchStatus,
        bool throws,
        bool blankReceipt,
        string errorCode)
    {
        var provider = new Mock<ICadWebProvider>(MockBehavior.Strict);
        var context = new WorkspaceContext(sketchStatus: sketchStatus, throwSketch: throws, blankSketchReceipt: blankReceipt);
        var factory = new WorkspaceFactory("w", context);
        var tools = new OnshapeSkillTools(provider.Object, factory);

        var result = Invoke(tools, Plan(Sketch("s"), Extrude("e", "s")));

        factory.BindCount.Should().Be(1);
        context.Calls.Should().Equal("sketch");
        provider.Invocations.Should().BeEmpty();
        result.IsError.Should().BeTrue();
        result.StructuredContent!.Value.GetProperty("modeling").GetProperty("lastSuccessfulFeatureId").ValueKind.Should().Be(System.Text.Json.JsonValueKind.Null);
        result.StructuredContent.Value.GetProperty("modeling").GetProperty("error").GetProperty("code").GetString().Should().Be(errorCode);
        result.StructuredContent.Value.GetProperty("stages")[0].GetProperty("stageStatus").GetString().Should().Be("failed");
        result.StructuredContent.Value.GetProperty("stages")[1].GetProperty("stageStatus").GetString().Should().Be("skipped");
    }

    [Theory]
    [MemberData(nameof(ContextExtrusionFailures))]
    public void onshape_orchestrate_part_StopsAfterEveryFailedUncertainThrownOrReceiptlessContextExtrusion(
        OperationStatus extrusionStatus,
        bool throws,
        bool blankReceipt,
        string errorCode)
    {
        var provider = new Mock<ICadWebProvider>(MockBehavior.Strict);
        var context = new WorkspaceContext(extrusionStatus: extrusionStatus, throwExtrusion: throws, blankExtrusionReceipt: blankReceipt);
        var factory = new WorkspaceFactory("w", context);
        var tools = new OnshapeSkillTools(provider.Object, factory);

        var result = Invoke(tools, Plan(Sketch("s"), Extrude("e", "s")));

        factory.BindCount.Should().Be(1);
        context.Calls.Should().Equal("sketch", "extrude");
        provider.Invocations.Should().BeEmpty();
        result.IsError.Should().BeTrue();
        result.StructuredContent!.Value.GetProperty("modeling").GetProperty("lastSuccessfulFeatureId").GetString().Should().Be("context-sketch");
        result.StructuredContent.Value.GetProperty("modeling").GetProperty("error").GetProperty("code").GetString().Should().Be(errorCode);
        result.StructuredContent.Value.GetProperty("stages")[0].GetProperty("stageStatus").GetString().Should().Be("completed");
        result.StructuredContent.Value.GetProperty("stages")[1].GetProperty("stageStatus").GetString().Should().Be("failed");
    }

    [Fact]
    public void onshape_orchestrate_part_ProjectsBindThrowAsProviderModelingFailureWithoutLegacyWrites()
    {
        var provider = new Mock<ICadWebProvider>(MockBehavior.Strict);
        var factory = new WorkspaceFactory("w", new WorkspaceContext(), throwBind: true);
        var tools = new OnshapeSkillTools(provider.Object, factory);

        var result = Invoke(tools, Plan(Sketch("s"), Extrude("e", "s")));

        factory.BindCount.Should().Be(1);
        provider.Invocations.Should().BeEmpty();
        result.IsError.Should().BeTrue();
        result.StructuredContent!.Value.GetProperty("modeling").GetProperty("error").GetProperty("code").GetString().Should().Be("PROVIDER_MODELING_FAILED");
        result.StructuredContent.Value.GetProperty("stages")[0].GetProperty("stageStatus").GetString().Should().Be("failed");
        result.StructuredContent.Value.GetProperty("stages")[1].GetProperty("stageStatus").GetString().Should().Be("skipped");
    }

    [Fact]
    public void onshape_orchestrate_part_UsesLegacyBeforeBindWhenFactoryDoesNotAdvertiseNativeReceipts()
    {
        var provider = LegacyProvider();
        var factory = new WorkspaceFactory("w", new WorkspaceContext(), supportsNativeReceipts: false);
        var tools = new OnshapeSkillTools(provider.Object, factory);

        _ = Invoke(tools, Plan(Sketch("s"), Extrude("e", "s")));

        factory.BindCount.Should().Be(0);
        provider.Verify(mock => mock.SketchReplace(It.IsAny<McpCad.Core.Models.CadElementAddress>(), It.IsAny<IReadOnlyDictionary<string, object?>?>()), Times.Once);
        provider.Verify(mock => mock.FeatureAdd(It.IsAny<McpCad.Core.Models.CadElementAddress>(), "extrude", null, It.IsAny<string?>()), Times.Once);
    }

    [Fact]
    public void onshape_orchestrate_part_FailsClosedWhenReceiptCapableFactoryBindsAContextWithoutReceipt()
    {
        var provider = new Mock<ICadWebProvider>(MockBehavior.Strict);
        var context = new NonReceiptWorkspaceContext();
        var factory = new WorkspaceFactory("w", context);
        var tools = new OnshapeSkillTools(provider.Object, factory);

        var result = Invoke(tools, Plan(Sketch("s"), Extrude("e", "s")));

        factory.BindCount.Should().Be(1);
        context.Calls.Should().BeEmpty();
        provider.Invocations.Should().BeEmpty();
        result.IsError.Should().BeTrue();
        result.StructuredContent!.Value.GetProperty("modeling").GetProperty("error").GetProperty("code").GetString().Should().Be("PROVIDER_MODELING_FAILED");
        result.StructuredContent.Value.GetProperty("stages")[0].GetProperty("stageStatus").GetString().Should().Be("failed");
        result.StructuredContent.Value.GetProperty("stages")[1].GetProperty("stageStatus").GetString().Should().Be("skipped");
    }

    [Theory]
    [MemberData(nameof(LegacyPlans))]
    public void onshape_orchestrate_part_UsesLegacyExecutorForIneligiblePlansAndFactories(string plan, string wvm, bool supportsNativeReceipts)
    {
        var provider = LegacyProvider();
        var factory = new WorkspaceFactory(wvm, new WorkspaceContext(), supportsNativeReceipts);
        var tools = new OnshapeSkillTools(provider.Object, factory);

        var result = Invoke(tools, plan);

        result.StructuredContent.Should().NotBeNull();
        factory.BindCount.Should().Be(0);
        provider.Invocations.Should().NotBeEmpty();
    }

    [Fact]
    public void onshape_orchestrate_part_UsesLegacyExecutorForUnknownFactory()
    {
        var provider = LegacyProvider();
        var factory = new Mock<IPartModelingContextFactory>(MockBehavior.Strict);
        var tools = new OnshapeSkillTools(provider.Object, factory.Object);

        _ = Invoke(tools, Plan(Sketch("s"), Extrude("e", "s")));

        factory.Verify(mock => mock.Bind(It.IsAny<McpCad.Core.Models.CadElementAddress>()), Times.Never);
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
        Invoke(new OnshapeSkillTools(provider), plan, address);

    private static ModelContextProtocol.Protocol.CallToolResult Invoke(OnshapeSkillTools tools, string plan, string address = Address) =>
        (ModelContextProtocol.Protocol.CallToolResult)ToolMethod()!.Invoke(tools, [address, plan])!;

    public static IEnumerable<object[]> ContextSketchFailures()
    {
        yield return [OperationStatus.Failed, false, false, "PROVIDER_MODELING_FAILED"];
        yield return [OperationStatus.Uncertain, false, false, "PROVIDER_MODELING_FAILED"];
        yield return [OperationStatus.Succeeded, true, false, "PROVIDER_MODELING_FAILED"];
        yield return [OperationStatus.Succeeded, false, true, "MISSING_NATIVE_FEATURE_ID"];
    }

    public static IEnumerable<object[]> ContextExtrusionFailures()
    {
        yield return [OperationStatus.Failed, false, false, "PROVIDER_MODELING_FAILED"];
        yield return [OperationStatus.Uncertain, false, false, "PROVIDER_MODELING_FAILED"];
        yield return [OperationStatus.Succeeded, true, false, "PROVIDER_MODELING_FAILED"];
        yield return [OperationStatus.Succeeded, false, true, "MISSING_NATIVE_FEATURE_ID"];
    }

    public static IEnumerable<object[]> LegacyPlans()
    {
        yield return [Plan(Polygon("s"), Extrude("e", "s")), "w", true];
        yield return [Plan(Mixed("s"), Extrude("e", "s")), "w", true];
        yield return [Plan(Sketch("s"), Extrude("e", "s", "add")), "w", true];
        yield return [Plan(Sketch("s"), Extrude("e", "s", "remove")), "w", true];
        yield return [Plan(Sketch("s"), Checkpoint("c", "s"), Extrude("e", "s")), "w", true];
        yield return [Plan(Sketch("s"), Extrude("e", "s"), Sketch("later")), "w", true];
        yield return [Plan(Sketch("s"), Extrude("e", "s")), "v", true];
        yield return [Plan(Sketch("s"), Extrude("e", "s")), "m", true];
        yield return [Plan(Sketch("s"), Extrude("e", "s")), "w", false];
    }

    private static Mock<ICadWebProvider> LegacyProvider()
    {
        var provider = new Mock<ICadWebProvider>(MockBehavior.Strict);
        provider.Setup(mock => mock.SketchReplace(It.IsAny<McpCad.Core.Models.CadElementAddress>(), It.IsAny<IReadOnlyDictionary<string, object?>?>()))
            .Returns(new Dictionary<string, object?> { ["success"] = true, ["featureId"] = "legacy-sketch" });
        provider.Setup(mock => mock.FeatureAdd(It.IsAny<McpCad.Core.Models.CadElementAddress>(), "extrude", null, It.IsAny<string?>()))
            .Returns(new Dictionary<string, object?> { ["success"] = true, ["featureId"] = "legacy-extrude" });
        provider.Setup(mock => mock.CaptureView(It.IsAny<McpCad.Core.Models.CadElementAddress>(), "iso", 800, 600, "show"))
            .Returns(new Dictionary<string, object?> { ["success"] = true, ["image_base64"] = Convert.ToBase64String([1]) });
        return provider;
    }

    private static MethodInfo? ToolMethod() => typeof(OnshapeSkillTools).GetMethod("onshape_orchestrate_part", BindingFlags.Public | BindingFlags.Instance);
    private static string Plan(params string[] stages) => "{\"schemaVersion\":1,\"stages\":[" + string.Join(',', stages) + "]}";
    private static string Sketch(string id) => "{\"id\":\"" + id + "\",\"kind\":\"sketch\",\"plane\":\"Top\",\"profile\":{\"type\":\"circle\",\"cx_cm\":0,\"cy_cm\":0,\"radius_cm\":1}}";
    private static string Rectangle(string id) => "{\"id\":\"" + id + "\",\"kind\":\"sketch\",\"plane\":\"Top\",\"profile\":{\"type\":\"rectangle\",\"x1_cm\":0,\"y1_cm\":0,\"x2_cm\":1,\"y2_cm\":1}}";
    private static string Polygon(string id) => "{\"id\":\"" + id + "\",\"kind\":\"sketch\",\"plane\":\"Top\",\"profile\":{\"type\":\"polygon\",\"points\":[{\"x_cm\":0,\"y_cm\":0},{\"x_cm\":1,\"y_cm\":0},{\"x_cm\":0,\"y_cm\":1},{\"x_cm\":0,\"y_cm\":0}]}}";
    private static string Mixed(string id) => "{\"id\":\"" + id + "\",\"kind\":\"sketch\",\"plane\":\"Top\",\"profile\":{\"type\":\"mixed\",\"start\":{\"x_cm\":0,\"y_cm\":0},\"segments\":[{\"type\":\"line\",\"end\":{\"x_cm\":10,\"y_cm\":0}},{\"type\":\"arc\",\"through\":{\"x_cm\":5,\"y_cm\":5},\"end\":{\"x_cm\":0,\"y_cm\":0}}]}}";
    private static string Extrude(string id, string sketchId, string operation = "new") => "{\"id\":\"" + id + "\",\"kind\":\"extrude\",\"sketchId\":\"" + sketchId + "\",\"operation\":\"" + operation + "\",\"depth_cm\":2,\"direction\":\"positive\"}";
    private static string Checkpoint(string id, string afterStageId) => "{\"id\":\"" + id + "\",\"kind\":\"checkpoint\",\"afterStageId\":\"" + afterStageId + "\",\"view\":\"iso\"}";

    private sealed class WorkspaceFactory(
        string effectiveWvm,
        IPartModelingContext context,
        bool supportsNativeReceipts = true,
        bool throwBind = false)
        : IPartModelingContextFactory, IOnshapePartModelingContextFactoryCompatibility
    {
        public int BindCount { get; private set; }
        public string EffectiveWvm => effectiveWvm;
        public bool IsWorkspaceCompatible => OnshapePartModelingContextFactory.IsWorkspaceCompatibleWvm(effectiveWvm);
        public bool SupportsNativeReceipts => supportsNativeReceipts;
        public IPartModelingContext Bind(McpCad.Core.Models.CadElementAddress target)
        {
            BindCount++;
            if (throwBind) throw new InvalidOperationException("context bind stopped");
            return context;
        }
    }

    private sealed class WorkspaceContext(
        OperationStatus sketchStatus = OperationStatus.Succeeded,
        OperationStatus extrusionStatus = OperationStatus.Succeeded,
        bool throwSketch = false,
        bool throwExtrusion = false,
        bool blankSketchReceipt = false,
        bool blankExtrusionReceipt = false) : IPartModelingContext, IOnshapePartModelingContextReceipt
    {
        public List<string> Calls { get; } = [];
        public PartModelingCapabilities Capabilities { get; } = new(OptionalCapabilityStatus.Unsupported, OptionalCapabilityStatus.Unsupported);
        public PartModelingResult Result { get; private set; } = new(
            new OperationResult<SketchReference>(OperationStatus.NotAttempted, MutationDisposition.None, null, null),
            new OperationResult<FeatureReference>(OperationStatus.NotAttempted, MutationDisposition.None, null, null));
        public OnshapePartModelingReceipt Receipt { get; private set; } = new(null, null);

        public ValueTask<OperationResult<SketchReference>> CreateSketch(SketchDefinition definition, CancellationToken cancellationToken)
        {
            Calls.Add("sketch");
            if (throwSketch) throw new InvalidOperationException("context sketch threw");
            var succeeded = sketchStatus == OperationStatus.Succeeded;
            var result = new OperationResult<SketchReference>(sketchStatus, succeeded ? MutationDisposition.Present : MutationDisposition.Unknown,
                succeeded ? null : new PartModelingError(PartModelingErrorCode.DegenerateGeometry, "context sketch stopped"), succeeded ? SketchReference.Create() : null);
            Result = Result with { Sketch = result };
            if (succeeded && !blankSketchReceipt) Receipt = Receipt with { SketchNativeFeatureId = "context-sketch" };
            return ValueTask.FromResult(result);
        }

        public ValueTask<OperationResult<FeatureReference>> ExtrudeNewBody(SketchReference sketch, ExtrusionDefinition definition, CancellationToken cancellationToken)
        {
            Calls.Add("extrude");
            if (throwExtrusion) throw new InvalidOperationException("context extrusion threw");
            var succeeded = extrusionStatus == OperationStatus.Succeeded;
            var result = new OperationResult<FeatureReference>(extrusionStatus, succeeded ? MutationDisposition.Present : MutationDisposition.Unknown,
                succeeded ? null : new PartModelingError(PartModelingErrorCode.DegenerateGeometry, "context extrusion stopped"), succeeded ? FeatureReference.Create() : null);
            Result = Result with { Extrusion = result };
            if (succeeded && !blankExtrusionReceipt) Receipt = Receipt with { ExtrusionNativeFeatureId = "context-extrude" };
            return ValueTask.FromResult(result);
        }
    }

    private sealed class NonReceiptWorkspaceContext : IPartModelingContext
    {
        public List<string> Calls { get; } = [];
        public PartModelingCapabilities Capabilities { get; } = new(OptionalCapabilityStatus.Unsupported, OptionalCapabilityStatus.Unsupported);
        public PartModelingResult Result { get; } = new(
            new OperationResult<SketchReference>(OperationStatus.NotAttempted, MutationDisposition.None, null, null),
            new OperationResult<FeatureReference>(OperationStatus.NotAttempted, MutationDisposition.None, null, null));

        public ValueTask<OperationResult<SketchReference>> CreateSketch(SketchDefinition definition, CancellationToken cancellationToken)
        {
            Calls.Add("sketch");
            throw new InvalidOperationException("must not create a sketch");
        }

        public ValueTask<OperationResult<FeatureReference>> ExtrudeNewBody(SketchReference sketch, ExtrusionDefinition definition, CancellationToken cancellationToken)
        {
            Calls.Add("extrude");
            throw new InvalidOperationException("must not extrude");
        }
    }
}
