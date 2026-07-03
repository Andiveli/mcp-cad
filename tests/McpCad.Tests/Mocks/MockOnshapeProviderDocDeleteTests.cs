using McpCad.Core.Exceptions;
using McpCad.Tests.Mocks;

namespace McpCad.Tests.Mocks;

public class MockOnshapeProviderDocDeleteTests
{
    [Fact]
    public void DocDelete_WhenConfirmFalse_ThrowsCadProviderException()
    {
        var mock = new MockOnshapeProvider();
        var act = () => mock.DocDelete("doc-1", confirm: false);
        act.Should().Throw<CadProviderException>()
            .WithMessage("*confirm=true*");
        mock.CallLog.Should().BeEmpty();
    }
}