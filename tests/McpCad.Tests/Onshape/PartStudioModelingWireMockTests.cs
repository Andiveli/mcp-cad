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
    public void ElementGet_UsesDocumentElementsListing_AndSelectsExactElementId()
    {
        _server.ResetMappings();
        _server
            .Given(Request.Create().WithPath("/api/v6/elements/d/doc-1/w/ws-1/e/e-part").UsingGet())
            .RespondWith(Response.Create().WithStatusCode(405));
        _server
            .Given(Request.Create().WithPath("/api/v6/documents/d/doc-1/w/ws-1/elements").UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody("""[{"id":"e-other","name":"Other"},{"id":"e-part","name":"Part Studio","microversionId":"mv-2"}]"""));

        var result = CreatePartStudioManager().ElementGet(
            new CadElementAddress("doc-1", "ws-1", null, "e-part"));

        result["success"].Should().Be(true);
        result["id"].Should().Be("e-part");
        result["name"].Should().Be("Part Studio");
        _server.LogEntries.Should().ContainSingle();
        var request = _server.LogEntries.Single().RequestMessage;
        request.AbsolutePath.Should().Be("/api/v6/documents/d/doc-1/w/ws-1/elements");
        request.RawQuery.Should().Contain("elementId=e-part");
    }

    [Fact]
    public void ElementGet_WhenExactElementIsMissing_ThrowsProviderException()
    {
        _server.ResetMappings();
        _server
            .Given(Request.Create().WithPath("/api/v6/documents/d/doc-1/w/ws-1/elements").UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody("""[{"id":"e-other","name":"Other"}]"""));

        var act = () => CreatePartStudioManager().ElementGet(
            new CadElementAddress("doc-1", "ws-1", null, "e-missing"));

        act.Should().Throw<CadProviderOperationException>()
            .WithMessage("*e-missing*not found*");
        _server.LogEntries.Should().ContainSingle();
        _server.LogEntries.Single().RequestMessage.RawQuery.Should().Contain("elementId=e-missing");
    }

    [Fact]
    public void ElementGet_UsesVersionAddressing_WhenDriverDefaultIsVersion()
    {
        _server.ResetMappings();
        _server
            .Given(Request.Create().WithPath("/api/v6/documents/d/doc-1/v/version-1/elements").UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody("""[{"id":"e-part","name":"Versioned Part Studio"}]"""));
        var (client, driver) = CreateStack();
        driver.DefaultWvm = "v";

        var result = new PartStudioManager(client, driver).ElementGet(
            new CadElementAddress("doc-1", "version-1", null, "e-part"));

        result["success"].Should().Be(true);
        result["id"].Should().Be("e-part");
        _server.LogEntries.Should().ContainSingle();
        _server.LogEntries.Single().RequestMessage.AbsolutePath
            .Should().Be("/api/v6/documents/d/doc-1/v/version-1/elements");
    }

    [Fact]
    public void ElementCreate_PostsPartStudioEndpoint_WithNameOnly()
    {
        _server.ResetMappings();
        _server
            .Given(Request.Create().WithPath("/api/v6/partstudios/d/doc-1/w/ws-1").UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody("""{"id":"ps-1","name":"Showcase / Δ","elementType":"PARTSTUDIO","microversionId":"mv-1"}"""));

        var result = CreatePartStudioManager().ElementCreate(
            new CadElementAddress("doc-1", "ws-1", null, "ignored-for-create"),
            "Showcase / Δ",
            "PARTSTUDIO");

        result["success"].Should().Be(true);
        var request = _server.LogEntries.Single().RequestMessage;
        request.AbsolutePath.Should().Be("/api/v6/partstudios/d/doc-1/w/ws-1");
        request.Method.Should().Be("POST");
        request.RawQuery.Should().Contain("rejectMicroversionSkew=true");

        using var body = System.Text.Json.JsonDocument.Parse(request.Body!);
        body.RootElement.GetProperty("name").GetString().Should().Be("Showcase / Δ");
        body.RootElement.TryGetProperty("elementType", out _).Should().BeFalse();
    }

    [Fact]
    public void ElementCreate_RejectsElementTypesOtherThanPartStudio()
    {
        var act = () => CreatePartStudioManager().ElementCreate(
            new CadElementAddress("doc-1", "ws-1", null, "ignored-for-create"),
            "Assembly-like name",
            "ASSEMBLY");

        act.Should().Throw<ArgumentException>()
            .WithMessage("*only supports elementType 'PARTSTUDIO'*");
        _server.LogEntries.Should().BeEmpty();
    }

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
                .WithBody("""{"microversion":"mv-extrude","featureState":{"featureStatus":"OK"}}"""));

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
    public void FeatureAdd_WithErrorFeatureStatus_ThrowsProviderException()
    {
        _server.ResetMappings();
        _server
            .Given(Request.Create().WithPath("/api/v6/partstudios/d/doc-1/w/ws-1/e/e-part/features").UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody("""{"featureState":{"featureStatus":"ERROR","errorString":"Invalid profile"}}"""));

        var act = () => CreateFeatureManager().FeatureAdd(
            new CadElementAddress("doc-1", "ws-1", null, "e-part"), "extrude");

        act.Should().Throw<CadProviderOperationException>()
            .WithMessage("*status ERROR: Invalid profile");
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

    [Fact]
    public void SketchReplace_WithErrorFeatureStatus_ThrowsProviderException()
    {
        _server.ResetMappings();
        _server
            .Given(Request.Create().WithPath("/api/v6/partstudios/d/doc-1/w/ws-1/e/e-part/features").UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody("""{"featureState":{"featureStatus":"ERROR","errorString":"Invalid sketch"}}"""));

        var act = () => CreatePartStudioManager().SketchReplace(
            new CadElementAddress("doc-1", "ws-1", null, "e-part"), """{"entities":[]}""");

        act.Should().Throw<CadProviderOperationException>()
            .WithMessage("*status ERROR: Invalid sketch");
    }

    [Theory]
    [InlineData("revolve")]
    [InlineData("fillet")]
    [InlineData("chamfer")]
    public void P1FeatureEnvelope_PostsWrappedFeatureShape(string featureType)
    {
        _server.ResetMappings();
        _server
            .Given(Request.Create().WithPath("/api/v6/partstudios/d/doc-1/w/ws-1/e/e-part/features").UsingPost())
            .RespondWith(Response.Create().WithStatusCode(200).WithBody("""{"microversion":"mv-p1"}"""));

        using var envelope = featureType switch
        {
            "revolve" => OnshapeFeatureTemplates.RevolveFromSketch("sketch-1", "axis-1", 180),
            "fillet" => OnshapeFeatureTemplates.Fillet(["edge-1"], 0.005),
            "chamfer" => OnshapeFeatureTemplates.Chamfer(["edge-1"], 0.003),
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
