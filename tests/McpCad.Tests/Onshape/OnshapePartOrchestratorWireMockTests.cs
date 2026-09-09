using System.Text.Json;
using McpCad.Core.Models;
using McpCad.Onshape;
using McpCad.Tools.Onshape;
using ModelContextProtocol.Protocol;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace McpCad.Tests.Onshape;

public sealed class OnshapePartOrchestratorWireMockTests : IDisposable
{
    private const string MinimalPngBase64 = "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVQIHWP4z8DwHwAFgAI/ScL1ZQAAAABJRU5ErkJggg==";
    private const string FeaturesPath = "/api/v6/partstudios/d/doc-1/w/ws-1/e/e-part/features";
    private readonly WireMockServer _server = WireMockServer.Start();

    public void Dispose() => _server.Dispose();

    [Fact]
    public void onshape_orchestrate_part_UsesOnlyLocalWireMock_AndPreservesWriteCaptureWriteOrder()
    {
        _server.Given(Request.Create().WithPath(FeaturesPath).UsingPost().WithBody(body => body?.Contains("\"btType\":\"BTMSketch-151\"") == true))
            .RespondWith(Response.Create().WithStatusCode(200).WithHeader("Content-Type", "application/json")
                .WithBody("""{"feature":{"featureId":"native-sketch"}}"""));
        _server.Given(Request.Create().WithPath(FeaturesPath).UsingPost().WithBody(body => body?.Contains("\"featureType\":\"extrude\"") == true))
            .RespondWith(Response.Create().WithStatusCode(200).WithHeader("Content-Type", "application/json")
                .WithBody("""{"featureId":"native-extrude"}"""));
        _server.Given(Request.Create().WithPath(ShadedViewsPath).UsingGet())
            .RespondWith(Response.Create().WithStatusCode(200).WithHeader("Content-Type", "application/json")
                .WithBody($$"""{"images":["{{MinimalPngBase64}}"]}"""));

        var result = CreateTool().onshape_orchestrate_part(AddressJson, PlanJson);

        result.IsError.Should().NotBe(true);
        result.Content.Should().ContainSingle().Which.Should().BeOfType<ImageContentBlock>();
        var structured = result.StructuredContent!.Value;
        structured.GetProperty("status").GetString().Should().Be("completed");
        structured.GetProperty("stages")[0].GetProperty("nativeFeatureId").GetString().Should().Be("native-sketch");
        structured.GetProperty("stages")[2].GetProperty("nativeFeatureId").GetString().Should().Be("native-extrude");

        var requests = _server.LogEntries.Select(entry => $"{entry.RequestMessage.Method} {entry.RequestMessage.AbsolutePath}");
        requests.Should().Equal($"POST {FeaturesPath}", $"GET {ShadedViewsPath}", $"POST {FeaturesPath}");
        _server.Url.Should().StartWith("http://localhost");
        var shadedView = _server.LogEntries.Single(entry => entry.RequestMessage.AbsolutePath == ShadedViewsPath).RequestMessage;
        shadedView.RawQuery.Should().Contain("outputWidth=800").And.Contain("outputHeight=600").And.Contain("edges=show");

        var sketchPost = _server.LogEntries.First().RequestMessage.Body!;
        using var sketch = JsonDocument.Parse(sketchPost);
        sketch.RootElement.GetProperty("feature").GetProperty("entities")[0].GetProperty("geometry").GetProperty("pntX").GetDouble().Should().Be(0.05);
        sketch.RootElement.GetProperty("feature").GetProperty("entities")[0].GetProperty("geometry").GetProperty("pntY").GetDouble().Should().Be(0);

        using var extrude = JsonDocument.Parse(_server.LogEntries.Last().RequestMessage.Body!);
        var parameters = extrude.RootElement.GetProperty("feature").GetProperty("parameters");
        parameters.EnumerateArray().Single(parameter => parameter.GetProperty("parameterId").GetString() == "entities")
            .GetProperty("queries")[0].GetProperty("featureId").GetString().Should().Be("native-sketch");
        extrude.RootElement.GetRawText().Should().Contain("0.02 m");
    }

    [Fact]
    public void onshape_orchestrate_part_ContinuesAfterLocalCaptureTransportFailure()
    {
        _server.Given(Request.Create().WithPath(FeaturesPath).UsingPost().WithBody(body => body?.Contains("\"btType\":\"BTMSketch-151\"") == true))
            .RespondWith(Response.Create().WithStatusCode(200).WithHeader("Content-Type", "application/json")
                .WithBody("""{"featureId":"top-level-sketch"}"""));
        _server.Given(Request.Create().WithPath(FeaturesPath).UsingPost().WithBody(body => body?.Contains("\"featureType\":\"extrude\"") == true))
            .RespondWith(Response.Create().WithStatusCode(200).WithHeader("Content-Type", "application/json")
                .WithBody("""{"feature":{"featureId":"nested-extrude"}}"""));
        _server.Given(Request.Create().WithPath(ShadedViewsPath).UsingGet()).RespondWith(Response.Create().WithStatusCode(503));

        var result = CreateTool().onshape_orchestrate_part(AddressJson, PlanJson);

        result.IsError.Should().Be(true);
        result.Content.Should().BeEmpty();
        result.StructuredContent!.Value.GetProperty("status").GetString().Should().Be("completed_with_capture_failures");
        result.StructuredContent!.Value.GetProperty("stages")[1].GetProperty("captureStatus").GetString().Should().Be("failed");
        result.StructuredContent!.Value.GetProperty("stages")[2].GetProperty("nativeFeatureId").GetString().Should().Be("nested-extrude");
        var requests = _server.LogEntries.ToList();
        var firstWrite = requests.FindIndex(entry => entry.RequestMessage.Method == "POST");
        var capture = requests.FindIndex(entry => entry.RequestMessage.AbsolutePath == ShadedViewsPath);
        var finalWrite = requests.FindLastIndex(entry => entry.RequestMessage.Method == "POST");
        firstWrite.Should().BeLessThan(capture);
        capture.Should().BeLessThan(finalWrite);
    }

    [Fact]
    public void OrchestratorDocumentation_StatesTheBoundedProviderContract()
    {
        var providers = File.ReadAllText(RepositoryFile("docs", "providers.md"));
        var matrix = File.ReadAllText(RepositoryFile("docs", "onshape-parity-matrix.md"));

        providers.Should().Contain("onshape_orchestrate_part")
            .And.Contain("single-body-result assumption")
            .And.Contain("fresh or otherwise known Part Studio")
            .And.Contain("non-idempotent")
            .And.Contain("no rollback or retry")
            .And.Contain("completed_with_capture_failures")
            .And.Contain("partial_modeling_failure")
            .And.Contain("evidence only");
        matrix.Should().Contain("onshape_orchestrate_part")
            .And.Contain("Top`, `Front`, or `Right")
            .And.Contain("rectangle, circle, polygon, or mixed")
            .And.Contain("checkpoint")
            .And.Contain("completed_with_capture_failures")
            .And.Contain("partial_modeling_failure")
            .And.Contain("no rollback or retry");
    }

    private OnshapeSkillTools CreateTool()
    {
        var http = new HttpClient { BaseAddress = new Uri(_server.Url!) };
        var client = new OnshapeClient(http, "local-access-key", "local-secret", _server.Url);
        return new OnshapeSkillTools(new OnshapeProvider(client, new OnshapeDriver(client) { DefaultWvm = "w" }));
    }

    private static string RepositoryFile(params string[] segments) => Path.GetFullPath(Path.Combine(
        [AppContext.BaseDirectory, "..", "..", "..", "..", "..", .. segments]));

    private const string ShadedViewsPath = "/api/v16/partstudios/d/doc-1/w/ws-1/e/e-part/shadedviews";
    private const string AddressJson = """{"documentId":"doc-1","workspaceOrVersionId":"ws-1","elementId":"e-part"}""";
    private const string PlanJson = """
        {"schemaVersion":1,"stages":[
          {"id":"sketch","kind":"sketch","plane":"Top","profile":{"type":"rectangle","x1_cm":0,"y1_cm":0,"x2_cm":10,"y2_cm":5}},
          {"id":"checkpoint","kind":"checkpoint","afterStageId":"sketch","view":"iso"},
          {"id":"extrude","kind":"extrude","sketchId":"sketch","operation":"new","depth_cm":2,"direction":"positive"}
        ]}
        """;
}
