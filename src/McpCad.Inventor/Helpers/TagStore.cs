namespace McpCad.Inventor.Helpers;

/// <summary>
/// In-memory tag registry for sketch entities.
/// Maps (sketch index, entity index) → tag string.
/// Tags survive for the session but not across restarts.
/// </summary>
public static class TagStore
{
    private static readonly Dictionary<(int sketchIdx, int entityIdx), string> _store = new();

    /// <summary>Associate a tag with a sketch entity.</summary>
    public static void SetTag(int sketchIdx, int entityIdx, string tag)
    {
        _store[(sketchIdx, entityIdx)] = tag;
    }

    /// <summary>Get the tag for a sketch entity, or null if none.</summary>
    public static string? GetTag(int sketchIdx, int entityIdx)
    {
        return _store.TryGetValue((sketchIdx, entityIdx), out var tag) ? tag : null;
    }

    /// <summary>Resolve a tag to its entity index within a sketch, or null if not found.</summary>
    public static int? Resolve(int sketchIdx, string tag)
    {
        foreach (var ((si, ei), t) in _store)
        {
            if (si == sketchIdx && t == tag)
                return ei;
        }
        return null;
    }

    /// <summary>Clear all stored tags. Useful for testing.</summary>
    public static void Clear() => _store.Clear();
}