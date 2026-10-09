using System.Net;
using System.Text;
using McpCad.Core.Exceptions;
using McpCad.Onshape;
using Xunit;

namespace McpCad.Onshape.Tests;

public sealed class OnshapeClientTests
{
    [Fact]
    public async Task Get_UsesBasicAuthAndKeepsReadQuery()
    {
        HttpRequestMessage? captured = null;
        using var client = new OnshapeClient(new StubHandler(request =>
        {
            captured = request;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        }), "myAccess", "mySecret", "https://cad.example.test/");

        using var response = await client.SendAsync(HttpMethod.Get, "api/v6/documents", query: new Dictionary<string, string?>
        {
            ["z"] = "a b",
            ["a"] = "1",
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("https://cad.example.test/api/v6/documents?a=1&z=a%20b", captured?.RequestUri?.AbsoluteUri);
        Assert.Equal("Basic", captured?.Headers.Authorization?.Scheme);
        Assert.Equal(Convert.ToBase64String(Encoding.UTF8.GetBytes("myAccess:mySecret")), captured?.Headers.Authorization?.Parameter);
        Assert.False(captured?.Headers.Contains("On-Nonce"));
        Assert.False(captured?.Headers.Contains("On-Date"));
    }

    [Fact]
    public async Task Post_AddsSkewGuardAndSendsJsonBody()
    {
        string? uri = null;
        string? body = null;
        string? contentType = null;
        using var client = new OnshapeClient(new StubHandler(async request =>
        {
            uri = request.RequestUri?.ToString();
            body = request.Content is null ? null : await request.Content.ReadAsStringAsync();
            contentType = request.Content?.Headers.ContentType?.MediaType;
            return new HttpResponseMessage(HttpStatusCode.Created);
        }), "key", "secret", "https://cad.example.test");

        using var response = await client.SendAsync(HttpMethod.Post, "/api/v6/documents", "{\"name\":\"part\"}");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal("https://cad.example.test/api/v6/documents?rejectMicroversionSkew=true", uri);
        Assert.Equal("{\"name\":\"part\"}", body);
        Assert.Equal("application/json", contentType);
    }

    [Fact]
    public async Task Unauthorized_ThrowsTypedAuthenticationError()
    {
        using var client = new OnshapeClient(new StubHandler(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.Unauthorized)
        {
            ReasonPhrase = "Unauthorized",
        })), "key", "secret", "https://cad.example.test");

        var error = await Assert.ThrowsAsync<OnshapeAuthException>(() => client.SendAsync(HttpMethod.Get, "/api/v6/documents"));

        Assert.Equal(401, error.StatusCode);
        Assert.Equal("Unauthorized", error.Reason);
    }

    [Fact]
    public async Task Post_WithPathQueryAndFragment_PutsSkewGuardInEffectiveQuery()
    {
        Uri? requestUri = null;
        using var client = new OnshapeClient(new StubHandler(request =>
        {
            requestUri = request.RequestUri;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        }), "key", "secret", "https://cad.example.test");

        using var response = await client.SendAsync(HttpMethod.Post, "/api/v6/documents?limit=2#section");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("?limit=2&rejectMicroversionSkew=true", requestUri?.Query);
    }

    [Fact]
    public async Task Post_WithConflictingSkewGuard_RejectsBeforeRequest()
    {
        var requests = 0;
        using var client = new OnshapeClient(new StubHandler(_ =>
        {
            requests++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        }), "key", "secret", "https://cad.example.test");

        await Assert.ThrowsAsync<ArgumentException>(() =>
            client.SendAsync(HttpMethod.Post, "/api/v6/documents?rejectMicroversionSkew=false"));

        Assert.Equal(0, requests);
    }

    [Theory]
    [InlineData("https://other.example.test/api/v6/documents")]
    [InlineData("http://cad.example.test/api/v6/documents")]
    [InlineData("//other.example.test/api/v6/documents")]
    public async Task CrossOriginUrl_RejectsBeforeSendingCredentials(string path)
    {
        var requests = 0;
        using var client = new OnshapeClient(new StubHandler(_ =>
        {
            requests++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        }), "key", "secret", "https://cad.example.test");

        await Assert.ThrowsAsync<ArgumentException>(() => client.SendAsync(HttpMethod.Get, path));

        Assert.Equal(0, requests);
    }

    [Fact]
    public async Task Redirect_IsReturnedWithoutForwardingWrite()
    {
        var requests = 0;
        using var client = new OnshapeClient(new StubHandler(_ =>
        {
            requests++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.TemporaryRedirect)
            {
                Headers = { Location = new Uri("https://other.example.test/upload") },
            });
        }), "key", "secret", "https://cad.example.test");

        using var response = await client.SendAsync(HttpMethod.Post, "/api/v6/documents", "{}", acceptRedirect: true);

        Assert.Equal(HttpStatusCode.TemporaryRedirect, response.StatusCode);
        Assert.Equal(1, requests);
    }

    [Fact]
    public void InsecureBaseUrl_IsRejectedBeforeTransportCreation()
    {
        Assert.Throws<ArgumentException>(() => new OnshapeClient("key", "secret", "http://cad.example.test"));
    }

    private sealed class StubHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            respond(request);
    }
}
