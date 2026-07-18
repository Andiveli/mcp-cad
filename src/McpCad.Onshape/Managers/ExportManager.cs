using System.Globalization;
using System.Text.Json;
using McpCad.Core.Models;

namespace McpCad.Onshape.Managers;

/// <summary>
/// STEP export via translation polling and synchronous STL export.
/// </summary>
public sealed class ExportManager
{
    private readonly OnshapeClient _client;
    private readonly OnshapeDriver _driver;

    public ExportManager(OnshapeClient client, OnshapeDriver driver)
    {
        _client = client;
        _driver = driver;
    }

    public Dictionary<string, object?> ExportStep(CadElementAddress address, Dictionary<string, object?>? options = null)
    {
        var wvm = _driver.DefaultWvm;
        var body = options is null || options.Count == 0
            ? "{}"
            : JsonSerializer.Serialize(options);
        using var doc = _client.SendJsonAsync(
                HttpMethod.Post,
                OnshapeApiPaths.PartStudioExportStep(
                    address.DocumentId,
                    wvm,
                    address.WorkspaceOrVersionId,
                    address.ElementId),
                body)
            .GetAwaiter().GetResult();
        var root = doc.RootElement;
        var translationId = root.TryGetProperty("id", out var idEl)
            ? idEl.GetString()
            : root.TryGetProperty("translationId", out var tidEl)
                ? tidEl.GetString()
                : null;
        if (string.IsNullOrEmpty(translationId))
        {
            return OnshapeJsonHelper.SuccessEnvelope(OnshapeJsonHelper.JsonElementToDictionary(root));
        }

        var polled = _client.PollTranslationAsync(translationId).GetAwaiter().GetResult();
        return OnshapeJsonHelper.SuccessEnvelope(polled);
    }

    public Dictionary<string, object?> ExportStl(CadElementAddress address, Dictionary<string, object?>? options = null)
    {
        var wvm = _driver.DefaultWvm;
        var query = options?.ToDictionary(
            pair => pair.Key,
            pair => pair.Value switch
            {
                null => null,
                bool value => value ? "true" : "false",
                _ => Convert.ToString(pair.Value, CultureInfo.InvariantCulture),
            });
        using var initialResponse = _client.SendAsync(
                HttpMethod.Get,
                OnshapeApiPaths.PartStudioExportStl(
                    address.DocumentId,
                    wvm,
                    address.WorkspaceOrVersionId,
                    address.ElementId),
                query: query,
                acceptRedirect: true)
            .GetAwaiter().GetResult();
        using var response = initialResponse.Headers.Location is { } location
            ? FollowStlRedirect(initialResponse, location)
            : null;
        var exportResponse = response ?? initialResponse;

        return OnshapeJsonHelper.SuccessEnvelope(new Dictionary<string, object?>
        {
            ["format"] = "STL",
            ["documentId"] = address.DocumentId,
            ["workspaceOrVersionId"] = address.WorkspaceOrVersionId,
            ["elementId"] = address.ElementId,
            ["downloadUrl"] = exportResponse.RequestMessage?.RequestUri?.ToString(),
            ["contentType"] = exportResponse.Content.Headers.ContentType?.MediaType,
            ["contentLength"] = exportResponse.Content.Headers.ContentLength,
        });
    }

    private HttpResponseMessage FollowStlRedirect(HttpResponseMessage response, Uri location)
    {
        var requestUri = response.RequestMessage?.RequestUri
            ?? throw new InvalidOperationException("STL export response has no request URI.");
        var redirectUri = new Uri(requestUri, location);
        if ((redirectUri.Scheme != Uri.UriSchemeHttp && redirectUri.Scheme != Uri.UriSchemeHttps)
            || !string.Equals(requestUri.Scheme, redirectUri.Scheme, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(requestUri.Host, redirectUri.Host, StringComparison.OrdinalIgnoreCase)
            || requestUri.Port != redirectUri.Port)
        {
            throw new InvalidOperationException("STL export redirect must remain on the authenticated Onshape origin.");
        }

        return _client.SendAsync(HttpMethod.Get, redirectUri.AbsoluteUri).GetAwaiter().GetResult();
    }
}
