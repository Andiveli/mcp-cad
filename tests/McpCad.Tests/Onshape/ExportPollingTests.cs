using McpCad.Core.Exceptions;
using McpCad.Onshape;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace McpCad.Tests.Onshape;

public class ExportPollingTests : IDisposable
{
    private readonly WireMockServer _server;

    public ExportPollingTests()
    {
        _server = WireMockServer.Start();
    }

    public void Dispose() => _server.Dispose();

    [Fact]
    public async Task PollTranslationAsync_WhenDone_ReturnsDownloadUrl()
    {
        var tid = "trans-abc";
        _server
            .Given(Request.Create().WithPath($"/api/v6/translations/{tid}").UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody("""{"requestState":"DONE","resultExternalDataIds":["https://cdn.example/step.part"]}"""));

        using var http = new HttpClient { BaseAddress = new Uri(_server.Url!) };
        var client = new OnshapeClient(http, "ak", "sk", _server.Url);

        var result = await client.PollTranslationAsync(tid);

        result["requestState"].Should().Be("DONE");
        result["downloadUrl"].Should().Be("https://cdn.example/step.part");
    }

    [Fact]
    public async Task PollTranslationAsync_PendingPast120s_ThrowsExportTimeout()
    {
        var tid = "trans-slow";
        _server
            .Given(Request.Create().WithPath($"/api/v6/translations/{tid}").UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody("""{"requestState":"PENDING"}"""));

        using var http = new HttpClient { BaseAddress = new Uri(_server.Url!) };
        var client = new OnshapeClient(http, "ak", "sk", _server.Url);

        var act = async () => await client.PollTranslationAsync(
            tid,
            pollInterval: TimeSpan.FromMilliseconds(20),
            pollTimeout: TimeSpan.FromMilliseconds(80));

        var ex = await act.Should().ThrowAsync<OnshapeApiException>();
        ex.Which.Reason.Should().Be("export_timeout");
    }

        [Fact]
        public async Task PollTranslationAsync_Transient500ThenDone_Succeeds()
        {
            var tid = "trans-flaky";
            var calls = 0;
            var doneJson = """{"requestState":"DONE","resultExternalDataIds":["https://cdn.example/ok"]}""";
            _server
                .Given(Request.Create().WithPath($"/api/v6/translations/{tid}").UsingGet())
                .RespondWith(Response.Create().WithCallback(_ =>
                {
                    var n = Interlocked.Increment(ref calls);
                    if (n == 1)
                    {
                        return new WireMock.ResponseMessage
                        {
                            StatusCode = 500,
                            BodyData = new WireMock.Util.BodyData { BodyAsString = "server blip" },
                        };
                    }

                    return new WireMock.ResponseMessage
                    {
                        StatusCode = 200,
                        BodyData = new WireMock.Util.BodyData { BodyAsString = doneJson },
                    };
                }));

            using var http = new HttpClient { BaseAddress = new Uri(_server.Url!) };
            var client = new OnshapeClient(http, "ak", "sk", _server.Url);

            var result = await client.PollTranslationAsync(
                tid,
                pollInterval: TimeSpan.FromMilliseconds(10),
                pollTimeout: TimeSpan.FromSeconds(2));

            result["downloadUrl"].Should().Be("https://cdn.example/ok");
            calls.Should().BeGreaterThan(1);
        }

    [Fact]
    public async Task SendAsync_Write_AddsRejectMicroversionSkewQuery()
    {
        string? capturedPath = null;
            _server
                .Given(Request.Create().WithPath("/api/v6/elements/d/d1/w/w1/e/e1").UsingPost())
                .RespondWith(Response.Create().WithCallback(req =>
                {
                    capturedPath = req.AbsolutePath + req.RawQuery;
                    return new WireMock.ResponseMessage
                    {
                        StatusCode = 200,
                        Headers = new Dictionary<string, WireMock.Types.WireMockList<string>>
                        {
                            ["Content-Type"] = "application/json",
                        },
                        BodyData = new WireMock.Util.BodyData { BodyAsString = """{"success":true}""" },
                    };
                }));

        using var http = new HttpClient { BaseAddress = new Uri(_server.Url!) };
        var client = new OnshapeClient(http, "ak", "sk", _server.Url);

        await client.SendAsync(HttpMethod.Post, "/api/v6/elements/d/d1/w/w1/e/e1", "{}");

        capturedPath.Should().Contain("rejectMicroversionSkew=true");
    }

    [Fact]
    public async Task SendAsync_Get_OmitsRejectMicroversionSkew()
    {
        string? capturedQuery = null;
            _server
                .Given(Request.Create().WithPath("/api/v6/elements/d/d1/w/w1/e/e1").UsingGet())
                .RespondWith(Response.Create().WithCallback(req =>
                {
                    capturedQuery = req.RawQuery;
                    return new WireMock.ResponseMessage
                    {
                        StatusCode = 200,
                        Headers = new Dictionary<string, WireMock.Types.WireMockList<string>>
                        {
                            ["Content-Type"] = "application/json",
                        },
                        BodyData = new WireMock.Util.BodyData { BodyAsString = """{"microversion":"mv1"}""" },
                    };
                }));

        using var http = new HttpClient { BaseAddress = new Uri(_server.Url!) };
        var client = new OnshapeClient(http, "ak", "sk", _server.Url);

        await client.SendAsync(HttpMethod.Get, "/api/v6/elements/d/d1/w/w1/e/e1");

        (capturedQuery ?? string.Empty).Should().NotContain("rejectMicroversionSkew");
    }

    [Fact]
    public async Task SendAsync_429WithRetryAfterDelta_RetriesOnceThenSucceeds()
    {
        var calls = 0;
            _server
                .Given(Request.Create().WithPath("/api/v6/ping").UsingGet())
                .RespondWith(Response.Create().WithCallback(_ =>
                {
                    calls++;
                    if (calls == 1)
                    {
                        return new WireMock.ResponseMessage
                        {
                            StatusCode = 429,
                            Headers = new Dictionary<string, WireMock.Types.WireMockList<string>>
                            {
                                ["Retry-After"] = "1",
                            },
                        };
                    }

                    return new WireMock.ResponseMessage
                    {
                        StatusCode = 200,
                        Headers = new Dictionary<string, WireMock.Types.WireMockList<string>>
                        {
                            ["Content-Type"] = "application/json",
                        },
                        BodyData = new WireMock.Util.BodyData { BodyAsString = """{"ok":true}""" },
                    };
                }));

        using var http = new HttpClient { BaseAddress = new Uri(_server.Url!) };
        var client = new OnshapeClient(http, "ak", "sk", _server.Url);

        using var response = await client.SendAsync(HttpMethod.Get, "/api/v6/ping");

        response.IsSuccessStatusCode.Should().BeTrue();
        calls.Should().Be(2);
    }

    [Fact]
    public async Task SendAsync_429WithoutRetryAfter_ThrowsOnshapeQuotaException_NotObjectDisposed()
    {
        _server
            .Given(Request.Create().WithPath("/api/v6/ping").UsingGet())
            .RespondWith(Response.Create().WithStatusCode(429));

        using var http = new HttpClient { BaseAddress = new Uri(_server.Url!) };
        var client = new OnshapeClient(http, "ak", "sk", _server.Url);

        var act = async () =>
        {
            using var _ = await client.SendAsync(HttpMethod.Get, "/api/v6/ping");
        };

        var ex = await act.Should().ThrowAsync<OnshapeQuotaException>();
        ex.Which.StatusCode.Should().Be(429);
    }

    [Fact]
    public async Task SendAsync_401_ThrowsOnshapeAuthException()
    {
        _server
            .Given(Request.Create().WithPath("/api/v6/ping").UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(401)
                .WithBody("""{"message":"invalid credentials"}"""));

        using var http = new HttpClient { BaseAddress = new Uri(_server.Url!) };
        var client = new OnshapeClient(http, "ak", "sk", _server.Url);

        var act = async () =>
        {
            using var _ = await client.SendAsync(HttpMethod.Get, "/api/v6/ping");
        };

        var ex = await act.Should().ThrowAsync<OnshapeAuthException>();
        ex.Which.StatusCode.Should().Be(401);
    }
}