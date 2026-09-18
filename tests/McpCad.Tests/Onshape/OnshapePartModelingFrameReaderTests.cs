using System.Net;
using System.Text.Json;
using McpCad.Core.Modeling;
using McpCad.Core.Models;
using McpCad.Onshape;
using McpCad.Onshape.Modeling;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace McpCad.Tests.Onshape;

public sealed class OnshapePartModelingFrameReaderTests : IDisposable
{
    private readonly WireMockServer _server = WireMockServer.Start();

    public void Dispose() => _server.Dispose();

    [Theory]
    [InlineData("Top", false, 1, 0, 0, 0, 1, 0, 0, 0, 1)]
    [InlineData("Front", false, 1, 0, 0, 0, 0, 1, 0, -1, 0)]
    [InlineData("Right", false, 0, 1, 0, 0, 0, 1, 1, 0, 0)]
    [InlineData("Top", true, 1, 0, 0, 0, 1, 0, 0, 0, 1)]
    [InlineData("Front", true, 1, 0, 0, 0, 0, 1, 0, -1, 0)]
    [InlineData("Right", true, 0, 1, 0, 0, 0, 1, 1, 0, 0)]
    public async Task ReadAsync_ParsesFramesAndPostsCanonicalReadOnlyEvaluatorScript(
        string planeName, bool legacy, double xx, double xy, double xz,
        double yx, double yy, double yz, double nx, double ny, double nz)
    {
        Stub(FrameResponse(legacy, (xx, xy, xz), (yx, yy, yz), (nx, ny, nz)));

        var frame = await CreateReader().ReadAsync(Plane(planeName), CancellationToken.None);

        frame.Origin.Should().Be(new OnshapeFrameVector(0, 0, 0));
        frame.XAxis.Should().Be(new OnshapeFrameVector(xx, xy, xz));
        frame.YAxis.Should().Be(new OnshapeFrameVector(yx, yy, yz));
        frame.Normal.Should().Be(new OnshapeFrameVector(nx, ny, nz));
        _server.LogEntries.Should().HaveCount(2);
        _server.LogEntries.Select(entry => entry.RequestMessage.Method).Should().Equal("GET", "POST");
        NoModelWrites();

        var evaluation = _server.LogEntries.Single(entry => entry.RequestMessage.Method == "POST");
        evaluation.RequestMessage.AbsolutePath.Should().Be("/api/v17/partstudios/d/test-document/w/test-workspace/e/test-element/featurescript");
        evaluation.RequestMessage.RawQuery.Should().Be("?rejectMicroversionSkew=true&rollbackBarIndex=-1");
        using var request = JsonDocument.Parse(evaluation.RequestMessage.Body!);
        request.RootElement.GetProperty("btType").GetString().Should().Be("BTFeatureScriptEvalCall-2377");
        request.RootElement.GetProperty("libraryVersion").GetInt32().Should().Be(4012);
        var script = request.RootElement.GetProperty("script").GetString();
        script.Should().Contain($"qCreatedBy(makeId(\"{planeName}\"), EntityType.FACE)");
        script.Should().Contain("const y = cross(plane.normal, plane.x);");
        request.RootElement.TryGetProperty("queries", out _).Should().BeFalse();
        script.Should().Contain("function(context is Context, queries)");
        script.Should().Contain("alignCanonically(context, evPlane(context,");
        script.Should().NotContain("JDC");
        script.Should().NotContain("JCC");
        script.Should().NotContain("JEC");
        script.Should().NotContain("plane.y");
    }

    [Theory]
    [InlineData("notices")]
    [InlineData("microversionSkew")]
    [InlineData("unsupported")]
    [InlineData("numeric-string")]
    [InlineData("bool")]
    [InlineData("nonfinite")]
    [InlineData("duplicate")]
    [InlineData("missing")]
    [InlineData("skew-basis")]
    public async Task ReadAsync_RejectsUnsafeOrMalformedEvaluatorResponses(string invalid)
    {
        Stub(FrameResponse(false, (1, 0, 0), (0, 1, 0), (0, 0, 1), invalid));

        var act = () => CreateReader().ReadAsync(Plane("Top"), CancellationToken.None);

        var error = (await act.Should().ThrowAsync<OnshapePartModelingFrameReadException>()).Which;
        error.Failure.Should().BeOneOf(
            OnshapePartModelingFrameReadFailure.Notice,
            OnshapePartModelingFrameReadFailure.MicroversionSkew,
            OnshapePartModelingFrameReadFailure.UnsupportedValue,
            OnshapePartModelingFrameReadFailure.MalformedFrame);
        _server.LogEntries.Should().HaveCount(2);
        NoModelWrites();
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("[]")]
    [InlineData("{\"libraryVersion\":\"4012\"}")]
    [InlineData("{\"libraryVersion\":0}")]
    [InlineData("{\"libraryVersion\":4012,\"microversionSkew\":true}")]
    public async Task ReadAsync_RejectsInvalidFeaturesResponseBeforeEvaluation(string featuresResponse)
    {
        StubFeatures(featuresResponse);

        var act = () => CreateReader().ReadAsync(Plane("Top"), CancellationToken.None);

        (await act.Should().ThrowAsync<OnshapePartModelingFrameReadException>()).Which.Failure
            .Should().BeOneOf(OnshapePartModelingFrameReadFailure.MalformedFrame, OnshapePartModelingFrameReadFailure.MicroversionSkew);
        _server.LogEntries.Should().ContainSingle(entry => entry.RequestMessage.Method == "GET");
    }

    [Fact]
    public async Task ReadAsync_RejectsInvalidPlaneAndTargetBeforeTransport()
    {
        var invalidPlane = () => CreateReader().ReadAsync((CanonicalPlane)99, CancellationToken.None);
        var invalidTarget = () => CreateReader(new CadElementAddress("", "test-workspace", null, "test-element"))
            .ReadAsync(Plane("Top"), CancellationToken.None);

        await invalidPlane.Should().ThrowAsync<OnshapePartModelingFrameReadException>();
        await invalidTarget.Should().ThrowAsync<OnshapePartModelingFrameReadException>();
        _server.LogEntries.Should().BeEmpty();
    }

    [Fact]
    public async Task ReadAsync_HonorsPreCanceledRequestWithoutTransport()
    {
        using var canceled = new CancellationTokenSource();
        canceled.Cancel();

        var act = () => CreateReader().ReadAsync(Plane("Top"), canceled.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
        _server.LogEntries.Should().BeEmpty();
    }

    [Fact]
    public async Task ReadAsync_HonorsCancellationDuringFeaturesRequest()
    {
        var handlerStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _server.Given(Request.Create().WithPath("/api/v6/partstudios/d/test-document/w/test-workspace/e/test-element/features").UsingGet())
            .RespondWith(Response.Create().WithCallback(_ =>
            {
                handlerStarted.TrySetResult();
                Thread.Sleep(500);
                return new WireMock.ResponseMessage { StatusCode = 200, BodyData = new WireMock.Util.BodyData { BodyAsString = "{\"libraryVersion\":4012}" } };
            }));
        using var canceled = new CancellationTokenSource();
        var pending = CreateReader().ReadAsync(Plane("Top"), canceled.Token);
        await handlerStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        canceled.Cancel();

        Func<Task> act = () => pending;
        await act.Should().ThrowAsync<OperationCanceledException>();
        NoModelWrites();
    }

    private OnshapePartModelingFrameReader CreateReader(CadElementAddress? target = null) => new(
        new OnshapeClient(new HttpClient(), "sanitized-key", "sanitized-secret", _server.Url),
        target ?? new CadElementAddress("test-document", "test-workspace", null, "test-element"));

    private void Stub(string evaluationResponse)
    {
        StubFeatures("{\"libraryVersion\":4012}");
        _server.Given(Request.Create().WithPath("/api/v17/partstudios/d/test-document/w/test-workspace/e/test-element/featurescript").UsingPost())
            .RespondWith(Response.Create().WithStatusCode(200).WithHeader("Content-Type", "application/json").WithBody(evaluationResponse));
    }

    private void StubFeatures(string response) => _server
        .Given(Request.Create().WithPath("/api/v6/partstudios/d/test-document/w/test-workspace/e/test-element/features").UsingGet())
        .RespondWith(Response.Create().WithStatusCode(200).WithBody(response));

    private void NoModelWrites() => _server.LogEntries.Should().NotContain(entry =>
        entry.RequestMessage.Method != "GET" && entry.RequestMessage.AbsolutePath.EndsWith("/features", StringComparison.Ordinal));

    private static CanonicalPlane Plane(string name) => name switch
    {
        "Top" => CanonicalPlane.Top,
        "Front" => CanonicalPlane.Front,
        "Right" => CanonicalPlane.Right,
        _ => throw new ArgumentOutOfRangeException(nameof(name)),
    };

    private static string FrameResponse(bool legacy, (double X, double Y, double Z) x, (double X, double Y, double Z) y,
        (double X, double Y, double Z) normal, string? invalid = null)
    {
        string Type(string type) => legacy
            ? "com.belmonttech.serialize.fsvalue.BTFSValue" + type
            : type switch
            {
                "Array" => "BTFSValueArray-1499",
                "Number" => "BTFSValueNumber-772",
                "String" => "BTFSValueString-1422",
                "Boolean" => "BTFSValueBoolean-1413",
                _ => "BTFSValueOther",
            };
        object Value(string type, object value) => new Dictionary<string, object?> { ["btType"] = Type(type), ["value"] = value };
        object Row(string name, (double X, double Y, double Z) vector) => Value("Array", new[]
        {
            Value("String", name), Value("Number", vector.X), Value("Number", vector.Y), Value("Number", vector.Z),
        });
        var rows = new List<object> { Row("origin", (0, 0, 0)), Row("x", x), Row("y", y), Row("normal", normal) };
        if (invalid == "duplicate") rows[3] = Row("x", normal);
        if (invalid == "missing") rows.RemoveAt(3);
        if (invalid == "skew-basis") rows[2] = Row("y", (1, 0, 0));
        if (invalid == "unsupported") rows[1] = Value("Other", "x");
        if (invalid == "numeric-string") rows[1] = Value("Array", new[] { Value("String", "x"), Value("String", "1"), Value("Number", 0), Value("Number", 0) });
        if (invalid == "bool") rows[1] = Value("Array", new[] { Value("String", "x"), Value("Number", 1), Value("Number", 0), Value("Boolean", false) });
        if (invalid == "nonfinite") rows[1] = Value("Array", new[] { Value("String", "x"), Value("Number", OverflowNumber()), Value("Number", 0), Value("Number", 0) });
        var result = Value("Array", rows);
        var envelope = new Dictionary<string, object?> { ["btType"] = "BTFeatureScriptEvalResponse-1859", ["result"] = result };
        if (invalid == "notices") envelope["notices"] = new[] { new { message = "sanitized" } };
        if (invalid == "microversionSkew") envelope["microversionSkew"] = true;
        return JsonSerializer.Serialize(envelope);
    }

    private static JsonElement OverflowNumber()
    {
        using var value = JsonDocument.Parse("1e9999");
        return value.RootElement.Clone();
    }
}
