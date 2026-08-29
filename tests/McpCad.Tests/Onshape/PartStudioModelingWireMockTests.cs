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
    public void WorkAxisSketch_PostsConstructionLineWithDisabledImprinting()
        {
            using var sketch = OnshapeSketchBuilder.BuildWorkAxis("Front", "x");
            var result = CreatePartStudioManager().SketchReplace(
                new CadElementAddress("doc-1", "ws-1", null, "e-part"), sketch.RootElement.GetRawText());

            result["success"].Should().Be(true);
            using var body = System.Text.Json.JsonDocument.Parse(_server.LogEntries.Single().RequestMessage.Body!);
            var feature = body.RootElement.GetProperty("feature");
            feature.GetProperty("parameters")[0].GetProperty("queries")[0].GetProperty("deterministicIds")[0].GetString().Should().Be("JCC");
            feature.GetProperty("parameters")[1].GetProperty("parameterId").GetString().Should().Be("disableImprinting");
            feature.GetProperty("parameters")[1].GetProperty("value").GetBoolean().Should().BeTrue();
            var axis = feature.GetProperty("entities");
            axis.GetArrayLength().Should().Be(1);
            axis = axis[0];
            axis.GetProperty("isConstruction").GetBoolean().Should().BeTrue();
            axis.GetProperty("geometry").GetProperty("dirX").GetDouble().Should().Be(1);
            axis.GetProperty("geometry").GetProperty("dirY").GetDouble().Should().Be(0);
        }

        [Fact]
        public void SketchReplace_PostsJsonBody_WithRejectMicroversionSkewOnWrite()
    {
        using var sketch = OnshapeSketchBuilder.BuildRectangle(0, 0, 0.1, 0.1);
        var partStudio = CreatePartStudioManager();
        var addr = new CadElementAddress("doc-1", "ws-1", null, "e-part");

        var result = partStudio.SketchReplace(addr, sketch.RootElement.GetRawText());

        result["success"].Should().Be(true);
        var entry = _server.LogEntries.Single(entry =>
            entry.RequestMessage.Method == "POST" &&
            entry.RequestMessage.AbsolutePath == "/api/v6/partstudios/d/doc-1/w/ws-1/e/e-part/features");
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
        var body = _server.LogEntries.Single(entry =>
                entry.RequestMessage.Method == "POST" &&
                entry.RequestMessage.AbsolutePath == "/api/v6/partstudios/d/doc-1/w/ws-1/e/e-part/features")
            .RequestMessage.Body;
        body.Should().NotBeNullOrWhiteSpace();
        body!.TrimStart().Should().StartWith("{\"feature\":");
        body.Should().Contain("rejectMicroversionSkew");
        body.TrimStart().Should().NotStartWith("{\"featureType\":");
    }

    [Fact]
    public void CircularPattern_FeatureAdd_PostsLiveVerifiedFeatureContract()
    {
        _server.ResetMappings();
        _server
            .Given(Request.Create().WithPath("/api/v6/partstudios/d/doc-1/w/ws-1/e/e-part/features").UsingPost())
            .RespondWith(Response.Create().WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody("""{"microversion":"mv-pattern","featureState":{"featureStatus":"OK"}}"""));

        using var envelope = OnshapeFeatureTemplates.CircularPatternFromFeature("extrude-1", "axis-sketch-1", 4, 180);
        var result = CreateFeatureManager().FeatureAdd(
            new CadElementAddress("doc-1", "ws-1", null, "e-part"), "circularPattern", null, envelope.RootElement.GetRawText());

        result["success"].Should().Be(true);
        using var body = System.Text.Json.JsonDocument.Parse(_server.LogEntries.Single(entry =>
            entry.RequestMessage.Method == "POST" &&
            entry.RequestMessage.AbsolutePath == "/api/v6/partstudios/d/doc-1/w/ws-1/e/e-part/features").RequestMessage.Body!);
        var parameters = body.RootElement.GetProperty("feature").GetProperty("parameters");
        body.RootElement.GetProperty("feature").GetProperty("featureType").GetString().Should().Be("circularPattern");
        parameters[4].GetProperty("featureIds")[0].GetString().Should().Be("extrude-1");
        parameters[5].GetProperty("queries")[0].GetProperty("queryString").GetString().Should()
            .Be("query = qCreatedBy(id + \"axis-sketch-1\" + \"wireOp\", EntityType.EDGE);");
        parameters[7].GetProperty("isInteger").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public void RectangularPattern_FeatureAdd_PostsLiveVerifiedFeatureContract()
        {
            _server.ResetMappings();
            _server
                .Given(Request.Create().WithPath("/api/v6/partstudios/d/doc-1/w/ws-1/e/e-part/features").UsingPost())
                .RespondWith(Response.Create().WithStatusCode(200)
                    .WithHeader("Content-Type", "application/json")
                    .WithBody("""{"microversion":"mv-pattern","featureState":{"featureStatus":"OK"}}"""));

            using var envelope = OnshapeFeatureTemplates.RectangularPatternFromFeature(
                "extrude-1", "direction-one-1", 0.03, 2, "direction-two-1", 0.03, 2);
            var result = CreateFeatureManager().FeatureAdd(
                new CadElementAddress("doc-1", "ws-1", null, "e-part"), "linearPattern", null, envelope.RootElement.GetRawText());

            result["success"].Should().Be(true);
            using var body = System.Text.Json.JsonDocument.Parse(_server.LogEntries.Single(entry =>
                entry.RequestMessage.Method == "POST" &&
                entry.RequestMessage.AbsolutePath == "/api/v6/partstudios/d/doc-1/w/ws-1/e/e-part/features").RequestMessage.Body!);
            var parameters = body.RootElement.GetProperty("feature").GetProperty("parameters");
            body.RootElement.GetProperty("feature").GetProperty("featureType").GetString().Should().Be("linearPattern");
            parameters[5].GetProperty("queries")[0].GetProperty("queryString").GetString().Should()
                .Be("query = qCreatedBy(id + \"direction-one-1\" + \"wireOp\", EntityType.EDGE);"); parameters.EnumerateArray().Select(parameter => parameter.GetProperty("parameterId").GetString()).Should().Equal("patternType", "operationType", "entities", "faces", "instanceFunction", "directionOne", "distance", "instanceCount", "oppositeDirection", "isCentered", "hasSecondDir", "directionTwo", "distanceTwo", "instanceCountTwo", "oppositeDirectionTwo", "isCenteredTwo", "defaultScope", "booleanScope", "fullFeaturePattern", "skipInstances", "skippedInstances"); parameters[4].GetProperty("featureIds")[0].GetString().Should().Be("extrude-1"); parameters[6].GetProperty("expression").GetString().Should().Be("0.03 m"); parameters[7].GetProperty("isInteger").GetBoolean().Should().BeTrue(); parameters[7].GetProperty("expression").GetString().Should().Be("2"); parameters[10].GetProperty("value").GetBoolean().Should().BeTrue(); parameters[12].GetProperty("expression").GetString().Should().Be("0.03 m"); parameters[13].GetProperty("isInteger").GetBoolean().Should().BeTrue(); parameters[13].GetProperty("expression").GetString().Should().Be("2");
            parameters[10].GetProperty("value").GetBoolean().Should().BeTrue();
            parameters[11].GetProperty("queries")[0].GetProperty("queryString").GetString().Should()
                .Be("query = qCreatedBy(id + \"direction-two-1\" + \"wireOp\", EntityType.EDGE);");
        }

        [Fact]
        public void MirrorFeature_FeatureAdd_PostsLiveVerifiedFeatureContract()
    {
        _server.ResetMappings();
        _server
            .Given(Request.Create().WithPath("/api/v6/partstudios/d/doc-1/w/ws-1/e/e-part/features").UsingPost())
            .RespondWith(Response.Create().WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody("""{"microversion":"mv-mirror","featureState":{"featureStatus":"OK"}}"""));

        using var envelope = OnshapeFeatureTemplates.MirrorFeature("extrude-1", "Front");
        var result = CreateFeatureManager().FeatureAdd(
            new CadElementAddress("doc-1", "ws-1", null, "e-part"), "mirror", null, envelope.RootElement.GetRawText());

        result["success"].Should().Be(true);
        using var body = System.Text.Json.JsonDocument.Parse(_server.LogEntries.Single(entry =>
            entry.RequestMessage.Method == "POST" &&
            entry.RequestMessage.AbsolutePath == "/api/v6/partstudios/d/doc-1/w/ws-1/e/e-part/features").RequestMessage.Body!);
        var parameters = body.RootElement.GetProperty("feature").GetProperty("parameters");
        body.RootElement.GetProperty("feature").GetProperty("featureType").GetString().Should().Be("mirror");
        parameters.EnumerateArray().Select(parameter => parameter.GetProperty("parameterId").GetString()).Should().Equal(
            "patternType", "operationType", "entities", "faces", "instanceFunction", "mirrorPlane",
            "defaultScope", "booleanScope", "fullFeaturePattern");
        parameters[0].GetProperty("enumName").GetString().Should().Be("MirrorType");
        parameters[0].GetProperty("value").GetString().Should().Be("FEATURE");
        parameters[4].GetProperty("featureIds")[0].GetString().Should().Be("extrude-1");
        parameters[5].GetProperty("queries")[0].GetProperty("deterministicIds")[0].GetString().Should().Be("JCC");
    }

    [Fact]
    public void OffsetPlane_FeatureAdd_PostsLiveProvenFlatContract()
    {
        _server.ResetMappings();
        _server
            .Given(Request.Create().WithPath("/api/v6/partstudios/d/doc-1/w/ws-1/e/e-part/features").UsingPost())
            .RespondWith(Response.Create().WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody("""{"microversion":"mv-plane","featureState":{"featureStatus":"OK"}}"""));

        using var envelope = OnshapeFeatureTemplates.OffsetPlane("Top", 0.01);
        var result = CreateFeatureManager().FeatureAdd(
            new CadElementAddress("doc-1", "ws-1", null, "e-part"), "cPlane", null, envelope.RootElement.GetRawText());

        result["success"].Should().Be(true);
        using var body = System.Text.Json.JsonDocument.Parse(_server.LogEntries.Single(entry =>
            entry.RequestMessage.Method == "POST" &&
            entry.RequestMessage.AbsolutePath == "/api/v6/partstudios/d/doc-1/w/ws-1/e/e-part/features").RequestMessage.Body!);
        var parameters = body.RootElement.GetProperty("feature").GetProperty("parameters");
        body.RootElement.GetProperty("feature").GetProperty("featureType").GetString().Should().Be("cPlane");
        parameters.EnumerateArray().Select(parameter => parameter.GetProperty("parameterId").GetString()).Should().Equal(
            "entities", "cplaneType", "offset", "angle", "oppositeDirection", "flipAlignment", "flipNormal", "width", "height");
        parameters[0].GetProperty("queries")[0].GetProperty("deterministicIds")[0].GetString().Should().Be("JDC");
        parameters[2].GetProperty("expression").GetString().Should().Be("0.01 m");
        parameters[4].GetProperty("value").GetBoolean().Should().BeFalse();
        parameters[5].GetProperty("value").GetBoolean().Should().BeFalse();
        parameters[6].GetProperty("value").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public void OffsetPlane_FeatureAdd_WithErrorFeatureStatus_ThrowsProviderException()
    {
        _server.ResetMappings();
        _server
            .Given(Request.Create().WithPath("/api/v6/partstudios/d/doc-1/w/ws-1/e/e-part/features").UsingPost())
            .RespondWith(Response.Create().WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody("""{"featureState":{"featureStatus":"ERROR","errorString":"Invalid plane"}}"""));

        using var envelope = OnshapeFeatureTemplates.OffsetPlane("Top", 0.01);
        var act = () => CreateFeatureManager().FeatureAdd(
            new CadElementAddress("doc-1", "ws-1", null, "e-part"), "cPlane", null, envelope.RootElement.GetRawText());

        act.Should().Throw<CadProviderOperationException>()
            .WithMessage("*status ERROR: Invalid plane");
    }

    [Fact]
    public void Shell_FeatureAdd_PostsLiveProvenFlatContractWithOrderedFaceIds()
    {
        _server.ResetMappings();
        _server
            .Given(Request.Create().WithPath("/api/v6/partstudios/d/doc-1/w/ws-1/e/e-part/features").UsingPost())
            .RespondWith(Response.Create().WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody("""{"microversion":"mv-shell","featureState":{"featureStatus":"OK"}}"""));

        using var envelope = OnshapeFeatureTemplates.Shell(["JHK", "JHL"], 0.001, oppositeDirection: true);
        var result = CreateFeatureManager().FeatureAdd(
            new CadElementAddress("doc-1", "ws-1", null, "e-part"), "shell", null, envelope.RootElement.GetRawText());

        result["success"].Should().Be(true);
        using var body = System.Text.Json.JsonDocument.Parse(_server.LogEntries.Single(entry =>
            entry.RequestMessage.Method == "POST" &&
            entry.RequestMessage.AbsolutePath == "/api/v6/partstudios/d/doc-1/w/ws-1/e/e-part/features").RequestMessage.Body!);
        var parameters = body.RootElement.GetProperty("feature").GetProperty("parameters");
        body.RootElement.GetProperty("feature").GetProperty("featureType").GetString().Should().Be("shell");
        parameters.EnumerateArray().Select(parameter => parameter.GetProperty("parameterId").GetString()).Should().Equal(
            "isHollow", "entities", "parts", "thickness", "oppositeDirection");
        parameters[1].GetProperty("queries")[0].GetProperty("deterministicIds").EnumerateArray()
            .Select(id => id.GetString()).Should().Equal("JHK", "JHL");
        parameters[3].GetProperty("expression").GetString().Should().Be("0.001 m");
        parameters[4].GetProperty("value").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public void Shell_FeatureAdd_WithErrorFeatureStatus_ThrowsProviderException()
    {
        _server.ResetMappings();
        _server
            .Given(Request.Create().WithPath("/api/v6/partstudios/d/doc-1/w/ws-1/e/e-part/features").UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody("""{"featureState":{"featureStatus":"ERROR","errorString":"Invalid shell"}}"""));

        using var envelope = OnshapeFeatureTemplates.Shell(["JHK"], 0.001);
        var act = () => CreateFeatureManager().FeatureAdd(
            new CadElementAddress("doc-1", "ws-1", null, "e-part"), "shell", null, envelope.RootElement.GetRawText());

        act.Should().Throw<CadProviderOperationException>()
            .WithMessage("*status ERROR: Invalid shell");
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
        var body = _server.LogEntries.Single(entry =>
                entry.RequestMessage.Method == "POST" &&
                entry.RequestMessage.AbsolutePath == "/api/v6/partstudios/d/doc-1/w/ws-1/e/e-part/features")
            .RequestMessage.Body!;
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
