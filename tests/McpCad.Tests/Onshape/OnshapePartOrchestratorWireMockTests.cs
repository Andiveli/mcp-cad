using System.Text.Json;
using McpCad.Core.Models;
using McpCad.Onshape;
using McpCad.Server;
using McpCad.Tools.Onshape;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
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
    private readonly List<ServiceProvider> _serviceProviders = [];

    public void Dispose()
    {
        foreach (var provider in _serviceProviders)
        {
            provider.Dispose();
        }

        _server.Dispose();
    }

    [Fact]
    public void onshape_orchestrate_part_UsesRegisteredWorkspaceContextForEligibleRectangleWithoutDuplicateLegacyWrites()
    {
        _server.Given(Request.Create().WithPath(FeaturesPath).UsingGet())
            .RespondWith(Response.Create().WithStatusCode(200).WithBody("{\"libraryVersion\":4012}"));
        _server.Given(Request.Create().WithPath("/api/v17/partstudios/d/doc-1/w/ws-1/e/e-part/featurescript").UsingPost())
            .RespondWith(Response.Create().WithStatusCode(200).WithBody(FrameResponse()));
        _server.Given(Request.Create().WithPath(FeaturesPath).UsingPost().WithBody(body => body!.Contains("\"btType\":\"BTMSketch-151\"")))
            .RespondWith(Response.Create().WithStatusCode(200).WithBody("{\"feature\":{\"featureId\":\"native-sketch\"}}"));
        _server.Given(Request.Create().WithPath(FeaturesPath).UsingPost().WithBody(body => body!.Contains("\"featureType\":\"extrude\"")))
            .RespondWith(Response.Create().WithStatusCode(200).WithBody("{\"featureId\":\"native-extrude\"}"));

        var result = CreateTool().onshape_orchestrate_part(AddressJson, EligibleRectanglePlanJson);

        result.IsError.Should().NotBe(true);
        result.StructuredContent!.Value.GetProperty("stages")[0].GetProperty("nativeFeatureId").GetString().Should().Be("native-sketch");
        result.StructuredContent!.Value.GetProperty("stages")[1].GetProperty("nativeFeatureId").GetString().Should().Be("native-extrude");
        _server.LogEntries.Select(entry => $"{entry.RequestMessage.Method} {entry.RequestMessage.AbsolutePath}").Should().Equal(
            $"GET {FeaturesPath}",
            "POST /api/v17/partstudios/d/doc-1/w/ws-1/e/e-part/featurescript",
            $"POST {FeaturesPath}",
            $"POST {FeaturesPath}");
        _server.LogEntries.Count(entry => entry.RequestMessage.Method == "POST" && entry.RequestMessage.AbsolutePath == FeaturesPath).Should().Be(2);
        using var sketch = JsonDocument.Parse(_server.LogEntries[2].RequestMessage.Body!);
        sketch.RootElement.GetProperty("feature").GetProperty("entities").GetArrayLength().Should().Be(4);
        using var extrusion = JsonDocument.Parse(_server.LogEntries[3].RequestMessage.Body!);
        extrusion.RootElement.GetProperty("feature").GetProperty("parameters").EnumerateArray()
            .Single(parameter => parameter.GetProperty("parameterId").GetString() == "entities")
            .GetProperty("queries")[0].GetProperty("featureId").GetString().Should().Be("native-sketch");
        extrusion.RootElement.GetRawText().Should().Contain("0.02 m");
    }

    [Fact]
    public void onshape_orchestrate_part_UsesRegisteredWorkspaceContextForEligibleCircle()
    {
        StubWorkspaceContextWrites("native-circle", "native-extrude");

        var result = CreateTool().onshape_orchestrate_part(AddressJson, EligibleCirclePlanJson);

        result.IsError.Should().NotBe(true);
        result.StructuredContent!.Value.GetProperty("stages")[0].GetProperty("nativeFeatureId").GetString().Should().Be("native-circle");
        result.StructuredContent!.Value.GetProperty("stages")[1].GetProperty("nativeFeatureId").GetString().Should().Be("native-extrude");
        _server.LogEntries.Select(entry => $"{entry.RequestMessage.Method} {entry.RequestMessage.AbsolutePath}").Should().Equal(
            $"GET {FeaturesPath}",
            "POST /api/v17/partstudios/d/doc-1/w/ws-1/e/e-part/featurescript",
            $"POST {FeaturesPath}",
            $"POST {FeaturesPath}");
        _server.LogEntries.Count(entry => entry.RequestMessage.Method == "POST" && entry.RequestMessage.AbsolutePath == FeaturesPath).Should().Be(2);
        using var sketch = JsonDocument.Parse(_server.LogEntries[2].RequestMessage.Body!);
        sketch.RootElement.GetProperty("feature").GetProperty("entities")[0].GetProperty("geometry").GetProperty("radius").GetDouble().Should().Be(0.01);
    }

    [Fact]
    public void onshape_orchestrate_part_PreservesLegacyPolygonAddOrderWhenARegisteredFactoryCannotBridgeIt()
    {
        _server.Given(Request.Create().WithPath(FeaturesPath).UsingPost().WithBody(body => body?.Contains("\"btType\":\"BTMSketch-151\"") == true))
            .RespondWith(Response.Create().WithStatusCode(200).WithHeader("Content-Type", "application/json")
                .WithBody("""{"feature":{"featureId":"native-polygon"}}"""));
        _server.Given(Request.Create().WithPath(FeaturesPath).UsingPost().WithBody(body => body?.Contains("\"featureType\":\"extrude\"") == true))
            .RespondWith(Response.Create().WithStatusCode(200).WithHeader("Content-Type", "application/json")
                .WithBody("""{"featureId":"native-add"}"""));

        var result = CreateTool().onshape_orchestrate_part(AddressJson, PolygonAddPlanJson);

        result.IsError.Should().NotBe(true);
        result.StructuredContent!.Value.GetProperty("status").GetString().Should().Be("completed");
        result.StructuredContent!.Value.GetProperty("stages")[0].GetProperty("nativeFeatureId").GetString().Should().Be("native-polygon");
        result.StructuredContent!.Value.GetProperty("stages")[1].GetProperty("nativeFeatureId").GetString().Should().Be("native-add");
        _server.LogEntries.Select(entry => $"{entry.RequestMessage.Method} {entry.RequestMessage.AbsolutePath}").Should().Equal(
            $"POST {FeaturesPath}", $"POST {FeaturesPath}");
        using var extrusion = JsonDocument.Parse(_server.LogEntries[1].RequestMessage.Body!);
        extrusion.RootElement.GetProperty("feature").GetProperty("parameters").EnumerateArray()
            .Single(parameter => parameter.GetProperty("parameterId").GetString() == "operationType")
            .GetProperty("value").GetString().Should().Be("ADD");
    }

    [Fact]
    public void onshape_orchestrate_part_UsesLegacyVersionEndpointsWithoutWorkspaceContextCalls()
    {
        const string versionFeaturesPath = "/api/v6/partstudios/d/doc-1/v/ws-1/e/e-part/features";
        _server.Given(Request.Create().WithPath(versionFeaturesPath).UsingPost().WithBody(body => body?.Contains("\"btType\":\"BTMSketch-151\"") == true))
            .RespondWith(Response.Create().WithStatusCode(200).WithBody("""{"featureId":"version-sketch"}"""));
        _server.Given(Request.Create().WithPath(versionFeaturesPath).UsingPost().WithBody(body => body?.Contains("\"featureType\":\"extrude\"") == true))
            .RespondWith(Response.Create().WithStatusCode(200).WithBody("""{"featureId":"version-extrude"}"""));

        var result = CreateTool("v").onshape_orchestrate_part(AddressJson, EligibleRectanglePlanJson);

        result.IsError.Should().NotBe(true);
        result.StructuredContent!.Value.GetProperty("status").GetString().Should().Be("completed");
        _server.LogEntries.Select(entry => $"{entry.RequestMessage.Method} {entry.RequestMessage.AbsolutePath}").Should().Equal(
            $"POST {versionFeaturesPath}", $"POST {versionFeaturesPath}");
        _server.LogEntries.Should().NotContain(entry => entry.RequestMessage.AbsolutePath.Contains("/w/", StringComparison.Ordinal));
        _server.LogEntries.Should().NotContain(entry => entry.RequestMessage.AbsolutePath.EndsWith("/featurescript", StringComparison.Ordinal));
    }

    [Fact]
    public void onshape_orchestrate_part_PreservesLegacyCheckpointCaptureOrderWithRegisteredFactory()
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

        AddHarnessRootProbes();

        var result = CreateTool().onshape_orchestrate_part(AddressJson, PlanJson);

        result.IsError.Should().NotBe(true);
        result.Content.Should().ContainSingle().Which.Should().BeOfType<ImageContentBlock>();
        var structured = result.StructuredContent!.Value;
        structured.GetProperty("status").GetString().Should().Be("completed");
        structured.GetProperty("stages")[0].GetProperty("nativeFeatureId").GetString().Should().Be("native-sketch");
        structured.GetProperty("stages")[2].GetProperty("nativeFeatureId").GetString().Should().Be("native-extrude");

        var requests = ScenarioRequests().ToList();
        requests.Select(request => $"{request.Method} {request.Path}").Should().Equal(
            $"POST {FeaturesPath}", $"GET {ShadedViewsPath}", $"POST {FeaturesPath}");
        _server.Url.Should().StartWith("http://localhost");
        var shadedView = requests.Single(request => request.Path == ShadedViewsPath);
        shadedView.RawQuery.Should().Contain("outputWidth=800").And.Contain("outputHeight=600").And.Contain("edges=show");

        var sketchPost = requests.First().Body!;
        using var sketch = JsonDocument.Parse(sketchPost);
        sketch.RootElement.GetProperty("feature").GetProperty("entities")[0].GetProperty("geometry").GetProperty("pntX").GetDouble().Should().Be(0.05);
        sketch.RootElement.GetProperty("feature").GetProperty("entities")[0].GetProperty("geometry").GetProperty("pntY").GetDouble().Should().Be(0);

        using var extrude = JsonDocument.Parse(requests.Last().Body!);
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

    // WireMock logs every listener request without scenario correlation. BRIDGE-4 observed
    // unqueried GET / probes during parallel execution; retain every API request and all other root access.
    private IEnumerable<(string Method, string Path, string RawQuery, string? Body)> ScenarioRequests() => _server.LogEntries
        .Select(entry => (Method: entry.RequestMessage.Method, Path: entry.RequestMessage.AbsolutePath, entry.RequestMessage.RawQuery, entry.RequestMessage.Body))
        .Where(request => request.Method != "GET" || request.Path != "/" || !string.IsNullOrEmpty(request.RawQuery));

    private void AddHarnessRootProbes()
    {
        using var client = new HttpClient();
        using var first = client.GetAsync(_server.Url!).GetAwaiter().GetResult();
        using var second = client.GetAsync(_server.Url!).GetAwaiter().GetResult();
    }

    private void StubWorkspaceContextWrites(string sketchId, string extrusionId)
    {
        _server.Given(Request.Create().WithPath(FeaturesPath).UsingGet())
            .RespondWith(Response.Create().WithStatusCode(200).WithBody("{\"libraryVersion\":4012}"));
        _server.Given(Request.Create().WithPath("/api/v17/partstudios/d/doc-1/w/ws-1/e/e-part/featurescript").UsingPost())
            .RespondWith(Response.Create().WithStatusCode(200).WithBody(FrameResponse()));
        _server.Given(Request.Create().WithPath(FeaturesPath).UsingPost().WithBody(body => body!.Contains("\"btType\":\"BTMSketch-151\"")))
            .RespondWith(Response.Create().WithStatusCode(200).WithBody(JsonSerializer.Serialize(new { feature = new { featureId = sketchId } })));
        _server.Given(Request.Create().WithPath(FeaturesPath).UsingPost().WithBody(body => body!.Contains("\"featureType\":\"extrude\"")))
            .RespondWith(Response.Create().WithStatusCode(200).WithBody(JsonSerializer.Serialize(new { featureId = extrusionId })));
    }

    private OnshapeSkillTools CreateTool(string defaultWvm = "w")
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Provider:Active"] = "onshape",
                ["Onshape:BaseUrl"] = _server.Url,
                ["Onshape:AccessKey"] = "local-access-key",
                ["Onshape:SecretKey"] = "local-secret",
                ["Onshape:DefaultWvm"] = defaultWvm,
            })
            .Build();
        var services = new ServiceCollection();
        ProviderBootstrap.RegisterCadProvider(services, configuration);
        var provider = services.BuildServiceProvider();
        _serviceProviders.Add(provider);

        provider.GetRequiredService<McpCad.Core.Modeling.IPartModelingContextFactory>().Should().NotBeNull();
        return provider.GetRequiredService<OnshapeSkillTools>();
    }

    private static string RepositoryFile(params string[] segments) => Path.GetFullPath(Path.Combine(
        [AppContext.BaseDirectory, "..", "..", "..", "..", "..", .. segments]));

    private static string FrameResponse()
    {
        object Value(string type, object value) => new Dictionary<string, object?> { ["btType"] = type, ["value"] = value };
        object Row(string name, double x, double y, double z) => Value("BTFSValueArray-1499", new[]
        {
            Value("BTFSValueString-1422", name), Value("BTFSValueNumber-772", x),
            Value("BTFSValueNumber-772", y), Value("BTFSValueNumber-772", z),
        });
        var result = Value("BTFSValueArray-1499", new[]
        {
            Row("origin", 0, 0, 0), Row("x", 1, 0, 0), Row("y", 0, 1, 0), Row("normal", 0, 0, 1),
        });
        return JsonSerializer.Serialize(new { btType = "BTFeatureScriptEvalResponse-1859", result });
    }

    private const string ShadedViewsPath = "/api/v16/partstudios/d/doc-1/w/ws-1/e/e-part/shadedviews";
    private const string AddressJson = """{"documentId":"doc-1","workspaceOrVersionId":"ws-1","elementId":"e-part"}""";
    private const string EligibleRectanglePlanJson = """
        {"schemaVersion":1,"stages":[
          {"id":"sketch","kind":"sketch","plane":"Top","profile":{"type":"rectangle","x1_cm":0,"y1_cm":0,"x2_cm":10,"y2_cm":5}},
          {"id":"extrude","kind":"extrude","sketchId":"sketch","operation":"new","depth_cm":2,"direction":"positive"}
        ]}
        """;
    private const string EligibleCirclePlanJson = """
        {"schemaVersion":1,"stages":[
          {"id":"sketch","kind":"sketch","plane":"Top","profile":{"type":"circle","cx_cm":1,"cy_cm":2,"radius_cm":1}},
          {"id":"extrude","kind":"extrude","sketchId":"sketch","operation":"new","depth_cm":2,"direction":"positive"}
        ]}
        """;
    private const string PolygonAddPlanJson = """
        {"schemaVersion":1,"stages":[
          {"id":"sketch","kind":"sketch","plane":"Top","profile":{"type":"polygon","points":[{"x_cm":0,"y_cm":0},{"x_cm":10,"y_cm":0},{"x_cm":0,"y_cm":5},{"x_cm":0,"y_cm":0}]}},
          {"id":"extrude","kind":"extrude","sketchId":"sketch","operation":"add","depth_cm":2,"direction":"positive"}
        ]}
        """;
    private const string PlanJson = """
        {"schemaVersion":1,"stages":[
          {"id":"sketch","kind":"sketch","plane":"Top","profile":{"type":"rectangle","x1_cm":0,"y1_cm":0,"x2_cm":10,"y2_cm":5}},
          {"id":"checkpoint","kind":"checkpoint","afterStageId":"sketch","view":"iso"},
          {"id":"extrude","kind":"extrude","sketchId":"sketch","operation":"new","depth_cm":2,"direction":"positive"}
        ]}
        """;
}
