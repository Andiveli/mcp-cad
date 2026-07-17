using System.Reflection;
using McpCad.Core;
using McpCad.Tests.Mocks;

namespace McpCad.Tests.Onshape;

public class MockOnshapeProviderConformanceTests
{
    [Fact]
    public void MockOnshapeProvider_SatisfiesICadWebProviderShape()
    {
        var providerType = typeof(MockOnshapeProvider);
        Assert.True(typeof(ICadWebProvider).IsAssignableFrom(providerType));
        Assert.False(typeof(IMechanicalCadProvider).IsAssignableFrom(providerType));

        foreach (var method in typeof(ICadWebProvider).GetMethods().Where(m => !m.IsSpecialName))
        {
            var map = providerType.GetMethod(
                method.Name,
                method.GetParameters().Select(p => p.ParameterType).ToArray());
            Assert.NotNull(map);
        }
    }

    [Fact]
    public void MockOnshapeProvider_RecordsCalls()
    {
        var mock = new MockOnshapeProvider();
        var addr = new McpCad.Core.Models.CadElementAddress("d1", "w1", null, "e1");
        mock.ElementGet(addr);
        mock.CallLog.Should().ContainSingle(c => c.Method == "ElementGet");
    }
}