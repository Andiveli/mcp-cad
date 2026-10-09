namespace McpCad.Onshape.Managers;

/// <summary>Read-only document and workspace discovery.</summary>
public sealed class DocumentManager
{
    private readonly OnshapeClient _client;
    private readonly OnshapeDriver _driver;

    public DocumentManager(OnshapeClient client) : this(client, new OnshapeDriver())
    {
    }

    public DocumentManager(OnshapeClient client, OnshapeDriver driver)
    {
        _client = client;
        _driver = driver;
    }

    public Dictionary<string, object?> WsList(string? documentId = null)
    {
        var path = string.IsNullOrWhiteSpace(documentId)
            ? OnshapeApiPaths.Documents()
            : OnshapeApiPaths.Workspaces(documentId);
        using var response = _client.SendJsonAsync(HttpMethod.Get, path).GetAwaiter().GetResult();
        return OnshapeJsonHelper.SuccessEnvelope(
            OnshapeJsonHelper.JsonElementToDictionary(response.RootElement));
    }

    public Dictionary<string, object?> WsCurrent(string documentId)
    {
        using var response = _client.SendJsonAsync(HttpMethod.Get, OnshapeApiPaths.Document(documentId))
            .GetAwaiter().GetResult();
        var root = response.RootElement;
        string? workspaceId = null;
        if (root.TryGetProperty("defaultWorkspace", out var workspace)
            && workspace.ValueKind == System.Text.Json.JsonValueKind.Object
            && workspace.TryGetProperty("id", out var id))
        {
            workspaceId = id.GetString();
        }

        return OnshapeJsonHelper.SuccessEnvelope(new Dictionary<string, object?>
        {
            ["documentId"] = documentId,
            ["wvm"] = _driver.DefaultWvm,
            ["workspaceId"] = workspaceId,
        });
    }
}
