using System.Text.Json;
using McpCad.Core.Models;

namespace McpCad.Onshape.Managers;

/// <summary>
/// Part Studio elements and sketch/feature stubs (v1 delegates to REST endpoints).
/// </summary>
public sealed class PartStudioManager
{
    private readonly OnshapeClient _client;
    private readonly OnshapeDriver _driver;

    public PartStudioManager(OnshapeClient client, OnshapeDriver driver)
    {
        _client = client;
        _driver = driver;
    }

    public Dictionary<string, object?> ElementList(string documentId, string wvm, string wvmId)
    {
        using var doc = _client.SendJsonAsync(HttpMethod.Get, OnshapeApiPaths.Elements(documentId, wvm, wvmId))
            .GetAwaiter().GetResult();
        return OnshapeJsonHelper.SuccessEnvelope(OnshapeJsonHelper.JsonElementToDictionary(doc.RootElement));
    }

    public Dictionary<string, object?> ElementGet(CadElementAddress address)
    {
        var (did, wvm, wvmId) = ResolveWvm(address);
        using var doc = _client.SendJsonAsync(
                HttpMethod.Get,
                OnshapeApiPaths.Element(did, wvm, wvmId, address.ElementId))
            .GetAwaiter().GetResult();
        _driver.UpdateMicroversionFromJson(doc.RootElement);
        return OnshapeJsonHelper.SuccessEnvelope(OnshapeJsonHelper.JsonElementToDictionary(doc.RootElement));
    }

    public Dictionary<string, object?> ElementCreate(CadElementAddress address, string name, string elementType)
    {
        var (did, wvm, wvmId) = ResolveWvm(address);
        var body = JsonSerializer.Serialize(new { name, elementType });
        using var doc = _client.SendJsonAsync(
                HttpMethod.Post,
                OnshapeApiPaths.Elements(did, wvm, wvmId),
                body)
            .GetAwaiter().GetResult();
        _driver.UpdateMicroversionFromJson(doc.RootElement);
        return OnshapeJsonHelper.SuccessEnvelope(OnshapeJsonHelper.JsonElementToDictionary(doc.RootElement));
    }

    public Dictionary<string, object?> ElementRename(CadElementAddress address, string name)
    {
        var (did, wvm, wvmId) = ResolveWvm(address);
        var body = JsonSerializer.Serialize(new { name });
        using var doc = _client.SendJsonAsync(
                HttpMethod.Post,
                OnshapeApiPaths.Element(did, wvm, wvmId, address.ElementId),
                body)
            .GetAwaiter().GetResult();
        _driver.UpdateMicroversionFromJson(doc.RootElement);
        return OnshapeJsonHelper.SuccessEnvelope(OnshapeJsonHelper.JsonElementToDictionary(doc.RootElement));
    }

    public Dictionary<string, object?> MicroversionGet(CadElementAddress address)
    {
        var result = ElementGet(address);
        if (_driver.LastMicroversion is { } mv)
        {
            result["microversion"] = mv;
        }

        return result;
    }

    /// <summary>Stub for future sketch payload replacement.</summary>
    public Dictionary<string, object?> SketchReplaceStub(CadElementAddress address) =>
        OnshapeJsonHelper.SuccessEnvelope(new Dictionary<string, object?>
        {
            ["stub"] = "sketch_replace",
            ["elementId"] = address.ElementId,
        });

    /// <summary>Stub for future feature POST.</summary>
    public Dictionary<string, object?> FeatureAddStub(CadElementAddress address, string featureType) =>
        OnshapeJsonHelper.SuccessEnvelope(new Dictionary<string, object?>
        {
            ["stub"] = "feature_add",
            ["featureType"] = featureType,
            ["elementId"] = address.ElementId,
        });

    private (string Did, string Wvm, string WvmId) ResolveWvm(CadElementAddress address) =>
        (address.DocumentId, _driver.DefaultWvm, address.WorkspaceOrVersionId);
}