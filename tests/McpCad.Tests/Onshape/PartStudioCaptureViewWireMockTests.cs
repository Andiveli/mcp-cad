using System.Globalization;
using FluentAssertions;
using McpCad.Core.Exceptions;
using McpCad.Core.Models;
using McpCad.Onshape;
using McpCad.Onshape.Managers;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace McpCad.Tests.Onshape;

public sealed class PartStudioCaptureViewWireMockTests : IDisposable
{
    private const string MinimalPngBase64 = "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVQIHWP4z8DwHwAFgAI/ScL1ZQAAAABJRU5ErkJggg==";
    private readonly WireMockServer _server = WireMockServer.Start();

    public void Dispose() => _server.Dispose();

    [Theory]
    [InlineData("iso")]
    [InlineData("top")]
    public void CaptureView_GetsV16ShadedViewsWithExactViewAndRenderingQuery(string view)
    {
        _server.Given(Request.Create()
                .WithPath("/api/v16/partstudios/d/doc-1/w/ws-1/e/e-part/shadedviews")
                .UsingGet())
            .RespondWith(Response.Create().WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody("{\"images\":[[\"" + MinimalPngBase64 + "\"]]}"));

        var result = CreateManager().CaptureView(new CadElementAddress("doc-1", "ws-1", null, "e-part"), view, 640, 480, "hide");

        result["success"].Should().Be(true);
        result["image_base64"].Should().Be(MinimalPngBase64);
        result["mime_type"].Should().Be("image/png");
        var request = _server.LogEntries.Last(entry =>
            entry.RequestMessage.AbsolutePath == "/api/v16/partstudios/d/doc-1/w/ws-1/e/e-part/shadedviews").RequestMessage;
        request.AbsolutePath.Should().Be("/api/v16/partstudios/d/doc-1/w/ws-1/e/e-part/shadedviews");
        if (view == "top")
        {
            request.RawQuery.Should().Contain("viewMatrix=top");
        }
        else
        {
            request.RawQuery.Should().Contain($"viewMatrix={Uri.EscapeDataString(CanonicalIsoMatrix)}");
        }

        request.RawQuery.Should().Contain("outputWidth=640")
            .And.Contain("outputHeight=480")
            .And.Contain("pixelSize=0")
            .And.Contain("edges=hide")
            .And.Contain("showAllParts=true")
            .And.Contain("includeSurfaces=true")
            .And.Contain("useAntiAliasing=true")
            .And.Contain("includeWires=false");
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"images\":[]}")]
    [InlineData("{\"images\":[[]]}")]
    [InlineData("{\"images\":[[\"\"]]}")]
    [InlineData("{\"images\":[[\"not base64\"]]}")]
    [InlineData("{\"images\":[[\"aGVsbG8=\"]]}")]
    [InlineData("{\"images\":[\"not base64\"]}")]
    [InlineData("{\"images\":[\"aGVsbG8=\"]}")]
    [InlineData("{\"images\":[\"not-a-slice\"]}")]
    public void CaptureView_WhenNoValidPngExists_ThrowsClearProviderError(string response)
    {
        _server.Given(Request.Create().WithPath("/api/v16/partstudios/d/doc-1/w/ws-1/e/e-part/shadedviews").UsingGet())
            .RespondWith(Response.Create().WithStatusCode(200).WithBody(response));

        var action = () => CreateManager().CaptureView(new CadElementAddress("doc-1", "ws-1", null, "e-part"), "iso", 800, 600, "show");

        action.Should().Throw<CadProviderOperationException>().WithMessage("*image*");
    }

    [Fact]
    public void CaptureView_AcceptsLiveFlatImagesPayload()
    {
        _server.Given(Request.Create().WithPath("/api/v16/partstudios/d/doc-1/w/ws-1/e/e-part/shadedviews").UsingGet())
            .RespondWith(Response.Create().WithStatusCode(200)
                .WithBody("{\"images\":[\"" + MinimalPngBase64 + "\"]}"));

        var result = CreateManager().CaptureView(new CadElementAddress("doc-1", "ws-1", null, "e-part"), "iso", 800, 600, "show");

        result["image_base64"].Should().Be(MinimalPngBase64);
    }

    [Fact]
    public void CaptureView_UsesFirstValidImageAcrossFlatPayload()
    {
        _server.Given(Request.Create().WithPath("/api/v16/partstudios/d/doc-1/w/ws-1/e/e-part/shadedviews").UsingGet())
            .RespondWith(Response.Create().WithStatusCode(200)
                .WithBody("{\"images\":[\"not base64\",\"aGVsbG8=\",\"" + MinimalPngBase64 + "\"]}"));

        var result = CreateManager().CaptureView(new CadElementAddress("doc-1", "ws-1", null, "e-part"), "iso", 800, 600, "show");

        result["image_base64"].Should().Be(MinimalPngBase64);
    }

    [Fact]
    public void CaptureView_UsesFirstValidImageAcrossAllRows()
    {
        _server.Given(Request.Create().WithPath("/api/v16/partstudios/d/doc-1/w/ws-1/e/e-part/shadedviews").UsingGet())
            .RespondWith(Response.Create().WithStatusCode(200)
                .WithBody("{\"images\":[[\"\"],[\"" + MinimalPngBase64 + "\"]]}"));

        var result = CreateManager().CaptureView(new CadElementAddress("doc-1", "ws-1", null, "e-part"), "iso", 800, 600, "show");

        result["image_base64"].Should().Be(MinimalPngBase64);
    }

    [Fact]
    public void CaptureView_IsoMatrixIsOrthonormalAndRightHanded()
    {
        _server.Given(Request.Create().WithPath("/api/v16/partstudios/d/doc-1/w/ws-1/e/e-part/shadedviews").UsingGet())
            .RespondWith(Response.Create().WithStatusCode(200)
                .WithBody("{\"images\":[[\"" + MinimalPngBase64 + "\"]]}"));

        _ = CreateManager().CaptureView(new CadElementAddress("doc-1", "ws-1", null, "e-part"), "iso", 800, 600, "show");

        var query = _server.LogEntries.Last().RequestMessage.RawQuery;
        var matrixText = Uri.UnescapeDataString(query.Split('&').Single(value => value.StartsWith("viewMatrix=")).Split('=')[1]);
        var values = matrixText.Split(',').Select(value => double.Parse(value, CultureInfo.InvariantCulture)).ToArray();
        var matrix = new[,] { { values[0], values[1], values[2] }, { values[4], values[5], values[6] }, { values[8], values[9], values[10] } };

        for (var index = 0; index < 3; index++)
        {
            Enumerable.Range(0, 3).Sum(other => matrix[index, other] * matrix[index, other]).Should().BeApproximately(1, 1e-12);
            Enumerable.Range(0, 3).Sum(other => matrix[other, index] * matrix[other, index]).Should().BeApproximately(1, 1e-12);
        }

        Enumerable.Range(0, 3).SelectMany(row => Enumerable.Range(row + 1, 2 - row), (row, other) => (row, other)).ToList()
            .ForEach(pair => Enumerable.Range(0, 3).Sum(index => matrix[pair.row, index] * matrix[pair.other, index]).Should().BeApproximately(0, 1e-12));
        var determinant = matrix[0, 0] * (matrix[1, 1] * matrix[2, 2] - matrix[1, 2] * matrix[2, 1])
            - matrix[0, 1] * (matrix[1, 0] * matrix[2, 2] - matrix[1, 2] * matrix[2, 0])
            + matrix[0, 2] * (matrix[1, 0] * matrix[2, 1] - matrix[1, 1] * matrix[2, 0]);
        determinant.Should().BeApproximately(1, 1e-12);
    }

    private PartStudioManager CreateManager()
    {
        var http = new HttpClient { BaseAddress = new Uri(_server.Url!) };
        var client = new OnshapeClient(http, "ak", "sk", _server.Url);
        return new PartStudioManager(client, new OnshapeDriver(client) { DefaultWvm = "w" });
    }

    private const string CanonicalIsoMatrix = "0.7071067811865476,-0.4082482904638631,0.5773502691896258,0,0,0.8164965809277261,0.5773502691896258,0,-0.7071067811865476,-0.4082482904638631,0.5773502691896258,0";
}
