using System.Text.Json;
using McpCad.Core.Modeling;
using McpCad.Core.Models;
using McpCad.Onshape;
using McpCad.Onshape.Modeling;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace McpCad.Tests.Onshape;

public sealed class OnshapePartModelingWireMockTests : IDisposable
{
    private const string FeaturesPath = "/api/v6/partstudios/d/doc-1/w/ws-1/e/e-part/features";
    private readonly WireMockServer _server = WireMockServer.Start();

    public void Dispose() => _server.Dispose();

    [Fact]
    public async Task Context_MapsBoundRectangleThenLinksItsSketchToNewBodyExtrusion()
    {
        StubFrame();
        StubWrites("native-sketch", "native-extrusion");
        var context = CreateAdapter().Bind(Target());

        var sketch = await context.CreateSketch(new SketchDefinition(
            CanonicalPlane.Top,
            LengthUnit.Centimeter,
            new RectangleProfile(new Point2(1, 2), new Point2(4, 5))), CancellationToken.None);
        var extrusion = await context.ExtrudeNewBody(sketch.Reference!, new ExtrusionDefinition(
            3, LengthUnit.Centimeter, ExtrusionDirection.Negative), CancellationToken.None);

        sketch.Status.Should().Be(OperationStatus.Succeeded);
        extrusion.Status.Should().Be(OperationStatus.Succeeded);
        context.Capabilities.Should().Be(new PartModelingCapabilities(
            OptionalCapabilityStatus.Unsupported, OptionalCapabilityStatus.Unsupported));
        Requests().Select(request => request.Method + " " + request.Path).Should().Equal(
            "GET " + FeaturesPath,
            "POST /api/v17/partstudios/d/doc-1/w/ws-1/e/e-part/featurescript",
            "POST " + FeaturesPath,
            "POST " + FeaturesPath);

        var writes = Requests().Where(request => request.Method == "POST" && request.Path == FeaturesPath).ToList();
        using var sketchBody = JsonDocument.Parse(writes[0].Body!);
        sketchBody.RootElement.GetProperty("feature").GetProperty("entities").GetArrayLength().Should().Be(4);
        sketchBody.RootElement.GetRawText().Should().Contain("0.01").And.Contain("0.05");
        using var extrusionBody = JsonDocument.Parse(writes[1].Body!);
        var parameters = extrusionBody.RootElement.GetProperty("feature").GetProperty("parameters");
        parameters.EnumerateArray().Single(parameter => parameter.GetProperty("parameterId").GetString() == "entities")
            .GetProperty("queries")[0].GetProperty("featureId").GetString().Should().Be("native-sketch");
        parameters.EnumerateArray().Single(parameter => parameter.GetProperty("parameterId").GetString() == "direction")
            .GetProperty("value").GetString().Should().Be("negative");
        parameters.EnumerateArray().Single(parameter => parameter.GetProperty("parameterId").GetString() == "operationType")
            .GetProperty("value").GetString().Should().Be("NEW");
    }

    [Fact]
    public async Task Context_CreatesBoundCircleSketchWithMeterRadius()
    {
        StubFrame();
        StubWrites("native-circle", "unused-extrusion");
        var context = CreateAdapter().Bind(Target());

        var sketch = await context.CreateSketch(Circle(), CancellationToken.None);

        sketch.Status.Should().Be(OperationStatus.Succeeded);
        sketch.Reference.Should().NotBeNull();
        var write = Requests().Single(request => request.Method == "POST" && request.Path == FeaturesPath);
        using var sketchBody = JsonDocument.Parse(write.Body!);
        sketchBody.RootElement.GetProperty("feature").GetProperty("entities").GetArrayLength().Should().Be(1);
        sketchBody.RootElement.GetRawText().Should().Contain("\"radius\"").And.Contain("0.005");
    }

    [Fact]
    public async Task Context_StopsBeforeWritesWhenFramePreflightFails()
    {
        StubFrame("{\"btType\":\"BTFeatureScriptEvalResponse-1859\",\"result\":{}}");
        var context = CreateAdapter().Bind(Target());

        var sketch = await context.CreateSketch(Circle(), CancellationToken.None);

        sketch.Status.Should().Be(OperationStatus.Failed);
        sketch.Mutation.Should().Be(MutationDisposition.None);
        context.Result.Extrusion.Status.Should().Be(OperationStatus.NotAttempted);
        Requests().Select(request => request.Path).Should().Equal(
            FeaturesPath,
            "/api/v17/partstudios/d/doc-1/w/ws-1/e/e-part/featurescript");
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public async Task Context_MarksDispatchedWriteUncertainWhenCancelled(int cancelledWrite)
    {
        var handler = new CancellationAfterWriteHandler(cancelledWrite);
        var context = CreateAdapter(handler).Bind(Target());

        if (cancelledWrite == 1)
        {
            var sketch = await context.CreateSketch(Circle(), CancellationToken.None);
            sketch.Should().Match<OperationResult<SketchReference>>(result =>
                result.Status == OperationStatus.Uncertain && result.Mutation == MutationDisposition.Unknown);
            (await context.CreateSketch(Circle(), CancellationToken.None)).Status.Should().Be(OperationStatus.Uncertain);
        }
        else
        {
            var sketch = await context.CreateSketch(Circle(), CancellationToken.None);
            var extrusion = await context.ExtrudeNewBody(sketch.Reference!, new ExtrusionDefinition(
                0.01, LengthUnit.Meter, ExtrusionDirection.Positive), CancellationToken.None);
            extrusion.Should().Match<OperationResult<FeatureReference>>(result =>
                result.Status == OperationStatus.Uncertain && result.Mutation == MutationDisposition.Unknown);
            (await context.ExtrudeNewBody(sketch.Reference!, new ExtrusionDefinition(
                0.01, LengthUnit.Meter, ExtrusionDirection.Positive), CancellationToken.None)).Status.Should().Be(OperationStatus.Uncertain);
        }

    }

    [Fact]
    public async Task Context_RejectsForeignReferenceWithoutRetryingOrWritingAnExtrusion()
    {
        StubFrame();
        StubWrites("native-sketch", "unused-extrusion");
        var first = CreateAdapter().Bind(Target());
        var second = CreateAdapter().Bind(Target());
        var sketch = await first.CreateSketch(Circle(), CancellationToken.None);

        var extrusion = await second.ExtrudeNewBody(sketch.Reference!, new ExtrusionDefinition(
            0.01, LengthUnit.Meter, ExtrusionDirection.Positive), CancellationToken.None);

        extrusion.Status.Should().Be(OperationStatus.Failed);
        extrusion.Mutation.Should().Be(MutationDisposition.None);
        Requests().Count(request => request.Method == "POST" && request.Path == FeaturesPath).Should().Be(1);
    }

    private OnshapePartModelingAdapter CreateAdapter() => new(
        new OnshapeClient(new HttpClient(), "sanitized-key", "sanitized-secret", _server.Url));

    private static OnshapePartModelingAdapter CreateAdapter(HttpMessageHandler handler) => new(
        new OnshapeClient(new HttpClient(handler), "sanitized-key", "sanitized-secret", "http://localhost"));

    private static CadElementAddress Target() => new("doc-1", "ws-1", null, "e-part");

    private static SketchDefinition Circle() => new(
        CanonicalPlane.Top,
        LengthUnit.Meter,
        new CircleProfile(new Point2(0.01, 0.02), 0.005));

    private void StubFrame(string? response = null)
    {
        _server.Given(Request.Create().WithPath(FeaturesPath).UsingGet())
            .RespondWith(Response.Create().WithStatusCode(200).WithBody("{\"libraryVersion\":4012}"));
        _server.Given(Request.Create().WithPath("/api/v17/partstudios/d/doc-1/w/ws-1/e/e-part/featurescript").UsingPost())
            .RespondWith(Response.Create().WithStatusCode(200).WithBody(response ?? FrameResponse()));
    }

    private void StubWrites(string sketchId, string extrusionId)
    {
        _server.Given(Request.Create().WithPath(FeaturesPath).UsingPost().WithBody(body => body!.Contains("\"btType\":\"BTMSketch-151\"")))
            .RespondWith(Response.Create().WithStatusCode(200).WithBody(
                JsonSerializer.Serialize(new { feature = new { featureId = sketchId } })));
        _server.Given(Request.Create().WithPath(FeaturesPath).UsingPost().WithBody(body => body!.Contains("\"featureType\":\"extrude\"")))
            .RespondWith(Response.Create().WithStatusCode(200).WithBody(
                JsonSerializer.Serialize(new { featureId = extrusionId })));
    }

    private IEnumerable<(string Method, string Path, string? Body)> Requests() => _server.LogEntries
        .Select(entry => (entry.RequestMessage.Method, entry.RequestMessage.AbsolutePath, entry.RequestMessage.Body))
        .Where(request => request.AbsolutePath.StartsWith("/api/", StringComparison.Ordinal))
        .Select(request => (request.Method, Path: request.AbsolutePath, request.Body));

    private sealed class CancellationAfterWriteHandler(int cancelledWrite) : HttpMessageHandler
    {
        private int _writes;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var path = request.RequestUri!.AbsolutePath;
            if (request.Method == HttpMethod.Get && path == FeaturesPath)
            {
                return Task.FromResult(Json("{\"libraryVersion\":4012}"));
            }

            if (path.EndsWith("/featurescript", StringComparison.Ordinal))
            {
                return Task.FromResult(Json(FrameResponse()));
            }

            _writes++;
            if (_writes == cancelledWrite)
            {
                return Task.FromCanceled<HttpResponseMessage>(new CancellationToken(true));
            }

            return Task.FromResult(Json("{\"feature\":{\"featureId\":\"native-sketch\"}}"));
        }

        private static HttpResponseMessage Json(string body) => new(System.Net.HttpStatusCode.OK) { Content = new StringContent(body) };
    }

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
}
