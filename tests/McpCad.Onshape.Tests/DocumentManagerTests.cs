using System.Net;
using McpCad.Core.Exceptions;
using McpCad.Onshape.Managers;
using Xunit;

namespace McpCad.Onshape.Tests;

public sealed class DocumentManagerTests
{
    [Fact]
    public void WsList_WithoutDocumentId_ListsDocumentsWithSuccessEnvelope()
    {
        Uri? requestUri = null;
        using var client = new OnshapeClient(new StubHandler(request =>
        {
            requestUri = request.RequestUri;
            return JsonResponse("{\"items\":[{\"id\":\"doc-1\"}],\"count\":1,\"success\":false}");
        }), "key", "secret", "https://cad.example.test");
        var manager = new DocumentManager(client);

        var result = manager.WsList();

        Assert.Equal("https://cad.example.test/api/v6/documents", requestUri?.AbsoluteUri);
        Assert.Equal(true, result["success"]);
        Assert.Equal(1L, result["count"]);
        var items = Assert.IsType<List<object?>>(result["items"]);
        Assert.Equal("doc-1", Assert.IsType<Dictionary<string, object?>>(Assert.Single(items))["id"]);
    }

    [Fact]
    public void WsList_WithDocumentId_EscapesIdAndListsWorkspaces()
    {
        Uri? requestUri = null;
        using var client = new OnshapeClient(new StubHandler(request =>
        {
            requestUri = request.RequestUri;
            return JsonResponse("[{\"id\":\"ws-1\",\"active\":true}]");
        }), "key", "secret", "https://cad.example.test");
        var manager = new DocumentManager(client);

        var result = manager.WsList("doc/one");

        Assert.Equal("https://cad.example.test/api/v6/documents/d/doc%2Fone/workspaces", requestUri?.AbsoluteUri);
        Assert.Equal(true, result["success"]);
        var workspaces = Assert.IsType<List<object?>>(result["value"]);
        var first = Assert.IsType<Dictionary<string, object?>>(Assert.Single(workspaces));
        Assert.Equal("ws-1", first["id"]);
        Assert.Equal(true, first["active"]);
    }

    [Fact]
    public void WsList_Unauthorized_DoesNotReturnSuccessEnvelope()
    {
        var requests = 0;
        using var client = new OnshapeClient(new StubHandler(_ =>
        {
            requests++;
            return new HttpResponseMessage(HttpStatusCode.Unauthorized) { ReasonPhrase = "Unauthorized" };
        }), "key", "secret", "https://cad.example.test");
        var manager = new DocumentManager(client);

        var error = Assert.Throws<OnshapeAuthException>(() => manager.WsList("doc-1"));

        Assert.Equal(401, error.StatusCode);
        Assert.Equal(1, requests);
    }

    [Fact]
    public void WsCurrent_UsesConfiguredWvmAndDefaultWorkspace()
    {
        Uri? requestUri = null;
        using var client = new OnshapeClient(new StubHandler(request =>
        {
            requestUri = request.RequestUri;
            return JsonResponse("{\"defaultWorkspace\":{\"id\":\"ws-1\"}}");
        }), "key", "secret", "https://cad.example.test");
        var driver = new OnshapeDriver { DefaultWvm = "m" };
        var manager = new DocumentManager(client, driver);

        var result = manager.WsCurrent("doc/one");

        Assert.Equal("https://cad.example.test/api/v6/documents/doc%2Fone", requestUri?.AbsoluteUri);
        Assert.Equal(true, result["success"]);
        Assert.Equal("doc/one", result["documentId"]);
        Assert.Equal("m", result["wvm"]);
        Assert.Equal("ws-1", result["workspaceId"]);
    }

    [Fact]
    public void WsCurrent_WithoutDefaultWorkspace_ReturnsNullWorkspaceId()
    {
        using var client = new OnshapeClient(new StubHandler(_ => JsonResponse("{\"name\":\"part\"}")),
            "key", "secret", "https://cad.example.test");
        var manager = new DocumentManager(client);

        var result = manager.WsCurrent("doc-1");

        Assert.Equal(true, result["success"]);
        Assert.Equal("doc-1", result["documentId"]);
        Assert.Equal("w", result["wvm"]);
        Assert.Null(result["workspaceId"]);
    }

    [Fact]
    public void WsCurrent_Unauthorized_PropagatesTypedError()
    {
        var requests = 0;
        using var client = new OnshapeClient(new StubHandler(_ =>
        {
            requests++;
            return new HttpResponseMessage(HttpStatusCode.Unauthorized) { ReasonPhrase = "Unauthorized" };
        }), "key", "secret", "https://cad.example.test");
        var manager = new DocumentManager(client);

        var error = Assert.Throws<OnshapeAuthException>(() => manager.WsCurrent("doc-1"));

        Assert.Equal(401, error.StatusCode);
        Assert.Equal(1, requests);
    }

    private static HttpResponseMessage JsonResponse(string json) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json"),
    };

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(respond(request));
    }
}
