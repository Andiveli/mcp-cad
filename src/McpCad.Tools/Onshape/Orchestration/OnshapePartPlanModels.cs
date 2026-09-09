using System.Collections.Immutable;
using System.Text.Json.Serialization;
using McpCad.Core.Models;
using McpCad.Onshape.Builders;

namespace McpCad.Tools.Onshape.Orchestration;

internal sealed record OnshapePartPlan(int SchemaVersion, ImmutableArray<OnshapePlanStage> Stages);

internal abstract record OnshapePlanStage(string Id);

internal sealed record SketchPlanStage(string Id, CanonicalSketchPlane Plane, ClosedProfile Profile)
    : OnshapePlanStage(Id);

internal sealed record ExtrudePlanStage(
    string Id,
    string SketchId,
    ExtrudeOperation Operation,
    double DepthCm,
    ExtrudeDirection Direction) : OnshapePlanStage(Id);

internal sealed record CheckpointPlanStage(string Id, string AfterStageId, CheckpointView View)
    : OnshapePlanStage(Id);

internal enum PlanStageKind { Sketch, Extrude, Checkpoint }
internal enum CanonicalSketchPlane { Top, Front, Right }
internal enum ExtrudeOperation { New, Add, Remove }
internal enum ExtrudeDirection { Positive, Negative }
internal enum CheckpointView { Iso, Top }

internal abstract record ClosedProfile;
internal sealed record RectangleProfile(double X1Cm, double Y1Cm, double X2Cm, double Y2Cm) : ClosedProfile;
internal sealed record CircleProfile(double CxCm, double CyCm, double RadiusCm) : ClosedProfile;
internal sealed record PolygonProfile(ImmutableArray<SketchPoint> PointsCm) : ClosedProfile;
internal sealed record MixedProfile(MixedProfilePointCm Start, ImmutableArray<MixedProfileSegmentInput> Segments) : ClosedProfile;

internal sealed record ValidatedOnshapePartPlan(
    CadElementAddress Address,
    ImmutableArray<ValidatedOnshapePlanStage> Stages);

internal abstract record ValidatedOnshapePlanStage(string Id);
internal sealed record ValidatedSketchPlanStage(string Id, string SketchRequestJson) : ValidatedOnshapePlanStage(Id);
internal sealed record ValidatedExtrudePlanStage(
    string Id,
    string SketchId,
    ExtrudeOperation Operation,
    double DepthCm,
    ExtrudeDirection Direction) : ValidatedOnshapePlanStage(Id);
internal sealed record ValidatedCheckpointPlanStage(string Id, CheckpointView View) : ValidatedOnshapePlanStage(Id);
internal sealed record StageSymbol(int DeclarationIndex, PlanStageKind Kind);

internal enum OrchestrationStatus { Completed, CompletedWithCaptureFailures, PartialModelingFailure }
internal enum ModelingStatus { Completed, Failed }
internal enum StageStatus { Completed, Failed, Skipped }
internal enum StageModelingStatus { Completed, Failed, NotApplicable, NotAttempted }
internal enum StageCaptureStatus { Completed, Failed, NotApplicable, NotAttempted }
internal enum CapturesStatus { None, Completed, PartialFailure }

internal sealed record OrchestrationError(
    [property: JsonPropertyName("code")] string Code,
    [property: JsonPropertyName("message")] string Message);

internal sealed record ModelingSummary(
    [property: JsonPropertyName("status")] ModelingStatus Status,
    [property: JsonPropertyName("lastSuccessfulFeatureId")] string? LastSuccessfulFeatureId,
    [property: JsonPropertyName("failedStageId")] string? FailedStageId,
    [property: JsonPropertyName("error")] OrchestrationError? Error);

internal sealed record StageExecutionResult(
    [property: JsonPropertyName("sequence")] int Sequence,
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("kind")] PlanStageKind Kind,
    [property: JsonPropertyName("stageStatus")] StageStatus StageStatus,
    [property: JsonPropertyName("modelingStatus")] StageModelingStatus ModelingStatus,
    [property: JsonPropertyName("captureStatus")] StageCaptureStatus CaptureStatus,
    [property: JsonPropertyName("nativeFeatureId")] string? NativeFeatureId,
    [property: JsonPropertyName("error")] OrchestrationError? Error);

internal sealed record CheckpointCaptureResult(
    [property: JsonPropertyName("stageId")] string StageId,
    [property: JsonPropertyName("view")] CheckpointView View,
    [property: JsonPropertyName("status")] StageCaptureStatus Status,
    [property: JsonPropertyName("evidence")] object? Evidence,
    [property: JsonPropertyName("error")] OrchestrationError? Error);

internal sealed record CapturesSummary(
    [property: JsonPropertyName("status")] CapturesStatus Status,
    [property: JsonPropertyName("results")] ImmutableArray<CheckpointCaptureResult> Results);

internal sealed record OnshapeOrchestrationResult(
    [property: JsonPropertyName("schemaVersion")] int SchemaVersion,
    [property: JsonPropertyName("status")] OrchestrationStatus Status,
    [property: JsonPropertyName("modeling")] ModelingSummary Modeling,
    [property: JsonPropertyName("stages")] ImmutableArray<StageExecutionResult> Stages,
    [property: JsonPropertyName("captures")] CapturesSummary Captures);

internal sealed record OnshapePartPlanExecutionState(
    ImmutableArray<StageExecutionResult> Stages,
    ImmutableArray<CheckpointCaptureResult> Captures,
    string? LastSuccessfulFeatureId,
    string? FailedStageId,
    OrchestrationError? ModelingError);
