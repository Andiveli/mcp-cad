using System.Collections.Concurrent;

namespace McpCad.SolidWorks.Helpers;

/// <summary>
/// Minimal in-memory tag store for SolidWorks MVP basic loop (Phase 5).
/// Supports @tag + runtime entity ref (PID byte[] / mark / segment ref) per sketch key.
/// Index priority for "1" etc is handled in SelectionHelper / FeatureManager (no store needed for numeric).
/// Per design §5: per-provider helpers (no surface change, no Inventor touch). Simple dictionary; no persistent ID full until verify/live.
/// "TODO verify on live SW in verify phase" for PID/GetPersistReference3 usage.
/// </summary>
public class SwTagStore
{
    // Keyed by sketch identifier (name or session index) + tag → real API object reference (stored as object, not synthetic string).
    // Stores actual COM references (ISketchSegment, IEdge, etc.) captured from Create* returns so SelectionHelper can
    // use real geometry for SelectByID2 / profile selection instead of synthetic names or hardcoded "1".
    private readonly ConcurrentDictionary<(string sketchKey, string tag), object> _store = new();

    /// <summary>
    /// Store a tag for a sketch entity (called from SketchManager on create with tag=).
    /// entityRef: the real API object (ISketchSegment etc) returned by CreateLine/CreateCircle. Stored directly (not a string name).
    /// </summary>
    public void SetTag(string sketchKey, string tag, object entityRef)
    {
        if (string.IsNullOrWhiteSpace(sketchKey) || string.IsNullOrWhiteSpace(tag) || entityRef == null)
            return;
        string clean = tag.TrimStart('@');
        _store[(sketchKey, clean)] = entityRef;
    }

    /// <summary>
    /// Resolve @tag (or pass-through) to usable real API object ref for selection.
    /// Returns the stored object (ISketchSegment etc) or null. SelectionHelper uses this for real profile selection.
    /// </summary>
    public object? Resolve(string sketchKey, string tag)
    {
        if (string.IsNullOrWhiteSpace(tag))
            return null;
        string clean = tag.TrimStart('@');
        if (_store.TryGetValue((sketchKey, clean), out var refVal))
            return refVal;
        return null;
    }

    /// <summary>
    /// Clear for test isolation (MVP).
    /// </summary>
    public void Clear() => _store.Clear();
}
