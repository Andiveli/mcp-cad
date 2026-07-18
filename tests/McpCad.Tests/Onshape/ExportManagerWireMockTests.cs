using McpCad.Core.Models;
using McpCad.Onshape;
using McpCad.Onshape.Managers;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace McpCad.Tests.Onshape;

public sealed class ExportManagerWireMockTests : IDisposable
{
    private readonly WireMockServer _server = WireMockServer.Start();

    public void Dispose() => _server.Dispose();

    [Fact]
    public void ExportStl_FollowsSameOriginRedirectWithAuthentication()
    {
        var downloadUrl = $"{_server.Url}/modelexport/cube.stl";
        _server.Given(Request.Create()
                .WithPath("/api/v6/partstudios/d/doc-1/w/ws-1/e/part-1/stl")
                .WithParam("mode", "binary")
                .WithParam("grouping", "true")
                .WithParam("units", "MILLIMETER")
                .UsingGet())
            .RespondWith(Response.Create().WithStatusCode(307)
                .WithHeader("Location", "/modelexport/cube.stl"));
        _server.Given(Request.Create()
                .WithPath("/modelexport/cube.stl")
                .WithHeader("Authorization", "Basic *")
                .UsingGet())
            .RespondWith(Response.Create().WithStatusCode(200)
                .WithHeader("Content-Type", "application/sla")
                .WithBody(new byte[] { 1, 2, 3 }));

        var result = CreateManager().ExportStl(Address(), new Dictionary<string, object?>
        {
            ["mode"] = "binary",
            ["grouping"] = true,
            ["units"] = "MILLIMETER",
        });

        result["success"].Should().Be(true);
        result["format"].Should().Be("STL");
        result["contentType"].Should().Be("application/sla");
        result["downloadUrl"].Should().Be(downloadUrl);
        _server.LogEntries.Should().HaveCount(2);
    }

    [Fact]
    public void ExportStl_RejectsCrossOriginRedirectWithoutLeakingAuthentication()
    {
        using var untrustedServer = WireMockServer.Start();
        _server.Given(Request.Create().UsingGet())
            .RespondWith(Response.Create().WithStatusCode(307)
                .WithHeader("Location", $"{untrustedServer.Url}/cube.stl"));

        var act = () => CreateManager().ExportStl(Address());

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*must remain on the authenticated Onshape origin*");
        untrustedServer.LogEntries.Should().BeEmpty();
    }

    [Fact]
    public void ExportStep_PostsDocumentedAsynchronousEndpoint()
    {
        _server.Given(Request.Create()
                .WithPath("/api/v6/partstudios/d/doc-1/w/ws-1/e/part-1/export/step")
                .UsingPost())
            .RespondWith(Response.Create().WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody("""{"requestState":"DONE"}"""));

        var result = CreateManager().ExportStep(Address());

        result["success"].Should().Be(true);
        _server.LogEntries.Should().ContainSingle();
    }

    private ExportManager CreateManager()
    {
        var client = new OnshapeClient(
            new HttpClient(new HttpClientHandler { AllowAutoRedirect = false }),
            "ak",
            "sk",
            _server.Url);
        return new ExportManager(client, new OnshapeDriver(client) { DefaultWvm = "w" });
    }

    private static CadElementAddress Address() => new("doc-1", "ws-1", null, "part-1");
}
