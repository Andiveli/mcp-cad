using System.Reflection;
using McpCad.Core;
using McpCad.Core.Exceptions;
using McpCad.Core.Models;
using McpCad.Onshape;
using McpCad.Onshape.Managers;
using McpCad.Tests.Mocks;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace McpCad.Tests.Onshape;

public class OnshapeProviderConformanceTests : IDisposable
{
    private readonly WireMockServer _server;

    public OnshapeProviderConformanceTests()
    {
        _server = WireMockServer.Start();
        RegisterDefaultStubs();
    }

    public void Dispose() => _server.Dispose();

    [Fact]
    public void OnshapeProvider_ImplementsICadWebProvider_InterfaceShape()
    {
        var providerType = typeof(OnshapeProvider);
        Assert.True(typeof(ICadWebProvider).IsAssignableFrom(providerType));

        foreach (var method in typeof(ICadWebProvider).GetMethods())
        {
            if (method.IsSpecialName)
            {
                continue;
            }

            var map = providerType.GetMethod(
                method.Name,
                method.GetParameters().Select(p => p.ParameterType).ToArray());
            Assert.NotNull(map);
        }
    }

    [Fact]
    public void EndToEnd_DocumentAndPartStudioManagers_ReturnSuccess()
    {
        var provider = CreateProvider();
        provider.Authenticate("ak", "skid", "secret");

        var open = provider.DocOpen("doc-1");
        open["success"].Should().Be(true);
        open["name"].Should().Be("Test Doc");

        var elements = provider.ElementList("doc-1", "w", "ws-1");
        elements["success"].Should().Be(true);

        var addr = new CadElementAddress("doc-1", "ws-1", null, "e-part");
        var element = provider.ElementGet(addr);
        element["success"].Should().Be(true);
        element["id"].Should().Be("e-part");
    }

    [Fact]
    public void EndToEnd_AssemblyAndExportManagers_ReturnSuccess()
    {
        var client = CreateClient();
        var driver = new OnshapeDriver(client);
        var asmAddr = new CadElementAddress("doc-1", "ws-1", null, "e-asm");
        var partAddr = new CadElementAddress("doc-1", "ws-1", null, "e-part");

        var assembly = new AssemblyManager(client, driver);
        var instances = assembly.ListInstances(asmAddr);
        instances["success"].Should().Be(true);

        var provider = new OnshapeProvider(client, driver);
        var export = provider.ExportStep(partAddr);
        export["success"].Should().Be(true);
        export["downloadUrl"].Should().Be("https://cdn.example/export.step");
    }

    [Fact]
    public void EndToEnd_ExportStl_PollsTranslation_ReturnsDownloadUrl()
    {
        var provider = CreateProvider();
        var partAddr = new CadElementAddress("doc-1", "ws-1", null, "e-part");
        var export = provider.ExportStl(partAddr);
        export["success"].Should().Be(true);
        export["downloadUrl"].Should().Be("https://cdn.example/export.stl");
    }

    [Fact]
    public void Write_With409_ReturnsOnshapeMicroversionSkewException_WithServerMicroversion()
    {
        _server
            .Given(Request.Create().WithPath("/api/v6/elements/d/doc-1/w/ws-1/e/e-part").UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(409)
                .WithHeader("Content-Type", "application/json")
                .WithBody("""{"message":"wrongMicroversion","microversion":"server-mv-99"}"""));

        var provider = CreateProvider();
        var addr = new CadElementAddress("doc-1", "ws-1", "stale-mv", "e-part");

        var act = () => provider.ElementRename(addr, "Renamed");

        var ex = act.Should().Throw<OnshapeMicroversionSkewException>().Which;
        ex.ServerMicroversion.Should().Be("server-mv-99");
    }

    [Fact]
    public void SketchUrlMatches_PartStudioFeaturesPath()
    {
        var p = OnshapeApiPaths.SketchFeatures("d", "w", "ws", "e");
        p.Should().Be(OnshapeApiPaths.PartStudioFeatures("d", "w", "ws", "e"));
        p.Should().Contain("/features");
    }

    [Fact]
    public void SketchReplace_WithSketchPayload_ReturnsSuccess_OnMockProvider()
    {
        var mock = new MockOnshapeProvider();
        var addr = new CadElementAddress("doc-1", "ws-1", null, "e-part");
        var payload = new Dictionary<string, object?>
        {
            ["entities"] = new List<object>(),
            ["constraints"] = new List<object>(),
        };

        var result = mock.SketchReplace(addr, payload);

        result["success"].Should().Be(true);
        mock.CallLog.Should().Contain(c => c.Method == "SketchReplace");
    }

    [Fact]
    public void SketchReplace_ViaProvider_PostsToFeaturesEndpoint()
    {
        _server
            .Given(Request.Create().WithPath("/api/v6/partstudios/d/doc-1/w/ws-1/e/e-part/features").UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody("""{"microversion":"mv-3"}"""));

        var provider = CreateProvider();
        var addr = new CadElementAddress("doc-1", "ws-1", null, "e-part");
        var result = provider.SketchReplace(addr, new Dictionary<string, object?> { ["entities"] = Array.Empty<object>() });

        result["success"].Should().Be(true);
        _server.LogEntries.Last().RequestMessage.Body.Should().Contain("entities");
    }

    [Fact]
    public void FeatureManager_List_ReturnsFeatures()
    {
        var provider = CreateProvider();
        var features = new FeatureManager(
            CreateClient(),
            new OnshapeDriver(CreateClient()));
        var addr = new CadElementAddress("doc-1", "ws-1", null, "e-part");
        var result = features.FeatureList(addr);
        result["success"].Should().Be(true);
    }

    private OnshapeProvider CreateProvider()
    {
        var client = CreateClient();
        var driver = new OnshapeDriver(client);
        return new OnshapeProvider(client, driver);
    }

    private OnshapeClient CreateClient()
    {
        var http = new HttpClient { BaseAddress = new Uri(_server.Url!) };
        return new OnshapeClient(http, "ak", "sk", _server.Url);
    }

    private void RegisterDefaultStubs()
    {
        _server
            .Given(Request.Create().WithPath("/api/v6/oauth/keys").UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithBody("""{"success":true}"""));

        _server
            .Given(Request.Create().WithPath("/api/v6/documents/doc-1").UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithBody("""{"name":"Test Doc","id":"doc-1","microversion":"mv-1"}"""));

        _server
            .Given(Request.Create().WithPath("/api/v6/documents/d/doc-1/w/ws-1/elements").UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithBody("""{"elements":[{"id":"e-part","name":"Part Studio 1"}]}"""));

        _server
            .Given(Request.Create().WithPath("/api/v6/elements/d/doc-1/w/ws-1/e/e-part").UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithBody("""{"id":"e-part","microversion":"mv-2"}"""));

        _server
            .Given(Request.Create().WithPath("/api/v6/partstudios/d/doc-1/w/ws-1/e/e-part/export/step").UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithBody("""{"id":"tid-step-1"}"""));

        _server
            .Given(Request.Create().WithPath("/api/v6/translations/tid-step-1").UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithBody("""{"requestState":"DONE","resultExternalDataIds":["https://cdn.example/export.step"]}"""));

        _server
            .Given(Request.Create().WithPath("/api/v6/partstudios/d/doc-1/w/ws-1/e/e-part/export/stl").UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithBody("""{"id":"tid-stl-1"}"""));

        _server
            .Given(Request.Create().WithPath("/api/v6/translations/tid-stl-1").UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithBody("""{"requestState":"DONE","resultExternalDataIds":["https://cdn.example/export.stl"]}"""));

        _server
            .Given(Request.Create().WithPath("/api/v6/assemblies/d/doc-1/w/ws-1/e/e-asm/instances").UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithBody("""{"instances":[]}"""));

        _server
            .Given(Request.Create().WithPath("/api/v6/partstudios/d/doc-1/w/ws-1/e/e-part/features").UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithBody("""{"features":[{"featureId":"f1","featureType":"extrude"}]}"""));
    }
}