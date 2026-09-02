using System.Text.Json;
using McpCad.Core.Models;
using McpCad.Onshape;
using McpCad.Tools.Onshape;
using ModelContextProtocol.Protocol;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace McpCad.Tests.Onshape;

public sealed class OnshapeGodPartWireMockTests : IDisposable
{
    private const string MinimalPngBase64 = "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVQIHWP4z8DwHwAFgAI/ScL1ZQAAAABJRU5ErkJggg==";
    private readonly WireMockServer _server = WireMockServer.Start();

    public void Dispose() => _server.Dispose();

    [Fact]
    public void skill_onshape_god_part_NestedLiveResponses_LinkSketchAndCaptureIsoThenTop()
    {
        const string featuresPath = "/api/v6/partstudios/d/doc-1/w/ws-1/e/e-part/features";
        _server
            .Given(Request.Create().WithPath(featuresPath).UsingPost().WithBody(body => body.Contains("\"btType\":\"BTMSketch-151\"")))
            .RespondWith(Response.Create().WithStatusCode(200).WithHeader("Content-Type", "application/json")
                .WithBody("""{"feature":{"featureId":"live-sketch-1","featureType":"newSketch"},"featureState":{"featureStatus":"OK"},"microversion":"mv-sketch"}"""));
        _server
            .Given(Request.Create().WithPath(featuresPath).UsingPost().WithBody(body => body.Contains("\"featureType\":\"extrude\"")))
            .RespondWith(Response.Create().WithStatusCode(200).WithHeader("Content-Type", "application/json")
                .WithBody("""{"feature":{"featureId":"live-extrude-1","featureType":"extrude"},"featureState":{"featureStatus":"OK"},"microversion":"mv-extrude"}"""));
        _server
            .Given(Request.Create().WithPath("/api/v16/partstudios/d/doc-1/w/ws-1/e/e-part/shadedviews").UsingGet())
            .RespondWith(Response.Create().WithStatusCode(200).WithHeader("Content-Type", "application/json")
                .WithBody($$"""{"images":["{{MinimalPngBase64}}"]}"""));

        var http = new HttpClient { BaseAddress = new Uri(_server.Url!) };
        var client = new OnshapeClient(http, "ak", "sk", _server.Url);
        var provider = new OnshapeProvider(client, new OnshapeDriver(client) { DefaultWvm = "w" });
        var result = new OnshapeSkillTools(provider).skill_onshape_god_part(
            new CadElementAddress("doc-1", "ws-1", null, "e-part").Serialize(), 0, 0, 10, 5, 2);

        result.IsError.Should().NotBe(true);
        result.Content.Should().HaveCount(2).And.OnlyContain(content => content is ImageContentBlock);
        var structured = Structured(result);
        structured["success"].Should().Be(true);
        structured["modelingSuccess"].Should().Be(true);
        structured["captureSuccess"].Should().Be(true);
        structured["finalFeatureId"].Should().Be("live-extrude-1");
        var captures = Assert.IsType<List<object?>>(structured["captures"]);
        captures.Select(capture => Assert.IsType<Dictionary<string, object?>>(capture)["view"]).Should().Equal("iso", "top");

        var posts = _server.LogEntries.Where(entry => entry.RequestMessage.Method == "POST" && entry.RequestMessage.AbsolutePath == featuresPath).ToList();
        posts.Should().HaveCount(2);
        using var extrude = JsonDocument.Parse(posts.Single(entry => entry.RequestMessage.Body!.Contains("\"featureType\":\"extrude\"")).RequestMessage.Body!);
        extrude.RootElement.GetProperty("feature").GetProperty("parameters").EnumerateArray()
            .Single(parameter => parameter.GetProperty("parameterId").GetString() == "entities")
            .GetProperty("queries")[0].GetProperty("featureId").GetString().Should().Be("live-sketch-1");
        _server.LogEntries.Count(entry => entry.RequestMessage.Method == "GET" && entry.RequestMessage.AbsolutePath.EndsWith("/shadedviews", StringComparison.Ordinal)).Should().Be(2);
    }

    private static Dictionary<string, object?> Structured(CallToolResult result) =>
        Assert.IsType<Dictionary<string, object?>>(Normalize(result.StructuredContent!.Value));

    private static object? Normalize(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.Object => value.EnumerateObject().ToDictionary(property => property.Name, property => Normalize(property.Value)),
        JsonValueKind.Array => value.EnumerateArray().Select(Normalize).ToList(),
        JsonValueKind.String => value.GetString(),
        JsonValueKind.True => true, JsonValueKind.False => false, JsonValueKind.Null => null,
        JsonValueKind.Number when value.TryGetInt64(out var integer) => integer, JsonValueKind.Number => value.GetDouble(),
        _ => throw new InvalidOperationException($"Unexpected JSON value: {value.ValueKind}"),
    };
}
