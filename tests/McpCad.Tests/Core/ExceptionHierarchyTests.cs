using McpCad.Core;
using McpCad.Core.Exceptions;

namespace McpCad.Tests.Core;

public class ExceptionHierarchyTests
{
    [Theory]
    [InlineData(401)]
    [InlineData(403)]
    public void FromHttpStatus_Unauthorized_ReturnsOnshapeAuthException(int statusCode)
    {
        var ex = OnshapeApiException.FromHttpStatus(statusCode, "unauthorized");

        Assert.IsType<OnshapeAuthException>(ex);
        Assert.IsAssignableFrom<CadProviderException>(ex);
        Assert.IsNotType<HttpRequestException>(ex.GetType());
    }

    [Fact]
    public void FromHttpStatus_429_ReturnsOnshapeQuotaException_WithRetryAfter()
    {
        var retry = TimeSpan.FromSeconds(30);
        var ex = OnshapeApiException.FromHttpStatus(429, "rate_limited", retryAfter: retry);

        var quota = Assert.IsType<OnshapeQuotaException>(ex);
        Assert.Equal(retry, quota.RetryAfter);
    }

    [Fact]
    public void FromHttpStatus_409_ReturnsOnshapeMicroversionSkewException()
    {
        var ex = OnshapeApiException.FromHttpStatus(409, "wrongMicroversion", serverMicroversion: "server-mv-7");

        var skew = Assert.IsType<OnshapeMicroversionSkewException>(ex);
        Assert.Equal("server-mv-7", skew.ServerMicroversion);
    }

    [Fact]
    public void FromHttpStatus_500_ReturnsOnshapeApiException()
    {
        var ex = OnshapeApiException.FromHttpStatus(500, "internal_error");

        var api = Assert.IsType<OnshapeApiException>(ex);
        Assert.Equal(500, api.StatusCode);
        Assert.Equal("internal_error", api.Reason);
    }

    [Fact]
    public void ICadWebProvider_IsSiblingOfIMechanicalCadProvider_NotAssignable()
    {
        Assert.False(typeof(IMechanicalCadProvider).IsAssignableFrom(typeof(ICadWebProvider)));
        Assert.True(typeof(ICadProvider).IsAssignableFrom(typeof(ICadWebProvider)));
    }
}