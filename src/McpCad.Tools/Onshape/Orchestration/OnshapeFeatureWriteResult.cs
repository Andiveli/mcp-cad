namespace McpCad.Tools.Onshape.Orchestration;

internal sealed record OnshapeFeatureWriteResult(string? NativeFeatureId, OrchestrationError? Error)
{
    public bool Succeeded => Error is null;

    public static OnshapeFeatureWriteResult Normalize(
        IReadOnlyDictionary<string, object?> response,
        string stageId,
        string operation)
    {
        if (response.TryGetValue("success", out var success) && success is true)
        {
            var featureId = StringValue(response, "featureId");
            featureId = string.IsNullOrWhiteSpace(featureId) ? NestedFeatureId(response) : featureId;
            return !string.IsNullOrWhiteSpace(featureId)
                ? new OnshapeFeatureWriteResult(featureId, null)
                : Failed("MISSING_NATIVE_FEATURE_ID", stageId, $"{operation} response did not include a native feature ID.");
        }

        var detail = StringValue(response, "error");
        return Failed("PROVIDER_MODELING_FAILED", stageId,
            string.IsNullOrWhiteSpace(detail) ? $"{operation} returned an unsuccessful provider result." : detail);
    }

    private static string? NestedFeatureId(IReadOnlyDictionary<string, object?> response) =>
        response.TryGetValue("feature", out var feature) && feature is IReadOnlyDictionary<string, object?> nested
            ? StringValue(nested, "featureId")
            : null;

    private static string? StringValue(IReadOnlyDictionary<string, object?> values, string key) =>
        values.TryGetValue(key, out var value) && value is string text ? text : null;

    private static OnshapeFeatureWriteResult Failed(string code, string stageId, string detail) =>
        new(null, new OrchestrationError(code, $"Stage '{stageId}': {detail}"));
}
