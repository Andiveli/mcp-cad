using System;
using System.Collections.Generic;

namespace McpCad.Inventor.Helpers;

/// <summary>
/// COM traversal for reading an existing PlanarSketch into the entity shape
/// expected by macro_god_part (sketch_entities JSON) and template capture/run.
/// Must live in Inventor project (dynamic COM + interop access).
/// Follows patterns from EdgeResolver/FaceResolver/AutoConstrain (1-based Item, WrapDispatch where needed, typed collections).
/// </summary>
public static class SketchReader
{
    /// <summary>
    /// Traverse typed sketch collections (lines/circles/arcs/points/ellipses/splines)
    /// and return list of entity dicts + non-fatal warnings.
    /// Entity shapes aim for round-trippable use in macro_god_part sketch_entities.
    /// </summary>
    public static (List<Dictionary<string, object?>> Entities, List<string> Warnings)
        ReadSketchEntities(dynamic planarSketch)
    {
        var entities = new List<Dictionary<string, object?>>();
        var warnings = new List<string>();

        if (planarSketch is null)
        {
            warnings.Add("No planar sketch provided to ReadSketchEntities.");
            return (entities, warnings);
        }

        // Lines (most common)
        try
        {
            dynamic lines = planarSketch.SketchLines;
            int count = lines?.Count ?? 0;
            for (int i = 1; i <= count; i++)
            {
                dynamic line = lines.Item(i);
                double x1 = GetPtX(line.StartSketchPoint);
                double y1 = GetPtY(line.StartSketchPoint);
                double x2 = GetPtX(line.EndSketchPoint);
                double y2 = GetPtY(line.EndSketchPoint);
                entities.Add(new Dictionary<string, object?>
                {
                    ["type"] = "line",
                    ["x1"] = x1,
                    ["y1"] = y1,
                    ["x2"] = x2,
                    ["y2"] = y2
                });
            }
        }
        catch (Exception ex)
        {
            warnings.Add($"SketchLines traversal warning: {ex.Message}");
        }

        // Circles
        try
        {
            dynamic circles = planarSketch.SketchCircles;
            int count = circles?.Count ?? 0;
            for (int i = 1; i <= count; i++)
            {
                dynamic c = circles.Item(i);
                double cx = GetPtX(c.CenterSketchPoint);
                double cy = GetPtY(c.CenterSketchPoint);
                double r = SafeDouble(c.Radius, 1.0);
                entities.Add(new Dictionary<string, object?>
                {
                    ["type"] = "circle",
                    ["cx"] = cx,
                    ["cy"] = cy,
                    ["radius"] = r
                });
            }
        }
        catch (Exception ex)
        {
            warnings.Add($"SketchCircles traversal warning: {ex.Message}");
        }

        // Arcs
        try
        {
            dynamic arcs = planarSketch.SketchArcs;
            int count = arcs?.Count ?? 0;
            for (int i = 1; i <= count; i++)
            {
                dynamic a = arcs.Item(i);
                double cx = GetPtX(a.CenterSketchPoint);
                double cy = GetPtY(a.CenterSketchPoint);
                double r = SafeDouble(a.Radius, 1.0);

                // Compute angles from start/end points relative to center (degrees)
                double sx = GetPtX(a.StartSketchPoint) - cx;
                double sy = GetPtY(a.StartSketchPoint) - cy;
                double ex = GetPtX(a.EndSketchPoint) - cx;
                double ey = GetPtY(a.EndSketchPoint) - cy;

                double startDeg = Math.Atan2(sy, sx) * 180.0 / Math.PI;
                double endDeg = Math.Atan2(ey, ex) * 180.0 / Math.PI;

                entities.Add(new Dictionary<string, object?>
                {
                    ["type"] = "arc",
                    ["cx"] = cx,
                    ["cy"] = cy,
                    ["radius"] = r,
                    ["start_angle"] = startDeg,
                    ["end_angle"] = endDeg
                });
            }
        }
        catch (Exception ex)
        {
            warnings.Add($"SketchArcs traversal warning: {ex.Message}");
        }

        // Points
        try
        {
            dynamic points = planarSketch.SketchPoints;
            int count = points?.Count ?? 0;
            for (int i = 1; i <= count; i++)
            {
                dynamic p = points.Item(i);
                entities.Add(new Dictionary<string, object?>
                {
                    ["type"] = "point",
                    ["x"] = GetPtX(p),
                    ["y"] = GetPtY(p)
                });
            }
        }
        catch (Exception ex)
        {
            warnings.Add($"SketchPoints traversal warning: {ex.Message}");
        }

        // Ellipses (major/minor + optional angle)
        try
        {
            dynamic ellipses = planarSketch.SketchEllipses;
            int count = ellipses?.Count ?? 0;
            for (int i = 1; i <= count; i++)
            {
                dynamic e = ellipses.Item(i);
                double cx = GetPtX(e.CenterSketchPoint);
                double cy = GetPtY(e.CenterSketchPoint);
                double major = SafeDouble(e.MajorRadius ?? e.MajorAxisRadius, 1.0);
                double minor = SafeDouble(e.MinorRadius ?? e.MinorAxisRadius, major * 0.5);
                entities.Add(new Dictionary<string, object?>
                {
                    ["type"] = "ellipse",
                    ["cx"] = cx,
                    ["cy"] = cy,
                    ["major_radius"] = major,
                    ["minor_radius"] = minor
                });
            }
        }
        catch (Exception ex)
        {
            warnings.Add($"SketchEllipses traversal warning: {ex.Message}");
        }

        // Splines (best-effort: emit control points if accessible; god macro accepts "points" string or array)
        try
        {
            dynamic splines = planarSketch.SketchSplines;
            int count = splines?.Count ?? 0;
            for (int i = 1; i <= count; i++)
            {
                dynamic s = splines.Item(i);
                // Try to extract fit points or control points; fall back to empty points (user can edit template)
                var pts = new List<double>();
                try
                {
                    dynamic fitPoints = s.FitPoints;
                    for (int j = 1; j <= (fitPoints?.Count ?? 0); j++)
                    {
                        dynamic fp = fitPoints.Item(j);
                        pts.Add(GetPtX(fp));
                        pts.Add(GetPtY(fp));
                    }
                }
                catch { /* ignore */ }

                entities.Add(new Dictionary<string, object?>
                {
                    ["type"] = "spline",
                    ["points"] = string.Join(",", pts),
                    ["fit_method"] = "sweet"
                });
            }
        }
        catch (Exception ex)
        {
            warnings.Add($"SketchSplines traversal warning: {ex.Message}");
        }

        if (entities.Count == 0 && warnings.Count == 0)
            warnings.Add("Sketch contained no supported entities (lines/circles/arcs/points/ellipses/splines).");

        return (entities, warnings);
    }

    private static double GetPtX(dynamic pt)
    {
        try
        {
            var geom = pt?.Geometry;
            if (geom != null)
                return Convert.ToDouble(geom.X ?? geom.x ?? 0);
            return Convert.ToDouble(pt?.X ?? 0);
        }
        catch { return 0.0; }
    }

    private static double GetPtY(dynamic pt)
    {
        try
        {
            var geom = pt?.Geometry;
            if (geom != null)
                return Convert.ToDouble(geom.Y ?? geom.y ?? 0);
            return Convert.ToDouble(pt?.Y ?? 0);
        }
        catch { return 0.0; }
    }

    private static double SafeDouble(object? v, double def)
    {
        if (v is double d) return d;
        if (v is float f) return f;
        if (v != null && double.TryParse(v.ToString(), out var p)) return p;
        return def;
    }
}
