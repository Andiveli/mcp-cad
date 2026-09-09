using System.Collections.Immutable;
using System.Text.Json;
using McpCad.Core;
using McpCad.Onshape;
using McpCad.Onshape.Builders;

namespace McpCad.Tools.Onshape.Orchestration;

internal sealed class OnshapePartPlanExecutor(ICadWebProvider provider)
{
    public OnshapePartPlanExecutionState Execute(ValidatedOnshapePartPlan plan)
    {
        var stages = ImmutableArray.CreateBuilder<StageExecutionResult>(plan.Stages.Length);
        var captures = ImmutableArray.CreateBuilder<CheckpointCaptureResult>();
        var nativeIds = new Dictionary<string, string>(StringComparer.Ordinal);
        string? lastSuccessfulFeatureId = null;
        string? failedStageId = null;
        OrchestrationError? modelingError = null;
        var modelingStopped = false;

        for (var sequence = 0; sequence < plan.Stages.Length; sequence++)
        {
            var stage = plan.Stages[sequence];
            if (modelingStopped)
            {
                stages.Add(Skipped(sequence, stage));
                continue;
            }

            switch (stage)
            {
                case ValidatedSketchPlanStage sketch:
                {
                    var result = Write(sketch.Id, "sketch", () => provider.SketchReplace(plan.Address, Request(sketch.SketchRequestJson)));
                    stages.Add(ModelingResult(sequence, sketch.Id, PlanStageKind.Sketch, result));
                    if (result.Succeeded)
                    {
                        nativeIds.Add(sketch.Id, result.NativeFeatureId!);
                        lastSuccessfulFeatureId = result.NativeFeatureId;
                    }
                    else
                    {
                        modelingStopped = true;
                        failedStageId = sketch.Id;
                        modelingError = result.Error;
                    }
                    break;
                }
                case ValidatedExtrudePlanStage extrude:
                {
                    if (!nativeIds.TryGetValue(extrude.SketchId, out var sketchId))
                    {
                        var error = new OrchestrationError("ORCHESTRATOR_INTERNAL_ERROR", $"Stage '{extrude.Id}': resolved sketch native ID is unavailable.");
                        stages.Add(new StageExecutionResult(sequence, extrude.Id, PlanStageKind.Extrude, StageStatus.Failed, StageModelingStatus.Failed, StageCaptureStatus.NotApplicable, null, error));
                        modelingStopped = true;
                        failedStageId = extrude.Id;
                        modelingError = error;
                        break;
                    }

                    var result = Write(extrude.Id, "extrude", () => Feature(plan, extrude, sketchId));
                    stages.Add(ModelingResult(sequence, extrude.Id, PlanStageKind.Extrude, result));
                    if (result.Succeeded)
                    {
                        nativeIds.Add(extrude.Id, result.NativeFeatureId!);
                        lastSuccessfulFeatureId = result.NativeFeatureId;
                    }
                    else
                    {
                        modelingStopped = true;
                        failedStageId = extrude.Id;
                        modelingError = result.Error;
                    }
                    break;
                }
                case ValidatedCheckpointPlanStage checkpoint:
                    Capture(plan, checkpoint, sequence, stages, captures);
                    break;
                default:
                    throw new InvalidOperationException($"Unsupported validated stage '{stage.Id}'.");
            }
        }

        return new OnshapePartPlanExecutionState(stages.ToImmutable(), captures.ToImmutable(), lastSuccessfulFeatureId, failedStageId, modelingError);
    }

    private Dictionary<string, object?> Feature(ValidatedOnshapePartPlan plan, ValidatedExtrudePlanStage stage, string sketchId)
    {
        using var document = OnshapeFeatureTemplates.ExtrudeFromSketch(
            sketchId,
            OnshapeUnitConversion.CmToMeters(stage.DepthCm),
            stage.Direction == ExtrudeDirection.Positive ? "positive" : "negative",
            stage.Operation switch
            {
                ExtrudeOperation.New => "new",
                ExtrudeOperation.Add => "add",
                ExtrudeOperation.Remove => "remove",
                _ => throw new InvalidOperationException("Unsupported validated extrude operation."),
            });
        return provider.FeatureAdd(plan.Address, "extrude", null, document.RootElement.GetRawText());
    }

    private static IReadOnlyDictionary<string, object?> Request(string json) =>
        JsonSerializer.Deserialize<Dictionary<string, object?>>(json)
        ?? throw new InvalidOperationException("Validated sketch request was empty.");

    private static OnshapeFeatureWriteResult Write(string stageId, string operation, Func<Dictionary<string, object?>> call)
    {
        try
        {
            return OnshapeFeatureWriteResult.Normalize(call(), stageId, operation);
        }
        catch (Exception exception)
        {
            return new OnshapeFeatureWriteResult(null, new OrchestrationError(
                "PROVIDER_MODELING_FAILED", $"Stage '{stageId}': {operation} threw {exception.Message}"));
        }
    }

    private void Capture(
        ValidatedOnshapePartPlan plan,
        ValidatedCheckpointPlanStage checkpoint,
        int sequence,
        ImmutableArray<StageExecutionResult>.Builder stages,
        ImmutableArray<CheckpointCaptureResult>.Builder captures)
    {
        try
        {
            var response = provider.CaptureView(plan.Address, checkpoint.View == CheckpointView.Iso ? "iso" : "top", 800, 600, "show");
            var error = CaptureError(response, checkpoint.Id);
            var succeeded = error is null;
            stages.Add(new StageExecutionResult(sequence, checkpoint.Id, PlanStageKind.Checkpoint,
                succeeded ? StageStatus.Completed : StageStatus.Failed, StageModelingStatus.NotApplicable,
                succeeded ? StageCaptureStatus.Completed : StageCaptureStatus.Failed, null, null));
            captures.Add(new CheckpointCaptureResult(checkpoint.Id, checkpoint.View,
                succeeded ? StageCaptureStatus.Completed : StageCaptureStatus.Failed, succeeded ? response : null, error));
        }
        catch (Exception exception)
        {
            var error = new OrchestrationError("PROVIDER_CAPTURE_FAILED", $"Stage '{checkpoint.Id}': capture threw {exception.Message}");
            stages.Add(new StageExecutionResult(sequence, checkpoint.Id, PlanStageKind.Checkpoint, StageStatus.Failed,
                StageModelingStatus.NotApplicable, StageCaptureStatus.Failed, null, null));
            captures.Add(new CheckpointCaptureResult(checkpoint.Id, checkpoint.View, StageCaptureStatus.Failed, null, error));
        }
    }

    private static OrchestrationError? CaptureError(IReadOnlyDictionary<string, object?> response, string stageId)
    {
        if (!response.TryGetValue("success", out var success) || success is not true)
            return CaptureFailure(stageId, response.TryGetValue("error", out var error) && error is string text ? text : "provider returned an unsuccessful result");
        if (!response.TryGetValue("image_base64", out var image) || image is not string base64 || string.IsNullOrWhiteSpace(base64))
            return CaptureFailure(stageId, "response did not include image_base64");
        try { Convert.FromBase64String(base64); }
        catch (FormatException) { return new OrchestrationError("INVALID_CAPTURE_IMAGE", $"Stage '{stageId}': image_base64 is invalid."); }
        return null;
    }

    private static OrchestrationError CaptureFailure(string stageId, string detail) =>
        new("PROVIDER_CAPTURE_FAILED", $"Stage '{stageId}': {detail}");

    private static StageExecutionResult ModelingResult(int sequence, string id, PlanStageKind kind, OnshapeFeatureWriteResult result) =>
        new(sequence, id, kind, result.Succeeded ? StageStatus.Completed : StageStatus.Failed,
            result.Succeeded ? StageModelingStatus.Completed : StageModelingStatus.Failed,
            StageCaptureStatus.NotApplicable, result.NativeFeatureId, result.Error);

    private static StageExecutionResult Skipped(int sequence, ValidatedOnshapePlanStage stage) => stage switch
    {
        ValidatedCheckpointPlanStage => new(sequence, stage.Id, PlanStageKind.Checkpoint, StageStatus.Skipped,
            StageModelingStatus.NotApplicable, StageCaptureStatus.NotAttempted, null, null),
        ValidatedSketchPlanStage => new(sequence, stage.Id, PlanStageKind.Sketch, StageStatus.Skipped,
            StageModelingStatus.NotAttempted, StageCaptureStatus.NotApplicable, null, null),
        ValidatedExtrudePlanStage => new(sequence, stage.Id, PlanStageKind.Extrude, StageStatus.Skipped,
            StageModelingStatus.NotAttempted, StageCaptureStatus.NotApplicable, null, null),
        _ => throw new InvalidOperationException("Unsupported validated stage."),
    };
}
