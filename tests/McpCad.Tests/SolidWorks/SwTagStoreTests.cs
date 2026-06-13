using McpCad.SolidWorks.Helpers;

namespace McpCad.Tests.SolidWorks;

/// <summary>
/// Structural safety tests for SwTagStore (COM lifetime hygiene).
/// Written per CRITICAL 3. Cannot fully assert Marshal.ReleaseComObject side-effects in pure unit test
/// (would require real COM objects), but we verify the public contract: Clear() does not throw,
/// and overwriting a tag is safe (no exceptions).
/// </summary>
public class SwTagStoreTests
{
    [Fact]
    public void Clear_DoesNotThrow_WhenEmpty()
    {
        var store = new SwTagStore();
        var ex = Record.Exception(() => store.Clear());
        Assert.Null(ex);
    }

    [Fact]
    public void Clear_DoesNotThrow_AfterSetTags()
    {
        var store = new SwTagStore();
        // Use plain objects (not real COM) — SetTag will still store them; Clear will attempt ReleaseComObject (best-effort, swallows).
        store.SetTag("active", "foo", new object());
        store.SetTag("active", "bar", "some-string-ref");

        var ex = Record.Exception(() => store.Clear());
        Assert.Null(ex);
    }

    [Fact]
    public void SetTag_Overwriting_IsSafe_DoesNotThrow()
    {
        var store = new SwTagStore();
        var first = new object();
        var second = new object();

        var ex = Record.Exception(() =>
        {
            store.SetTag("s1", "t1", first);
            store.SetTag("s1", "t1", second); // overwrite triggers release attempt on first
            store.Clear();
        });

        Assert.Null(ex);
    }
}
