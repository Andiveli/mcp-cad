using System.Collections.Immutable;
using McpCad.Core.Modeling;
using McpCad.Onshape.Modeling;

namespace McpCad.Tools.Onshape.Orchestration;

internal sealed class OnshapePartPlanContextBridge(IPartModelingContextFactory factory)
{
    public bool TryExecute(ValidatedOnshapePartPlan plan, out OnshapePartPlanExecutionState state)
    {
        state = null!;
        if (factory is not IOnshapePartModelingContextFactoryCompatibility
            {
                IsWorkspaceCompatible: true,
                SupportsNativeReceipts: true,
            }
            || plan.Stages is not [ValidatedSketchPlanStage { ContextDefinition: not null } sketch, ValidatedExtrudePlanStage extrude]
            || extrude.Operation != ExtrudeOperation.New
            || extrude.SketchId != sketch.Id)
        {
            return false;
        }

        IPartModelingContext context;
        try
        {
            context = factory.Bind(plan.Address);
        }
        catch (Exception exception)
        {
            state = FailedBeforeSketch(sketch, extrude, $"threw {exception.Message}");
            return true;
        }

        if (context is not IOnshapePartModelingContextReceipt receipt)
        {
            state = FailedBeforeSketch(sketch, extrude, "context factory bound a context without native receipt capability");
            return true;
        }

        var sketchResult = CreateSketch(context, sketch.ContextDefinition!);
        if (sketchResult.Status != OperationStatus.Succeeded || sketchResult.Reference is null)
        {
            state = FailedSketch(sketch, extrude, sketchResult, "PROVIDER_MODELING_FAILED");
            return true;
        }

        if (string.IsNullOrWhiteSpace(receipt.Receipt.SketchNativeFeatureId))
        {
            state = FailedSketch(sketch, extrude, MissingNativeId(sketchResult, "sketch"), "MISSING_NATIVE_FEATURE_ID");
            return true;
        }

        var sketchStage = Completed(0, sketch.Id, PlanStageKind.Sketch, receipt.Receipt.SketchNativeFeatureId);
        var extrusionResult = Extrude(context, sketchResult.Reference, extrude);
        if (extrusionResult.Status != OperationStatus.Succeeded || extrusionResult.Reference is null)
        {
            state = FailedExtrusion(sketchStage, extrude, extrusionResult, "PROVIDER_MODELING_FAILED");
            return true;
        }

        if (string.IsNullOrWhiteSpace(receipt.Receipt.ExtrusionNativeFeatureId))
        {
            state = FailedExtrusion(sketchStage, extrude, MissingNativeId(extrusionResult, "extrude"), "MISSING_NATIVE_FEATURE_ID");
            return true;
        }

        var extrusionStage = Completed(1, extrude.Id, PlanStageKind.Extrude, receipt.Receipt.ExtrusionNativeFeatureId);
        state = new OnshapePartPlanExecutionState([sketchStage, extrusionStage], [], extrusionStage.NativeFeatureId, null, null);
        return true;
    }

    private static OperationResult<SketchReference> CreateSketch(IPartModelingContext context, SketchDefinition definition)
    {
        try
        {
            return context.CreateSketch(definition, CancellationToken.None).AsTask().GetAwaiter().GetResult();
        }
        catch (Exception exception)
        {
            return new OperationResult<SketchReference>(OperationStatus.Uncertain, MutationDisposition.Unknown,
                new PartModelingError(PartModelingErrorCode.DegenerateGeometry, $"threw {exception.Message}"), null);
        }
    }

    private static OperationResult<FeatureReference> Extrude(
        IPartModelingContext context,
        SketchReference sketch,
        ValidatedExtrudePlanStage stage)
    {
        try
        {
            var direction = stage.Direction == ExtrudeDirection.Positive ? ExtrusionDirection.Positive : ExtrusionDirection.Negative;
            var definition = new ExtrusionDefinition(stage.DepthCm / 100, LengthUnit.Meter, direction);
            return context.ExtrudeNewBody(sketch, definition, CancellationToken.None).AsTask().GetAwaiter().GetResult();
        }
        catch (Exception exception)
        {
            return new OperationResult<FeatureReference>(OperationStatus.Uncertain, MutationDisposition.Unknown,
                new PartModelingError(PartModelingErrorCode.DegenerateGeometry, $"threw {exception.Message}"), null);
        }
    }

    private static OperationResult<TReference> MissingNativeId<TReference>(OperationResult<TReference> result, string operation) =>
        result.Status == OperationStatus.Succeeded
            ? result with
            {
                Status = OperationStatus.Failed,
                Mutation = MutationDisposition.Unknown,
                Error = new PartModelingError(PartModelingErrorCode.DegenerateGeometry, "response did not include a native feature ID."),
                Reference = default,
            }
            : result;

    private static OnshapePartPlanExecutionState FailedBeforeSketch(
        ValidatedSketchPlanStage sketch,
        ValidatedExtrudePlanStage extrude,
        string detail) =>
        FailedSketch(sketch, extrude, new OperationResult<SketchReference>(OperationStatus.Uncertain, MutationDisposition.Unknown,
            new PartModelingError(PartModelingErrorCode.DegenerateGeometry, detail), null), "PROVIDER_MODELING_FAILED");

    private static OnshapePartPlanExecutionState FailedSketch(
        ValidatedSketchPlanStage sketch,
        ValidatedExtrudePlanStage extrude,
        OperationResult<SketchReference> result,
        string code)
    {
        var error = Error(code, sketch.Id, "sketch", result.Error);
        return new OnshapePartPlanExecutionState(
            [Failed(0, sketch.Id, PlanStageKind.Sketch, error), Skipped(1, extrude.Id, PlanStageKind.Extrude)],
            [], null, sketch.Id, error);
    }

    private static OnshapePartPlanExecutionState FailedExtrusion(
        StageExecutionResult sketch,
        ValidatedExtrudePlanStage extrude,
        OperationResult<FeatureReference> result,
        string code)
    {
        var error = Error(code, extrude.Id, "extrude", result.Error);
        return new OnshapePartPlanExecutionState(
            [sketch, Failed(1, extrude.Id, PlanStageKind.Extrude, error)],
            [], sketch.NativeFeatureId, extrude.Id, error);
    }

    private static StageExecutionResult Completed(int sequence, string id, PlanStageKind kind, string? nativeFeatureId) =>
        new(sequence, id, kind, StageStatus.Completed, StageModelingStatus.Completed, StageCaptureStatus.NotApplicable, nativeFeatureId, null);

    private static StageExecutionResult Failed(int sequence, string id, PlanStageKind kind, OrchestrationError error) =>
        new(sequence, id, kind, StageStatus.Failed, StageModelingStatus.Failed, StageCaptureStatus.NotApplicable, null, error);

    private static StageExecutionResult Skipped(int sequence, string id, PlanStageKind kind) =>
        new(sequence, id, kind, StageStatus.Skipped, StageModelingStatus.NotAttempted, StageCaptureStatus.NotApplicable, null, null);

    private static OrchestrationError Error(string code, string stageId, string operation, PartModelingError? error) =>
        new(code, $"Stage '{stageId}': {operation} {error?.Detail ?? "did not complete"}");
}
