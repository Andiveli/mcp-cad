using McpCad.Onshape;
using McpCad.Onshape.Managers;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace McpCad.Tests.Onshape;

public sealed class DocumentManagerWireMockTests : IDisposable
{
    private readonly WireMockServer _server = WireMockServer.Start();

    public void Dispose() => _server.Dispose();

    [Fact]
    public void DocCopy_PostsToWorkspaceCopyEndpoint_WithNewName()
    {
        GivenDocumentWithDefaultWorkspace();
        GivenWorkspaceCopy("{\"newName\":\"copy-name\"}");

        var result = CreateManager().DocCopy("doc-1", "copy-name");

        result["success"].Should().Be(true);
        result["id"].Should().Be("copied-doc");
        _server.LogEntries.Should().HaveCount(2);
    }

    [Fact]
    public void DocCreate_PostsNameToDocumentsEndpoint_AndReturnsDocumentAndWorkspaceIdentity()
    {
        GivenDocumentCreate();

        var result = CreateManager().DocCreate("new-doc");

        result["success"].Should().Be(true);
        result["id"].Should().Be("created-doc");
        result["documentId"].Should().Be("created-doc");
        result["workspaceId"].Should().Be("ws-new");
        _server.LogEntries.Should().HaveCount(1);
    }

    [Fact]
    public void DocCopy_PostsEmptyOptionsWhenNameIsOmitted()
    {
        GivenDocumentWithDefaultWorkspace();
        GivenWorkspaceCopy("{}");

        var result = CreateManager().DocCopy("doc-1");

        result["success"].Should().Be(true);
        result["id"].Should().Be("copied-doc");
        _server.LogEntries.Should().HaveCount(2);
    }

    private void GivenDocumentWithDefaultWorkspace()
    {
        _server.Given(Request.Create()
                .WithPath("/api/v6/documents/doc-1")
                .UsingGet())
            .RespondWith(Response.Create().WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody("""{"defaultWorkspace":{"id":"ws-1"}}"""));
    }

    private void GivenDocumentCreate()
    {
        _server.Given(Request.Create()
                .WithPath("/api/v6/documents")
                .UsingPost()
                .WithBody("{\"name\":\"new-doc\"}"))
            .RespondWith(Response.Create().WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody("""{"id":"created-doc","defaultWorkspace":{"id":"ws-new"}}"""));
    }

    private void GivenWorkspaceCopy(string requestBody)
    {
        _server.Given(Request.Create()
                .WithPath("/api/v6/documents/doc-1/workspaces/ws-1/copy")
                .UsingPost()
                .WithBody(requestBody))
            .RespondWith(Response.Create().WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody("""{"id":"copied-doc"}"""));
    }

    private DocumentManager CreateManager()
    {
        var client = new OnshapeClient(new HttpClient(), "ak", "sk", _server.Url);
        return new DocumentManager(client, new OnshapeDriver(client));
    }
}
