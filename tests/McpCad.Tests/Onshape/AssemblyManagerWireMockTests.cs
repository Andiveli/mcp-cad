using McpCad.Core.Models;
using McpCad.Onshape;
using McpCad.Onshape.Managers;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace McpCad.Tests.Onshape;

public sealed class AssemblyManagerWireMockTests : IDisposable
{
    private readonly WireMockServer _server = WireMockServer.Start();

    public void Dispose() => _server.Dispose();

    [Fact]
    public void ListInstances_GetsAssemblyDefinition()
    {
        _server.Given(Request.Create()
                .WithPath("/api/v6/assemblies/d/doc-1/w/ws-1/e/asm-1")
                .UsingGet())
            .RespondWith(Response.Create().WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody("""{"rootAssembly":{"instances":[{"id":"node-1"}]}}"""));

        var result = CreateManager().ListInstances(Address());

        result["success"].Should().Be(true);
        _server.LogEntries.Should().ContainSingle();
    }

    [Fact]
    public void CreateInstance_PostsWholePartStudioContract()
    {
        _server.Given(Request.Create()
                .WithPath("/api/v6/assemblies/d/doc-1/w/ws-1/e/asm-1/instances")
                .UsingPost()
                .WithBody(body => body.Contains("\"documentId\":\"source-doc\"")
                                  && body.Contains("\"elementId\":\"source-part-studio\"")
                                  && body.Contains("\"isWholePartStudio\":true")
                                  && body.Contains("\"includePartTypes\":[\"PARTS\"]")))
            .RespondWith(Response.Create().WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody("""{"sourceMicroversion":"mv-2","instances":[{"id":"node-1"}]}"""));

        var result = CreateManager().CreateInstance(Address(), "source-doc", "source-part-studio");

        result["success"].Should().Be(true);
    }

    [Fact]
    public void CreateInstance_EmptySuccess_ReturnsRequestContext()
    {
        _server.Given(Request.Create()
                .WithPath("/api/v6/assemblies/d/doc-1/w/ws-1/e/asm-1/instances")
                .UsingPost())
            .RespondWith(Response.Create().WithStatusCode(204));

        var result = CreateManager().CreateInstance(Address(), "source-doc", "source-part-studio");

        result.Should().Contain(new Dictionary<string, object?>
        {
            ["success"] = true,
            ["documentId"] = "doc-1",
            ["workspaceId"] = "ws-1",
            ["assemblyElementId"] = "asm-1",
            ["sourceDocumentId"] = "source-doc",
            ["sourceElementId"] = "source-part-studio",
        });
    }

    [Theory]
    [InlineData("[]")]
    [InlineData("\"node-1\"")]
    [InlineData("true")]
    public void CreateInstance_NonObjectJsonSuccess_PreservesResponse(string responseJson)
    {
        _server.Given(Request.Create()
                .WithPath("/api/v6/assemblies/d/doc-1/w/ws-1/e/asm-1/instances")
                .UsingPost())
            .RespondWith(Response.Create().WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody(responseJson));

        var result = CreateManager().CreateInstance(Address(), "source-doc", "source-part-studio");

        result["success"].Should().Be(true);
        result.Should().ContainKey("value");
    }

    [Fact]
    public void CreateInstance_HttpError_PreservesApiException()
    {
        _server.Given(Request.Create()
                .WithPath("/api/v6/assemblies/d/doc-1/w/ws-1/e/asm-1/instances")
                .UsingPost())
            .RespondWith(Response.Create().WithStatusCode(400)
                .WithHeader("Content-Type", "application/json")
                .WithBody("""{"message":"invalid instance"}"""));

        var act = () => CreateManager().CreateInstance(Address(), "source-doc", "source-part-studio");

        act.Should().Throw<McpCad.Core.Exceptions.OnshapeApiException>()
            .Which.Reason.Should().Be("invalid instance");
    }

    [Fact]
    public void DeleteInstance_DeletesNodeIdEndpoint()
    {
        _server.Given(Request.Create()
                .WithPath("/api/v6/assemblies/d/doc-1/w/ws-1/e/asm-1/instance/nodeid/node-1")
                .UsingDelete())
            .RespondWith(Response.Create().WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody("{}"));

        var result = CreateManager().DeleteInstance(Address(), "node-1");

        result["success"].Should().Be(true);
    }

    private AssemblyManager CreateManager()
    {
        var client = new OnshapeClient(new HttpClient(), "ak", "sk", _server.Url);
        return new AssemblyManager(client, new OnshapeDriver(client) { DefaultWvm = "w" });
    }

    private static CadElementAddress Address() => new("doc-1", "ws-1", null, "asm-1");
}
