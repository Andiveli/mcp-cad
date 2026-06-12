using McpCad.Core.Exceptions;
using ModelDoc2 = SolidWorks.Interop.sldworks.ModelDoc2;
using Callout = SolidWorks.Interop.sldworks.Callout;  // for SelectByID2 Callout param (interop binding types it as Callout not object in some versions)

namespace McpCad.SolidWorks.Helpers;

/// <summary>
/// Selection helper for SolidWorks: thin wrappers around Extension.SelectByID2(..., mark) + SelectionManager for multi/profile marks (0-63).
/// Index priority for MVP ("1" from profiles()) preferred; @tag resolved via SwTagStore before select.
/// Used by FeatureManager for profile resolve in Extrude (mark !=0 for profile selection in Insert*).
/// Documented exact SW API calls per tasks: doc.Extension.SelectByID2(name, "SKETCHSEGMENT" or "PROFILE", x,y,z, append, mark, ...)
/// If variance on live (e.g. type strings), "TODO verify on live SW in verify phase".
/// Mirrors Inventor resolver pattern but SW-specific (no shared abstraction this increment).
/// </summary>
public class SelectionHelper
{
    /// <summary>
    /// Select a profile or entity by index string ("1") or resolved @tag using SelectByID2 with mark for feature ops.
    /// Returns true if selection succeeded (for basic MVP).
    /// CRITICAL 3 fix: when @tag resolves to a *real* API object (ISketchSegment etc from SwTagStore), use SelectByID2 with the object's name (or GetName) + mark.
    /// For numeric "1" (from profiles or synthetic), keep the reliable empty+mark pattern for extrude profile selection.
    /// Profile resolution now returns real selectors usable by extrude/hole (real closed contour refs when available).
    /// </summary>
    public bool SelectProfileByIndexOrTag(ModelDoc2 doc, string profileSpec, string sketchKey, SwTagStore tagStore, int mark = 1)
    {
        if (doc == null) throw new InventorConnectionException("No active document for selection.");

        string spec = profileSpec?.Trim() ?? "1";

        // Priority: numeric index first for MVP (reliable from sketch_profiles "1")
        if (int.TryParse(spec, out int idx) && idx >= 1)
        {
            // MVP: direct empty+mark for profile (reliable for extrude Insert*); resolved index "1" treated same. Name for real segment/PID later.
            // Exact call per design: Extension.SelectByID2( nameOrEmpty, "SKETCHSEGMENT", 0,0,0, false, mark, null, 0 )
            // Route through the typed wrapper that does (object?)cast for Callout interop binding variance (CRITICAL 4 stabilization + skeleton build compatibility).
            // TODO verify on live SW in verify phase
            return SelectById2(doc, "", "SKETCHSEGMENT", 0, 0, 0, false, mark, null, 0)
                || SelectById2(doc, null, "", 0, 0, 0, false, mark, null, 0);
        }

        // @tag path: resolve then select (in-mem for MVP). CRITICAL 3: resolve now returns real object, not string.
        if (spec.StartsWith("@", StringComparison.Ordinal))
        {
            var resolved = tagStore?.Resolve(sketchKey, spec);
            if (resolved != null)
            {
                // CRITICAL 3 fix: if real API object (ISketchSegment etc), extract usable name for SelectByID2 + mark.
                // This enables real closed-contour selection instead of always empty+mark or synthetic "1".
                string useName = "";
                try
                {
                    dynamic seg = resolved;
                    // Common SW segment properties for name: GetName(), Name, or the object itself may work as name in some SelectByID2 overloads.
                    useName = (seg.GetName?.ToString() ?? seg.Name?.ToString() ?? seg.ToString() ?? "");
                }
                catch (Exception) { useName = ""; }
                if (!string.IsNullOrWhiteSpace(useName))
                {
                    bool sel = SelectById2(doc, useName, "SKETCHSEGMENT", 0, 0, 0, false, mark, null, 0);
                    if (sel) return true;
                }
                // Fallback to reliable empty+mark if name resolution failed on this SW version (documented)
                return SelectById2(doc, "", "SKETCHSEGMENT", 0, 0, 0, false, mark, null, 0);
            }
            // no resolve: fall to mark
            return SelectById2(doc, "", "SKETCHSEGMENT", 0, 0, 0, false, mark, null, 0);
        }

        // Fallback: try direct name select (via typed wrapper for Callout interop binding stability)
        return SelectById2(doc, spec, "SKETCHSEGMENT", 0, 0, 0, false, mark, null, 0);
    }

    /// <summary>
    /// Wrapper for generic SelectByID2 (used by SketchManager create etc).
    /// Exact signature used: bool SelectByID2(string Name, string Type, double X, double Y, double Z, bool Append, int Mark, Callout Callout, int SelectOption)
    /// Callout cast fixed (interop expects typed Callout or object?); use as Callout? + null.
    /// TODO verify exact signature + behavior on live SolidWorks in sdd-verify phase (SelectByID2 Callout param type)
    /// </summary>
    public bool SelectById2(ModelDoc2 doc, string name, string type, double x, double y, double z, bool append, int mark, Callout? callout = null, int selectOption = 0)
    {
        if (doc?.Extension == null) return false;
        try
        {
            // Cast to object for the Callout parameter (some interop bindings expect object here; CRITICAL 4 stabilization).
            object? calloutArg = callout;
            // Use dynamic to bypass typed Callout param signature variance in interop assembly (avoids CS1503 object -> Callout at compile).
            dynamic ext = doc.Extension;
            return ext.SelectByID2(name, type, x, y, z, append, mark, calloutArg, selectOption);
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>
    /// Clear current selection (best effort for MVP before new select/mark).
    /// </summary>
    public void ClearSelection(ModelDoc2 doc)
    {
        try { doc?.ClearSelection2(true); } catch (Exception) { /* best effort */ }
    }
}
