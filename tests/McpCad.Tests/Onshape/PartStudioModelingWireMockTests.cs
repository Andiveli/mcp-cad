using McpCad.Core.Exceptions;
using McpCad.Core.Models;
using McpCad.Onshape;
using McpCad.Onshape.Builders;
using McpCad.Onshape.Managers;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace McpCad.Tests.Onshape;

public class PartStudioModelingWireMockTests : IDisposable
{
    private readonly WireMockServer _server;

    public PartStudioModelingWireMockTests()
    {
        _server = WireMockServer.Start();
        _server
            .Given(Request.Create().WithPath("/api/v6/partstudios/d/doc-1/w/ws-1/e/e-part/features").UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody("""{"featureId":"sketch-1","microversion":"mv-sketch"}"""));
    }

    public void Dispose() => _server.Dispose();

    [Fact]
    public void SketchReplace_PostsJsonBody_WithRejectMicroversionSkewOnWrite()
    {
        using var sketch = OnshapeSketchBuilder.BuildRectangle(0, 0, 0.1, 0.1);
        var partStudio = CreatePartStudioManager();
        var addr = new CadElementAddress("doc-1", "ws-1", null, "e-part");

        var result = partStudio.SketchReplace(addr, sketch.RootElement.GetRawText());

        result["success"].Should().Be(true);
        var entry = _server.LogEntries.Last();
        entry.RequestMessage.Body.Should().Contain("entities");
        entry.RequestMessage.RawQuery.Should().Contain("rejectMicroversionSkew=true");
    }

    [Fact]
    public void FeatureAdd_FlatPayload_RegressionGuard_UsesFeatureEnvelope()
    {
        _server.ResetMappings();
        _server
            .Given(Request.Create().WithPath("/api/v6/partstudios/d/doc-1/w/ws-1/e/e-part/features").UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody("""{"microversion":"mv-extrude"}"""));

        var features = CreateFeatureManager();
        var addr = new CadElementAddress("doc-1", "ws-1", null, "e-part");
        using var envelope = OnshapeFeatureTemplates.ExtrudeFromSketch("sketch-1", 0.02);
        var result = features.FeatureAdd(addr, "extrude", null, envelope.RootElement.GetRawText());

        result["success"].Should().Be(true);
        var body = _server.LogEntries.Last().RequestMessage.Body;
        body.Should().NotBeNullOrWhiteSpace();
        body!.TrimStart().Should().StartWith("{\"feature\":");
        body.Should().Contain("rejectMicroversionSkew");
        body.TrimStart().Should().NotStartWith("{\"featureType\":");
    }

    [Fact]
    public void SketchReplace_With409_ThrowsOnshapeMicroversionSkewException()
    {
        _server.ResetMappings();
        _server
            .Given(Request.Create().WithPath("/api/v6/partstudios/d/doc-1/w/ws-1/e/e-part/features").UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(409)
                .WithBody("""{"message":"wrongMicroversion","microversion":"mv-server-42"}"""));

        var partStudio = CreatePartStudioManager();
        var addr = new CadElementAddress("doc-1", "ws-1", "stale", "e-part");

        var act = () => partStudio.SketchReplace(addr, """{"entities":[],"constraints":[]}""");

        act.Should().Throw<OnshapeMicroversionSkewException>()
            .Which.ServerMicroversion.Should().Be("mv-server-42");
    }

    [Theory]
    [InlineData("revolve")]
    [InlineData("fillet")]
    [InlineData("chamfer")]
    [InlineData("hole")]
    public void P1FeatureEnvelope_PostsWrappedFeatureShape(string featureType)
    {
        _server.ResetMappings();
        _server
            .Given(Request.Create().WithPath("/api/v6/partstudios/d/doc-1/w/ws-1/e/e-part/features").UsingPost())
            .RespondWith(Response.Create().WithStatusCode(200).WithBody("""{"microversion":"mv-p1"}"""));

        using var envelope = featureType switch
        {
            "revolve" => OnshapeFeatureTemplates.RevolveFromSketch("sketch-1", 180),
            "fillet" => OnshapeFeatureTemplates.Fillet(["edge-1"], 0.005),
            "chamfer" => OnshapeFeatureTemplates.Chamfer(["edge-1"], 0.003),
            "hole" => OnshapeFeatureTemplates.Hole("vertex-1", 0.006, 0.02),
            _ => throw new ArgumentOutOfRangeException(nameof(featureType)),
        };
        var result = CreateFeatureManager().FeatureAdd(
            new CadElementAddress("doc-1", "ws-1", null, "e-part"), featureType, null, envelope.RootElement.GetRawText());

        result["success"].Should().Be(true);
        var body = _server.LogEntries.Last().RequestMessage.Body!;
        body.Should().Contain($"\"featureType\":\"{featureType}\"")
            .And.Contain("\"rejectMicroversionSkew\":true");
    }

    private (OnshapeClient Client, OnshapeDriver Driver) CreateStack()
    {
        var http = new HttpClient { BaseAddress = new Uri(_server.Url!) };
        var client = new OnshapeClient(http, "ak", "sk", _server.Url);
        var driver = new OnshapeDriver(client) { DefaultWvm = "w" };
        return (client, driver);
    }

    private PartStudioManager CreatePartStudioManager()
    {
        var (client, driver) = CreateStack();
        return new PartStudioManager(client, driver);
    }

    private FeatureManager CreateFeatureManager()
    {
        var (client, driver) = CreateStack();
        return new FeatureManager(client, driver);
    }
}
