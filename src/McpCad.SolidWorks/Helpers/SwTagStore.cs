using System.Collections.Concurrent;
using System.Runtime.InteropServices;

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
    /// Releases previous COM ref if overwriting (CRITICAL 3 fix).
    /// </summary>
    public void SetTag(string sketchKey, string tag, object entityRef)
    {
        if (string.IsNullOrWhiteSpace(sketchKey) || string.IsNullOrWhiteSpace(tag) || entityRef == null)
            return;
        string clean = tag.TrimStart('@');
        var key = (sketchKey, clean);

        // Release previous value if overwriting (COM ref may have been replaced)
        if (_store.TryRemove(key, out var oldRef))
            ReleaseComObjectSafe(oldRef);

        _store[key] = entityRef;
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
    /// Clear all tags, releasing any stored COM references (CRITICAL 3 fix).
    /// Best-effort: swallows per-value exceptions so one bad ref doesn't block the rest.
    /// </summary>
    public void Clear()
    {
        foreach (var key in _store.Keys)
        {
            if (_store.TryRemove(key, out var refVal))
                ReleaseComObjectSafe(refVal);
        }
    }

    /// <summary>
    /// Best-effort ReleaseComObject for potentially-COM references.
    /// Safe for non-COM objects (string, plain object, etc.) — catches and swallows.
    /// </summary>
    private static void ReleaseComObjectSafe(object obj)
    {
        try
        {
            if (obj != null && Marshal.IsComObject(obj))
                Marshal.ReleaseComObject(obj);
        }
        catch (Exception)
        {
            // Best-effort per CRITICAL 3: COM release should never crash the caller
        }
    }
}
