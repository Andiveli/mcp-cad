using System.Text.Json;
using McpCad.Core.Modeling;
using McpCad.Core.Models;
using McpCad.Onshape;
using McpCad.Onshape.Modeling;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace McpCad.Tests.Onshape;

public sealed class OnshapePartModelingPreflightAdapterTests : IDisposable
{
    private readonly WireMockServer _server = WireMockServer.Start();

    public void Dispose() => _server.Dispose();

    [Fact]
    public async Task PrepareAsync_UsesItsBoundAddressThenReturnsNormalizedMappedGeometry()
    {
        StubFrame(FrameResponse());
        var target = new CadElementAddress("bound-document", "bound-workspace", null, "bound-element");
        var adapter = CreateAdapter(target);
        var sketch = new SketchDefinition(
            CanonicalPlane.Top,
            LengthUnit.Centimeter,
            new RectangleProfile(new Point2(2, -1), new Point2(7, 3)));
        var extrusion = new ExtrusionDefinition(8, LengthUnit.Centimeter, ExtrusionDirection.Positive);

        var geometry = await adapter.PrepareAsync(sketch, extrusion, CancellationToken.None);

        var rectangle = geometry.Profile.Should().BeOfType<OnshapeMappedRectangle>().Subject;
        rectangle.CornersM.Should().Equal(
            new OnshapeMappedPoint(0.02, -0.01),
            new OnshapeMappedPoint(0.07, -0.01),
            new OnshapeMappedPoint(0.07, 0.03),
            new OnshapeMappedPoint(0.02, 0.03));
        geometry.Extrusion.Should().Be(new OnshapeMappedExtrusion(0.08, ExtrusionDirection.Positive));
        RequestPaths().Should().Equal(
            ("GET", "/api/v6/partstudios/d/bound-document/w/bound-workspace/e/bound-element/features"),
            ("POST", "/api/v17/partstudios/d/bound-document/w/bound-workspace/e/bound-element/featurescript"));
        NoModelWrites();
    }

    [Fact]
    public async Task PrepareAsync_MapsCircleAndFlipsDirectionThroughTheReadFrame()
    {
        StubFrame(InvertedTopFrameResponse());
        var sketch = new SketchDefinition(
            CanonicalPlane.Top,
            LengthUnit.Meter,
            new CircleProfile(new Point2(0.01, 0.02), 0.005));

        var geometry = await CreateAdapter().PrepareAsync(
            sketch,
            new ExtrusionDefinition(0.08, LengthUnit.Meter, ExtrusionDirection.Positive),
            CancellationToken.None);

        geometry.Profile.Should().Be(new OnshapeMappedCircle(new OnshapeMappedPoint(0.01, -0.02), 0.005));
        geometry.Extrusion.Should().Be(new OnshapeMappedExtrusion(0.08, ExtrusionDirection.Negative));
        RequestPaths().Should().HaveCount(2);
        NoModelWrites();
    }

    [Fact]
    public async Task PrepareAsync_RejectsInvalidPortableInputBeforeAnyTransport()
    {
        var sketch = new SketchDefinition(
            CanonicalPlane.Top,
            LengthUnit.Meter,
            new RectangleProfile(new Point2(0, 0), new Point2(0, 0.01)));

        var act = () => CreateAdapter().PrepareAsync(sketch, Extrusion(), CancellationToken.None);

        var error = (await act.Should().ThrowAsync<OnshapePartModelingPreflightException>()).Which;
        error.ValidationError.Code.Should().Be(PartModelingErrorCode.DegenerateGeometry);
        _server.LogEntries.Should().BeEmpty();
    }

    [Fact]
    public async Task PrepareAsync_StopsAfterFrameReadFailureWithoutAnyModelWrite()
    {
        StubFrame("{\"btType\":\"BTFeatureScriptEvalResponse-1859\",\"result\":{}}");

        var act = () => CreateAdapter().PrepareAsync(ValidSketch(), Extrusion(), CancellationToken.None);

        await act.Should().ThrowAsync<OnshapePartModelingFrameReadException>();
        RequestPaths().Should().HaveCount(2);
        NoModelWrites();
    }

    private OnshapePartModelingPreflightAdapter CreateAdapter(CadElementAddress? target = null) => new(
        new OnshapeClient(new HttpClient(), "sanitized-key", "sanitized-secret", _server.Url),
        target ?? new CadElementAddress("test-document", "test-workspace", null, "test-element"));

    private static SketchDefinition ValidSketch() => new(
        CanonicalPlane.Top,
        LengthUnit.Meter,
        new CircleProfile(new Point2(0.01, 0.02), 0.005));

    private static ExtrusionDefinition Extrusion() => new(0.08, LengthUnit.Meter, ExtrusionDirection.Positive);

    private void StubFrame(string response)
    {
        _server.Given(Request.Create().WithPath("/api/v6/partstudios/d/*/w/*/e/*/features").UsingGet())
            .RespondWith(Response.Create().WithStatusCode(200).WithBody("{\"libraryVersion\":4012}"));
        _server.Given(Request.Create().WithPath("/api/v17/partstudios/d/*/w/*/e/*/featurescript").UsingPost())
            .RespondWith(Response.Create().WithStatusCode(200).WithBody(response));
    }

    private IEnumerable<(string Method, string Path)> RequestPaths() => _server.LogEntries
        .Select(entry => (Method: entry.RequestMessage.Method, Path: entry.RequestMessage.AbsolutePath))
        .Where(request => request.Path.StartsWith("/api/", StringComparison.Ordinal));

    private void NoModelWrites() => RequestPaths().Should().NotContain(request =>
        request.Method != "GET" && request.Path.EndsWith("/features", StringComparison.Ordinal));

    private static string FrameResponse() => FrameResponse((1, 0, 0), (0, 1, 0), (0, 0, 1));

    private static string InvertedTopFrameResponse() => FrameResponse((1, 0, 0), (0, -1, 0), (0, 0, -1));

    private static string FrameResponse(
        (double X, double Y, double Z) x, (double X, double Y, double Z) y, (double X, double Y, double Z) normal)
    {
        object Value(string type, object value) => new Dictionary<string, object?> { ["btType"] = type, ["value"] = value };
        object Row(string name, (double X, double Y, double Z) vector) => Value("BTFSValueArray-1499", new[]
        {
            Value("BTFSValueString-1422", name), Value("BTFSValueNumber-772", vector.X),
            Value("BTFSValueNumber-772", vector.Y), Value("BTFSValueNumber-772", vector.Z),
        });
        var result = Value("BTFSValueArray-1499", new[]
        {
            Row("origin", (0, 0, 0)), Row("x", x), Row("y", y), Row("normal", normal),
        });
        return JsonSerializer.Serialize(new { btType = "BTFeatureScriptEvalResponse-1859", result });
    }
}
