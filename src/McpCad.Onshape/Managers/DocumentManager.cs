namespace McpCad.Onshape.Managers;

/// <summary>Read-only document and workspace discovery.</summary>
public sealed class DocumentManager
{
    private readonly OnshapeClient _client;

    public DocumentManager(OnshapeClient client)
    {
        _client = client;
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
}
