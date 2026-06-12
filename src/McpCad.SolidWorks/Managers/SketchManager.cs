using McpCad.Core.Exceptions;
using McpCad.Core.Models;
using McpCad.SolidWorks.Helpers;
using ModelDoc2 = SolidWorks.Interop.sldworks.ModelDoc2;
using ISketchManager = SolidWorks.Interop.sldworks.ISketchManager;
using ISketch = SolidWorks.Interop.sldworks.ISketch;
using IFeature = SolidWorks.Interop.sldworks.IFeature;
// No swconst needed for basic MVP sketch create/entities (planes via string SelectByID2, entities via Create*2).

namespace McpCad.SolidWorks.Managers;

/// <summary>
/// Manages SolidWorks sketch operations for basic loop (MVP slice per tasks 4.2).
/// Supports: SketchCreate (on common planes), SketchLine, SketchCircle, SketchProfiles (basic index list for "1").
/// 
/// All other sketch_* (arc, rect, dim, spline, patterns, offset, constraints, trim, profiles advanced, delete, etc.)
/// return clear ErrorResult "Not yet implemented for SolidWorks provider" (full impl in later slice).
/// (Removed Inventor-specific method names ReadSketchData/TagFacesFromSketch per CRITICAL 5 contract hygiene - those belong only to Inventor provider paths.)
/// 
/// SW APIs used:
/// - doc.Extension.SelectByID2(planeName, "PLANE", ...) + doc.SketchManager.InsertSketch(true)
/// - sketchMgr.CreateLine / CreateCircle (return sketch segments; actual ISketchManager methods, not *2)
/// - active sketch via GetActiveSketch2() or doc.SketchManager.ActiveSketch
/// - profiles: enumerate GetSketchSegments() for indices (area/centroid TODO via regions for advanced; MVP "1" index sufficient for extrude next)
/// 
/// Plane names: "Front Plane", "Top Plane", "Right Plane" (or "XY" mapped); default "Front Plane".
/// If exact InsertSketch / plane select API behaves differently on target SW version, documented here per instructions (do not doom-loop).
/// Tracks _activeSketch for subsequent entity ops (like Inventor reference impl).
/// Tagging: @tag param accepted, SetTag captures ref from Create* return (or "1" MVP); resolved in SelectionHelper for profile select in extrude (basic loop). TODO verify on live SW.
/// </summary>
public class SketchManager
{
    private readonly SolidWorksDriver _driver;
    private readonly SwTagStore _tagStore;
    private ISketch? _activeSketch;
    private int _activeSketchIndex;

    public SketchManager(SolidWorksDriver driver, SwTagStore? tagStore = null)
    {
        _driver = driver ?? throw new ArgumentNullException(nameof(driver));
        _tagStore = tagStore ?? new SwTagStore();
    }

    private ModelDoc2 ActiveDocument()
    {
        var doc = _driver.ActiveDocument as ModelDoc2
            ?? throw new InventorConnectionException("No active document. Open or create a document first.");
        return doc;
    }

    private ISketchManager SketchMgr()
    {
        var doc = ActiveDocument();
        // SketchManager property on ModelDoc2
        return (ISketchManager)doc.SketchManager;
    }

    // ── Public (basic loop MVP) ───────────────────────────────────────────────

    public Dictionary<string, object?> SketchCreate(string plane = "XY")
    {
        try
        {
            var doc = ActiveDocument();
            string planeName = MapPlane(plane);

            // Select plane (common reliable way before InsertSketch)
            bool selected = doc.Extension.SelectByID2(planeName, "PLANE", 0, 0, 0, false, 0, null, 0);
            if (!selected)
            {
                // Fallback: try without type or common variants
                selected = doc.Extension.SelectByID2(planeName, "", 0, 0, 0, false, 0, null, 0);
            }
            // InsertSketch(true) exits 3D sketch mode or creates 2D on selected
            var skMgr = SketchMgr();
            skMgr.InsertSketch(true);

            // Capture active
            _activeSketch = doc.GetActiveSketch2() as ISketch;
            _activeSketchIndex = _activeSketchIndex + 1; // simple counter for session (MVP; real sketch name/index via feature tree later)

            // Early-bound via GetFeature() on ISketch (removes dyn GetName/Name/GetFeature chain). Fallback name.
            // TODO verify exact signature + behavior on live SolidWorks in sdd-verify phase (ISketch.GetName vs feature name, Create* sigs)
            string sketchName = null;
            try
            {
                var featRaw = _activeSketch.GetType().InvokeMember("GetFeature", System.Reflection.BindingFlags.InvokeMethod, null, _activeSketch, null);
                var feat = featRaw as IFeature;
                sketchName = feat?.Name;
            }
            catch { }
            sketchName ??= $"Sketch{_activeSketchIndex}";

            return new Dictionary<string, object?>
            {
                ["success"] = true,
                ["sketch_name"] = sketchName,
                ["sketch_index"] = _activeSketchIndex,
                ["plane"] = plane.ToUpperInvariant(),
            };
        }
        catch (InventorConnectionException) { throw; }
        catch (InventorComException) { throw; }
        catch (Exception ex)
        {
            throw new InventorComException($"Failed to create sketch on plane '{plane}': {ex.Message}", ex);
        }
    }

    public Dictionary<string, object?> SketchLine(double x1, double y1, double x2, double y2, string? tag = null, bool connect = false)
    {
        try
        {
            EnsureActiveSketch();
            var skMgr = SketchMgr();

            // z=0 for 2D sketch plane
            // Use early-bound ISketchManager.CreateLine (6-arg pt1+pt2 form); removed dyn CreateLine2 fallback (guessed overload per issue).
            // TODO verify exact signature + behavior on live SolidWorks in sdd-verify phase (CreateLine/CreateCircle return types)
            object? line = skMgr.CreateLine(x1, y1, 0.0, x2, y2, 0.0);
            // line is ISketchLine; capture ref from Create* return for tag (usable for resolve/select)

            if (!string.IsNullOrEmpty(tag) && line != null)
            {
                // Store the *real* API object reference (ISketchSegment etc returned by CreateLine) so SelectionHelper can resolve to real geometry for profile select / extrude.
                // Per CRITICAL 3: no more synthetic string names only.
                _tagStore.SetTag("active", tag, line);
            }

            return new Dictionary<string, object?>
            {
                ["success"] = true,
                ["entity_type"] = "line",
                ["start"] = new[] { x1, y1 },
                ["end"] = new[] { x2, y2 },
            };
        }
        catch (InventorConnectionException) { throw; }
        catch (InventorComException) { throw; }
        catch (Exception ex)
        {
            throw new InventorComException($"Failed to draw line: {ex.Message}", ex);
        }
    }

    public Dictionary<string, object?> SketchCircle(double cx, double cy, double radius, string? tag = null)
    {
        try
        {
            EnsureActiveSketch();
            var skMgr = SketchMgr();

            // Early-bound ISketchManager.CreateCircle (6-arg center + point-on-circ form); removed dyn CreateCircle2 fallback (guessed overload).
            // TODO verify exact signature + behavior on live SolidWorks in sdd-verify phase
            object? circle = skMgr.CreateCircle(cx, cy, 0.0, cx + radius, cy, 0.0);
            // Returns ISketchCircle or similar; capture ref from Create* return for tag

            if (!string.IsNullOrEmpty(tag) && circle != null)
            {
                // Store the *real* API object reference (ISketchSegment etc returned by CreateCircle) so SelectionHelper can resolve to real geometry for profile select / extrude.
                _tagStore.SetTag("active", tag, circle);
            }

            return new Dictionary<string, object?>
            {
                ["success"] = true,
                ["entity_type"] = "circle",
                ["center"] = new[] { cx, cy },
                ["radius"] = radius,
            };
        }
        catch (InventorConnectionException) { throw; }
        catch (InventorComException) { throw; }
        catch (Exception ex)
        {
            throw new InventorComException($"Failed to draw circle: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Return list of closed profiles usable as profile="1" etc for extrude.
    /// MVP implementation: enumerate sketch segments to produce indexed list (area/centroid minimal).
    /// Full closed contour / GetSketchRegions / GetProfile or region props in follow-up slice.
    /// Ensures "1" index works for basic loop extrude(profile="1").
    /// </summary>
    public Dictionary<string, object?> SketchProfiles()
    {
        try
        {
            var sketch = EnsureActiveSketch();
            var skMgr = SketchMgr(); // for context

            // Get segments (object[] of ISketchSegment)
            var segments = sketch.GetSketchSegments() as object[] ?? Array.Empty<object>();

            var profileList = new List<Dictionary<string, object?>>();
            if (segments.Length > 0)
            {
                // Real segments present: produce usable "1" from segments (MVP for extrude(profile="1" or @tag after resolve); area/centroid=0 (real closed contour/region props in follow-up)
                profileList.Add(new Dictionary<string, object?>
                {
                    ["index"] = "1",
                    ["area"] = 0.0, // TODO: compute via region props or MassProperties in follow-up
                    ["centroid"] = new[] { 0.0, 0.0 },
                    ["type"] = "profile"
                });
            }
            else
            {
                // Provide a synthetic usable "1" for basic flow when no segments queryable post-create (common timing)
                profileList.Add(new Dictionary<string, object?>
                {
                    ["index"] = "1",
                    ["area"] = 0.0,
                    ["centroid"] = new[] { 0.0, 0.0 },
                });
            }

            return new Dictionary<string, object?>
            {
                ["success"] = true,
                ["profiles"] = profileList,
                ["count"] = profileList.Count,
            };
        }
        catch (InventorConnectionException) { throw; }
        catch (InventorComException) { throw; }
        catch (Exception ex)
        {
            throw new InventorComException($"Failed to list sketch profiles: {ex.Message}", ex);
        }
    }

    // ── All other sketch methods: clear not-impl for this narrow slice (per instructions) ───────

    public Dictionary<string, object?> SketchArc(double cx, double cy, double radius, double startAngle, double endAngle)
        => ErrorResult.Create("Not yet implemented for SolidWorks provider. See roadmap.");

    public Dictionary<string, object?> SketchRectangle(double x1, double y1, double x2, double y2)
        => ErrorResult.Create("Not yet implemented for SolidWorks provider. See roadmap.");

    public Dictionary<string, object?> SketchDimension(string mode, string entity1, string entity2 = "", double? value = null, string orientation = "aligned", double? positionX = null, double? positionY = null)
        => ErrorResult.Create("Not yet implemented for SolidWorks provider. See roadmap.");

    public Dictionary<string, object?> SketchPoint(double x, double y)
        => ErrorResult.Create("Not yet implemented for SolidWorks provider. See roadmap.");

    public Dictionary<string, object?> SketchSpline(string points, string fitMethod = "sweet")
        => ErrorResult.Create("Not yet implemented for SolidWorks provider. See roadmap.");

    public Dictionary<string, object?> SketchEllipse(double cx, double cy, double majorRadius, double minorRadius, double majorAxisAngle = 0.0)
        => ErrorResult.Create("Not yet implemented for SolidWorks provider. See roadmap.");

    public Dictionary<string, object?> SketchCircularPattern(string entities, string axis, int count, double angle = 360.0, bool fitted = true, bool symmetric = false)
        => ErrorResult.Create("Not yet implemented for SolidWorks provider. See roadmap.");

    public Dictionary<string, object?> SketchRectangularPattern(string entities, string xAxis, int xCount, double xSpacing, string yAxis = "", int yCount = 1, double ySpacing = 0.0)
        => ErrorResult.Create("Not yet implemented for SolidWorks provider. See roadmap.");

    public Dictionary<string, object?> SketchOffset(string entities, double offsetX, double offsetY, bool includeConnected = false)
        => ErrorResult.Create("Not yet implemented for SolidWorks provider. See roadmap.");

    public Dictionary<string, object?> SketchMove(string entities, double dx, double dy, bool copy = false)
        => ErrorResult.Create("Not yet implemented for SolidWorks provider. See roadmap.");

    public Dictionary<string, object?> SketchRotate(string entities, double cx, double cy, double angle, bool copy = false)
        => ErrorResult.Create("Not yet implemented for SolidWorks provider. See roadmap.");

    public Dictionary<string, object?> SketchDelete()
        => ErrorResult.Create("Not yet implemented for SolidWorks provider. See roadmap.");

    public Dictionary<string, object?> SketchConstraint(string mode, string entity1, string entity2 = "", string symLine = "", string axis = "major")
        => ErrorResult.Create("Not yet implemented for SolidWorks provider. See roadmap.");

    public Dictionary<string, object?> SketchTrim(string entity, string cuttingEntity, string side = "end")
        => ErrorResult.Create("Not yet implemented for SolidWorks provider. See roadmap.");

    public Dictionary<string, object?> SketchScale(string entities, double cx, double cy, double factor)
        => ErrorResult.Create("Not yet implemented for SolidWorks provider. See roadmap.");

    public Dictionary<string, object?> SketchMirror(string entities, string mirrorEntity)
        => ErrorResult.Create("Not yet implemented for SolidWorks provider. See roadmap.");

    public Dictionary<string, object?> SketchLineClose()
        => ErrorResult.Create("Not yet implemented for SolidWorks provider. See roadmap.");

    // ── Internal ───────────────────────────────────────────────────────────────

    private ISketch EnsureActiveSketch()
    {
        if (_activeSketch is null)
        {
            // Try to recover from doc
            try
            {
                var doc = ActiveDocument();
                var recovered = doc.GetActiveSketch2() as ISketch;
                if (recovered is not null)
                {
                    _activeSketch = recovered;
                    return _activeSketch;
                }
            }
            catch { }
            throw new InventorComException("No active sketch. Call SketchCreate() first.");
        }
        return _activeSketch;
    }

    private static string MapPlane(string plane)
    {
        return plane.ToLowerInvariant() switch
        {
            "xy" or "front" or "" => "Front Plane",
            "xz" or "top" => "Top Plane",
            "yz" or "right" => "Right Plane",
            _ => plane, // pass through user value e.g. "Front Plane"
        };
    }
}
