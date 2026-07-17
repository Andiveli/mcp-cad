using System.Text;
using McpCad.Onshape;

namespace McpCad.Tests.Onshape;

public class OnshapeBasicAuthTests
{
    [Fact]
    public void FormatAuthorizationHeader_MatchesOpenApiBasicAuthExample()
    {
        var b64 = OnshapeClient.FormatBasicAuthForTests("myAccess", "mySecret");
        b64.Should().Be(Convert.ToBase64String(Encoding.UTF8.GetBytes("myAccess:mySecret")));
    }
}