using System.Collections.Immutable;
using System.Reflection;
using System.Text.Json.Serialization;
using McpCad.Core.Models;
using McpCad.Tools.Onshape;

namespace McpCad.Tests.Tools;

public class OnshapePartPlanModelTests
{
    private const string Namespace = "McpCad.Tools.Onshape.Orchestration.";
    private static readonly Assembly ModelsAssembly = typeof(OnshapeSkillTools).Assembly;

    [Fact]
    public void ParsedPlan_UsesInternalImmutableClosedStageAndProfileContracts()
    {
        var plan = TypeNamed("OnshapePartPlan");
        var stages = Constructor(plan).GetParameters();
        stages.Select(parameter => parameter.Name).Should().Equal("SchemaVersion", "Stages");
        stages[1].ParameterType.Should().Be(typeof(ImmutableArray<>).MakeGenericType(TypeNamed("OnshapePlanStage")));

        TypeNamed("OnshapePlanStage").IsAbstract.Should().BeTrue();
        TypeNamed("ClosedProfile").IsAbstract.Should().BeTrue();
        TypeNamed("SketchPlanStage").BaseType.Should().Be(TypeNamed("OnshapePlanStage"));
        TypeNamed("ExtrudePlanStage").BaseType.Should().Be(TypeNamed("OnshapePlanStage"));
        TypeNamed("CheckpointPlanStage").BaseType.Should().Be(TypeNamed("OnshapePlanStage"));

        new[] { "RectangleProfile", "CircleProfile", "PolygonProfile", "MixedProfile" }
            .Select(TypeNamed)
            .Should().OnlyContain(type => type.BaseType == TypeNamed("ClosedProfile"));
    }

    [Fact]
    public void ContractEnums_AreClosedToSupportedDiscriminants()
    {
        EnumNames("CanonicalSketchPlane").Should().Equal("Top", "Front", "Right");
        EnumNames("ExtrudeOperation").Should().Equal("New", "Add", "Remove");
        EnumNames("ExtrudeDirection").Should().Equal("Positive", "Negative");
        EnumNames("CheckpointView").Should().Equal("Iso", "Top");
        EnumNames("PlanStageKind").Should().Equal("Sketch", "Extrude", "Checkpoint");
    }

    [Fact]
    public void ValidatedPlan_OwnsExistingAddressAndImmutableValidatedStages()
    {
        var parameters = Constructor(TypeNamed("ValidatedOnshapePartPlan")).GetParameters();

        parameters.Select(parameter => parameter.Name).Should().Equal("Address", "Stages");
        parameters[0].ParameterType.Should().Be(typeof(CadElementAddress));
        parameters[1].ParameterType.Should().Be(typeof(ImmutableArray<>).MakeGenericType(TypeNamed("ValidatedOnshapePlanStage")));
    }

    [Fact]
    public void ExecutionResults_ExposeExactStatusesNullableIdentifiersAndCaptureFields()
    {
        PropertiesOf("StageExecutionResult").Should().Equal(
            "Sequence", "Id", "Kind", "StageStatus", "ModelingStatus", "CaptureStatus", "NativeFeatureId", "Error");
        PropertiesOf("ModelingSummary").Should().Equal(
            "Status", "LastSuccessfulFeatureId", "FailedStageId", "Error");
        PropertiesOf("CheckpointCaptureResult").Should().Equal("StageId", "View", "Status", "Evidence", "Error");
        PropertiesOf("OnshapeOrchestrationResult").Should().Equal("SchemaVersion", "Status", "Modeling", "Stages", "Captures");
        PropertiesOf("OrchestrationError").Should().Equal("Code", "Message");

        Constructor(TypeNamed("StageExecutionResult")).GetParameters()
            .Single(parameter => parameter.Name == "NativeFeatureId").ParameterType.Should().Be(typeof(string));
        Constructor(TypeNamed("StageExecutionResult")).GetParameters()
            .Single(parameter => parameter.Name == "Error").ParameterType.Should().Be(TypeNamed("OrchestrationError"));
    }

    [Fact]
    public void ModelContracts_PreserveEmptyCollectionsNullResultsAllStatusesAndMixedProfileBoundary()
    {
        var planStages = EmptyArray(TypeNamed("OnshapePlanStage"));
        var plan = Constructor(TypeNamed("OnshapePartPlan")).Invoke([1, planStages]);
        plan.GetType().GetProperty("Stages")!.GetValue(plan).Should().Be(planStages);

        var stage = Constructor(TypeNamed("StageExecutionResult")).Invoke([
            0, "sketch-1", Enum.Parse(TypeNamed("PlanStageKind"), "Sketch"),
            Enum.Parse(TypeNamed("StageStatus"), "Completed"),
            Enum.Parse(TypeNamed("StageModelingStatus"), "Completed"),
            Enum.Parse(TypeNamed("StageCaptureStatus"), "NotApplicable"), null, null,
        ]);
        stage.GetType().GetProperty("NativeFeatureId")!.GetValue(stage).Should().BeNull();
        stage.GetType().GetProperty("Error")!.GetValue(stage).Should().BeNull();

        EnumNames("OrchestrationStatus").Should().Equal("Completed", "CompletedWithCaptureFailures", "PartialModelingFailure");
        EnumNames("ModelingStatus").Should().Equal("Completed", "Failed");
        EnumNames("StageStatus").Should().Equal("Completed", "Failed", "Skipped");
        EnumNames("StageModelingStatus").Should().Equal("Completed", "Failed", "NotApplicable", "NotAttempted");
        EnumNames("StageCaptureStatus").Should().Equal("Completed", "Failed", "NotApplicable", "NotAttempted");
        EnumNames("CapturesStatus").Should().Equal("None", "Completed", "PartialFailure");

        var mixed = Constructor(TypeNamed("MixedProfile")).Invoke([
            Activator.CreateInstance(typeof(McpCad.Onshape.Builders.MixedProfilePointCm), 0d, 0d)!,
            EmptyArray(typeof(McpCad.Onshape.Builders.MixedProfileSegmentInput)), 
        ]);
        mixed.GetType().BaseType.Should().Be(TypeNamed("ClosedProfile"));
        PropertiesOf("StageExecutionResult").Select(name =>
            TypeNamed("StageExecutionResult").GetProperty(name)!.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name)
            .Should().Equal("sequence", "id", "kind", "stageStatus", "modelingStatus", "captureStatus", "nativeFeatureId", "error");
        PropertiesOf("OrchestrationError").Select(name =>
            TypeNamed("OrchestrationError").GetProperty(name)!.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name)
            .Should().Equal("code", "message");
    }

    private static Type TypeNamed(string name) =>
        ModelsAssembly.GetType(Namespace + name, throwOnError: true)!;

    private static ConstructorInfo Constructor(Type type) =>
        type.GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .Where(constructor => constructor.GetParameters().Length > 1)
            .Should().ContainSingle().Subject;

    private static string[] EnumNames(string name) => Enum.GetNames(TypeNamed(name));

    private static object EmptyArray(Type elementType) =>
        typeof(ImmutableArray).GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Single(method =>
            {
                var parameters = method.GetParameters();
                return method.Name == "CreateRange" && method.GetGenericArguments().Length == 1
                    && parameters.Length == 1 && parameters[0].ParameterType.IsGenericType
                    && parameters[0].ParameterType.GetGenericTypeDefinition() == typeof(IEnumerable<>);
            })
            .MakeGenericMethod(elementType)
            .Invoke(null, [Array.CreateInstance(elementType, 0)])!;

    private static string[] PropertiesOf(string name) =>
        TypeNamed(name).GetProperties(BindingFlags.Instance | BindingFlags.Public).Select(property => property.Name).ToArray();
}
