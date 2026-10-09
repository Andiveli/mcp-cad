using McpCad.Core.Models;

namespace McpCad.Tests.Core;

public class CadElementAddressTests
{
    [Fact]
    public void SerializeDeserialize_RoundTripsAllFields()
    {
        var original = new CadElementAddress("doc-1", "ws-9", "mv-42", "e-part");

        var json = original.Serialize();
        var restored = CadElementAddress.Deserialize(json);

        Assert.Equal(original, restored);
    }

    [Fact]
    public void SerializeDeserialize_OmitsNullMicroversion()
    {
        var original = new CadElementAddress("doc-1", "ws-9", null, "e-part");

        var json = original.Serialize();

        Assert.DoesNotContain("microversionId", json, StringComparison.OrdinalIgnoreCase);

        var restored = CadElementAddress.Deserialize(json);

        Assert.Equal(original.DocumentId, restored.DocumentId);
        Assert.Null(restored.MicroversionId);
        Assert.Equal(original.ElementId, restored.ElementId);
    }

    [Fact]
    public void Equality_UsesAllRecordFields()
    {
        var a = new CadElementAddress("d", "w", "m", "e");
        var b = new CadElementAddress("d", "w", "m", "e");
        var c = new CadElementAddress("d", "w", null, "e");

        Assert.Equal(a, b);
        Assert.NotEqual(a, c);
    }
}
