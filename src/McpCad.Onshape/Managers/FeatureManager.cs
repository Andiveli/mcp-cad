using System.Text.Json;
using McpCad.Core.Models;

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
        IReadOnlyList<Dictionary<string, object?>>? parameters = null)
    {
        var wvm = _driver.DefaultWvm;
        var body = JsonSerializer.Serialize(new
        {
            featureType,
            parameters = parameters ?? Array.Empty<Dictionary<string, object?>>(),
        });
        using var doc = _client.SendJsonAsync(
                HttpMethod.Post,
                OnshapeApiPaths.PartStudioFeatures(
                    partStudio.DocumentId,
                    wvm,
                    partStudio.WorkspaceOrVersionId,
                    partStudio.ElementId),
                body)
            .GetAwaiter().GetResult();
        _driver.UpdateMicroversionFromJson(doc.RootElement);
        return OnshapeJsonHelper.SuccessEnvelope(OnshapeJsonHelper.JsonElementToDictionary(doc.RootElement));
    }

    public Dictionary<string, object?> FeatureGetResult(CadElementAddress partStudio, string featureId)
    {
        var list = FeatureList(partStudio);
        list["requestedFeatureId"] = featureId;
        return list;
    }
}