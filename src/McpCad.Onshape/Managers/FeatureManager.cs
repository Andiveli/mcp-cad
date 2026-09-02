using System.Text.Json;
using McpCad.Core.Exceptions;
using McpCad.Core.Models;
using McpCad.Onshape.Builders;

namespace McpCad.Onshape.Managers;

/// <summary>
/// Feature spec/result wrappers (mirrors Inventor FeatureManager shape at the seam).
/// </summary>
public sealed class FeatureManager
{
    private readonly OnshapeClient _client;
    private readonly OnshapeDriver _driver;

    public FeatureManager(OnshapeClient client, OnshapeDriver driver)
    {
        _client = client;
        _driver = driver;
    }

    public Dictionary<string, object?> FeatureList(CadElementAddress partStudio)
    {
        var wvm = _driver.DefaultWvm;
        using var doc = _client.SendJsonAsync(
                HttpMethod.Get,
                OnshapeApiPaths.PartStudioFeatures(
                    partStudio.DocumentId,
                    wvm,
                    partStudio.WorkspaceOrVersionId,
                    partStudio.ElementId))
            .GetAwaiter().GetResult();
        return OnshapeJsonHelper.SuccessEnvelope(OnshapeJsonHelper.JsonElementToDictionary(doc.RootElement));
    }

    public Dictionary<string, object?> FeatureAdd(
        CadElementAddress partStudio,
        string featureType,
        IReadOnlyList<Dictionary<string, object?>>? parameters = null,
        string? featurePostBodyJson = null)
    {
        var wvm = _driver.DefaultWvm;
        var body = featurePostBodyJson
                   ?? JsonSerializer.Serialize(OnshapeFeatureTemplates.BuildMinimalFeatureEnvelope(featureType, parameters));
        using var doc = _client.SendJsonAsync(
                HttpMethod.Post,
                OnshapeApiPaths.PartStudioFeatures(
                    partStudio.DocumentId,
                    wvm,
                    partStudio.WorkspaceOrVersionId,
                    partStudio.ElementId),
                body)
            .GetAwaiter().GetResult();
        if (doc.RootElement.TryGetProperty("featureState", out var featureState)
            && featureState.ValueKind == JsonValueKind.Object
            && featureState.TryGetProperty("featureStatus", out var featureStatus)
            && featureStatus.ValueKind == JsonValueKind.String
            && featureStatus.GetString() is { } status
            && (status.Equals("ERROR", StringComparison.OrdinalIgnoreCase)
                || status.Equals("UNKNOWN", StringComparison.OrdinalIgnoreCase)))
        {
            var context = featureState.TryGetProperty("errorString", out var error)
                          && error.ValueKind == JsonValueKind.String
                ? $": {error.GetString()}"
                : string.Empty;
            throw new CadProviderOperationException($"Onshape feature creation failed with status {status}{context}");
        }

        _driver.UpdateMicroversionFromJson(doc.RootElement);
        return OnshapeJsonHelper.SuccessEnvelope(OnshapeJsonHelper.NormalizeFeatureWriteResponse(doc.RootElement));
    }

    public Dictionary<string, object?> FeatureGetResult(CadElementAddress partStudio, string featureId)
    {
        var list = FeatureList(partStudio);
        list["requestedFeatureId"] = featureId;
        return list;
    }
}
