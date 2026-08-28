using System.Text.Json;
using McpCad.Core.Exceptions;
using McpCad.Core.Models;

namespace McpCad.Onshape.Managers;

/// <summary>
/// Part Studio element CRUD and sketch replace (REST).
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
                OnshapeApiPaths.Elements(did, wvm, wvmId),
                query: new Dictionary<string, string?> { ["elementId"] = address.ElementId })
            .GetAwaiter().GetResult();

        var element = FindElement(doc.RootElement, address.ElementId);
        if (element is null)
        {
            throw new CadProviderOperationException(
                $"Onshape element '{address.ElementId}' was not found in {wvm} '{wvmId}'.");
        }

        _driver.UpdateMicroversionFromJson(element.Value);
        return OnshapeJsonHelper.SuccessEnvelope(OnshapeJsonHelper.JsonElementToDictionary(element.Value));
    }

    public Dictionary<string, object?> ElementCreate(CadElementAddress address, string name, string elementType)
    {
        if (!string.Equals(elementType, "PARTSTUDIO", StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "Onshape Part Studio creation only supports elementType 'PARTSTUDIO'.",
                nameof(elementType));
        }

        var (did, _, workspaceId) = ResolveWvm(address);
        var body = JsonSerializer.Serialize(new { name });
        using var doc = _client.SendJsonAsync(
                HttpMethod.Post,
                OnshapeApiPaths.PartStudioCreate(did, workspaceId),
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

    public Dictionary<string, object?> SketchReplace(CadElementAddress address, string? sketchBodyJson)
    {
        var (did, wvm, wvmId) = ResolveWvm(address);
        var path = OnshapeApiPaths.PartStudioFeatures(did, wvm, wvmId, address.ElementId);
        var body = string.IsNullOrWhiteSpace(sketchBodyJson)
            ? """{"entities":[],"constraints":[]}"""
            : sketchBodyJson;
        using var doc = _client.SendJsonAsync(HttpMethod.Post, path, body).GetAwaiter().GetResult();
        ThrowIfFeatureFailed(doc.RootElement);
        _driver.UpdateMicroversionFromJson(doc.RootElement);
        return OnshapeJsonHelper.SuccessEnvelope(OnshapeJsonHelper.JsonElementToDictionary(doc.RootElement));
    }

    private static void ThrowIfFeatureFailed(JsonElement root)
    {
        if (!root.TryGetProperty("featureState", out var state)
            || !state.TryGetProperty("featureStatus", out var statusElement))
        {
            return;
        }

        var status = statusElement.GetString();
        if (status is not ("ERROR" or "UNKNOWN"))
        {
            return;
        }

        var detail = state.TryGetProperty("errorString", out var error) ? error.GetString() : null;
        throw new CadProviderOperationException(
            $"Onshape sketch creation returned feature status {status}: {detail ?? "No error detail provided."}");
    }

    private static JsonElement? FindElement(JsonElement root, string elementId)
    {
        JsonElement elements;
        if (root.ValueKind == JsonValueKind.Array)
        {
            elements = root;
        }
        else if (root.ValueKind != JsonValueKind.Object
                 || !root.TryGetProperty("elements", out elements)
                 || elements.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        foreach (var element in elements.EnumerateArray())
        {
            if (element.ValueKind == JsonValueKind.Object
                && element.TryGetProperty("id", out var id)
                && string.Equals(id.GetString(), elementId, StringComparison.Ordinal))
            {
                return element;
            }
        }

        return null;
    }

    private (string Did, string Wvm, string WvmId) ResolveWvm(CadElementAddress address) =>
        (address.DocumentId, _driver.DefaultWvm, address.WorkspaceOrVersionId);
}
