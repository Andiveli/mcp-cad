using System.Text.Json;
using System.Text.Json.Serialization;
using ModelContextProtocol.Protocol;

namespace McpCad.Tools.Onshape.Orchestration;

internal static class OnshapeOrchestrationResultBuilder
{
    public static CallToolResult Build(OnshapePartPlanExecutionState state)
    {
        var capturesStatus = CaptureSummaryStatus(state.Captures);
        var modelingStatus = state.ModelingError is null ? ModelingStatus.Completed : ModelingStatus.Failed;
        var status = OverallStatus(state.ModelingError, capturesStatus);
        var result = new OnshapeOrchestrationResult(
            1,
            status,
            new ModelingSummary(modelingStatus, state.LastSuccessfulFeatureId, state.FailedStageId, state.ModelingError),
            state.Stages,
            new CapturesSummary(capturesStatus, state.Captures));

        return new CallToolResult
        {
            IsError = status != OrchestrationStatus.Completed,
            Content = Images(state.Captures),
            StructuredContent = JsonSerializer.SerializeToElement(result, NewSerializerOptions()),
        };
    }

    public static CallToolResult PreflightError(OrchestrationError error)
    {
        var options = NewSerializerOptions();
        return new CallToolResult
        {
            IsError = true,
            Content = [new TextContentBlock { Text = JsonSerializer.Serialize(error, options) }],
            StructuredContent = JsonSerializer.SerializeToElement(new PreflightErrorEnvelope(error), options),
        };
    }

    private static JsonSerializerOptions NewSerializerOptions() => new()
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower) },
    };

    private static OrchestrationStatus OverallStatus(OrchestrationError? modelingError, CapturesStatus capturesStatus) =>
        modelingError is not null ? OrchestrationStatus.PartialModelingFailure
        : capturesStatus == CapturesStatus.PartialFailure ? OrchestrationStatus.CompletedWithCaptureFailures
        : OrchestrationStatus.Completed;

    private static CapturesStatus CaptureSummaryStatus(IEnumerable<CheckpointCaptureResult> captures)
    {
        var attempted = false;
        foreach (var capture in captures)
        {
            attempted = true;
            if (capture.Status == StageCaptureStatus.Failed) return CapturesStatus.PartialFailure;
        }

        return attempted ? CapturesStatus.Completed : CapturesStatus.None;
    }

    private static List<ContentBlock> Images(IEnumerable<CheckpointCaptureResult> captures) => captures
        .Where(capture => capture.Status == StageCaptureStatus.Completed)
        .Select(capture => ImageContentBlock.FromBytes(ImageBytes(capture), "image/png"))
        .Cast<ContentBlock>()
        .ToList();

    private static byte[] ImageBytes(CheckpointCaptureResult capture)
    {
        if (capture.Evidence is not IReadOnlyDictionary<string, object?> evidence ||
            !evidence.TryGetValue("image_base64", out var image) || image is not string base64)
        {
            throw new InvalidOperationException($"Completed checkpoint '{capture.StageId}' has no image_base64 evidence.");
        }

        return Convert.FromBase64String(base64);
    }

    private sealed record PreflightErrorEnvelope(
        [property: JsonPropertyName("error")] OrchestrationError Error);

}
