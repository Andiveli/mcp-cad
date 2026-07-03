using System.Text.Json;
using McpCad.Core.Models;

namespace McpCad.Onshape.Managers;

/// <summary>
/// Document lifecycle: open metadata, copy, move workspace, delete.
/// </summary>
public sealed class DocumentManager
{
    private readonly OnshapeClient _client;
    private readonly OnshapeDriver _driver;

    public DocumentManager(OnshapeClient client, OnshapeDriver driver)
    {
        _client = client;
        _driver = driver;
    }

    public Dictionary<string, object?> DocOpen(string pathOrDocumentId)
    {
        var documentId = NormalizeDocumentId(pathOrDocumentId);
        using var doc = _client.SendJsonAsync(HttpMethod.Get, OnshapeApiPaths.Document(documentId))
            .GetAwaiter().GetResult();
        _driver.UpdateMicroversionFromJson(doc.RootElement);
        var payload = OnshapeJsonHelper.JsonElementToDictionary(doc.RootElement);
        payload["documentId"] = documentId;
        return OnshapeJsonHelper.SuccessEnvelope(payload);
    }

    public Dictionary<string, object?> DocCopy(string documentId, string? name = null)
    {
        var body = name is null ? "{}" : JsonSerializer.Serialize(new { name });
        using var doc = _client.SendJsonAsync(HttpMethod.Post, OnshapeApiPaths.DocumentCopy(documentId), body)
            .GetAwaiter().GetResult();
        _driver.UpdateMicroversionFromJson(doc.RootElement);
        var payload = OnshapeJsonHelper.JsonElementToDictionary(doc.RootElement);
        return OnshapeJsonHelper.SuccessEnvelope(payload);
    }

    public Dictionary<string, object?> DocMove(string documentId, string targetFolderId)
    {
        var body = JsonSerializer.Serialize(new { folderId = targetFolderId });
        using var doc = _client.SendJsonAsync(
                HttpMethod.Post,
                $"/api/v6/documents/{Uri.EscapeDataString(documentId)}/move",
                body)
            .GetAwaiter().GetResult();
        var payload = OnshapeJsonHelper.JsonElementToDictionary(doc.RootElement);
        return OnshapeJsonHelper.SuccessEnvelope(payload);
    }

    public Dictionary<string, object?> DocDelete(string documentId)
    {
        _client.SendAsync(HttpMethod.Delete, OnshapeApiPaths.Document(documentId))
            .GetAwaiter().GetResult()
            .Dispose();
        return OnshapeJsonHelper.SuccessEnvelope(new Dictionary<string, object?> { ["documentId"] = documentId });
    }

    public Dictionary<string, object?> WsList(string? documentId = null)
    {
        if (string.IsNullOrWhiteSpace(documentId))
        {
            using var doc = _client.SendJsonAsync(HttpMethod.Get, OnshapeApiPaths.Documents())
                .GetAwaiter().GetResult();
            return OnshapeJsonHelper.SuccessEnvelope(OnshapeJsonHelper.JsonElementToDictionary(doc.RootElement));
        }

        using var ws = _client.SendJsonAsync(HttpMethod.Get, OnshapeApiPaths.Workspaces(documentId))
            .GetAwaiter().GetResult();
        return OnshapeJsonHelper.SuccessEnvelope(OnshapeJsonHelper.JsonElementToDictionary(ws.RootElement));
    }

    public Dictionary<string, object?> WsCurrent(string documentId)
    {
        using var doc = _client.SendJsonAsync(HttpMethod.Get, OnshapeApiPaths.Document(documentId))
            .GetAwaiter().GetResult();
        var root = doc.RootElement;
        string? workspaceId = null;
        if (root.TryGetProperty("defaultWorkspace", out var dw))
        {
            workspaceId = dw.GetProperty("id").GetString();
        }

        return OnshapeJsonHelper.SuccessEnvelope(new Dictionary<string, object?>
        {
            ["documentId"] = documentId,
            ["wvm"] = _driver.DefaultWvm,
            ["workspaceId"] = workspaceId,
        });
    }

    public Dictionary<string, object?> VersionCreate(string documentId, string? name = null)
    {
        var body = name is null ? "{}" : JsonSerializer.Serialize(new { name });
        using var doc = _client.SendJsonAsync(HttpMethod.Post, OnshapeApiPaths.Version(documentId), body)
            .GetAwaiter().GetResult();
        _driver.UpdateMicroversionFromJson(doc.RootElement);
        return OnshapeJsonHelper.SuccessEnvelope(OnshapeJsonHelper.JsonElementToDictionary(doc.RootElement));
    }

    private static string NormalizeDocumentId(string pathOrDocumentId)
    {
        if (pathOrDocumentId.StartsWith("d/", StringComparison.Ordinal))
        {
            return pathOrDocumentId[2..];
        }

        return pathOrDocumentId.Trim();
    }
}