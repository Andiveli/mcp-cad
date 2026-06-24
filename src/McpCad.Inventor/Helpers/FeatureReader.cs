using System;
using System.Collections.Generic;

namespace McpCad.Inventor.Helpers;

/// <summary>
/// COM traversal for reading existing PartFeature objects into typed descriptors
/// expected by macro_god_part (features[] JSON) and template capture/run.
///
/// Must live in Inventor project (dynamic COM + interop access).
/// Follows patterns from SketchReader (1-based Item, WrapDispatch where needed,
/// per-collection try/catch, non-fatal warnings).
///
/// Walks in creational order using the master Features collection, then extracts
/// type-specific parameters from the concrete feature/definition objects.
/// </summary>
public static class FeatureReader
{
    /// <summary>
    /// Traverse the feature tree in creation order and return list of feature descriptors
    /// + non-fatal warnings.
    ///
    /// Each descriptor is a Dictionary with at minimum:
    ///   "feature_type": string (lowercase, e.g. "extrude", "fillet")
    /// plus type-dependent params (distance, edges, radius, profile, axis, etc.).
    ///
    /// Unsupported feature types produce a warning entry and are skipped (capture continues).
    /// </summary>
    public static (List<Dictionary<string, object?>> Features, List<string> Warnings)
        ReadFeatures(dynamic compDef)
    {
        var features = new List<Dictionary<string, object?>>();
        var warnings = new List<string>();

        if (compDef is null)
        {
            warnings.Add("No component definition provided to ReadFeatures.");
            return (features, warnings);
        }

        try
        {
            dynamic partFeatures = compDef.Features;
            if (partFeatures == null)
            {
                warnings.Add("Component definition has no Features collection.");
                return (features, warnings);
            }

            int count = 0;
            try { count = partFeatures.Count; } catch { }

            for (int i = 1; i <= count; i++)
            {
                dynamic f;
                try
                {
                    f = partFeatures.Item(i);
                }
                catch (Exception ex)
                {
                    warnings.Add($"Feature {i} access warning: {ex.Message}");
                    continue;
                }

                if (f == null) continue;

                string name = SafeString(f.Name, $"Feature{i}");
                string subType = "";
                try { subType = f.SubType?.ToString() ?? f.Type?.ToString() ?? ""; } catch { }

                // Determine feature type from SubType or by probing collections later.
                // For now we use the general feature object and try to extract common + typed data.
                var descriptor = BuildFeatureDescriptor(f, name, subType, warnings);
                if (descriptor != null)
                {
                    features.Add(descriptor);
                }
            }
        }
        catch (Exception ex)
        {
            warnings.Add($"Features traversal warning: {ex.Message}");
        }

        if (features.Count == 0 && warnings.Count == 0)
        {
            warnings.Add("Part contained no supported features or feature tree was empty.");
        }

        return (features, warnings);
    }

    private static Dictionary<string, object?>? BuildFeatureDescriptor(dynamic f, string name, string subType, List<string> warnings)
    {
        // Try to get a human-readable type name
        string typeStr = "";
        try
        {
            // Inventor features often expose SubType as a GUID or friendly name; fall back to class name
            typeStr = (f.SubType?.ToString() ?? "").ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(typeStr))
                typeStr = f.GetType().Name?.ToString()?.ToLowerInvariant() ?? "";
        }
        catch { }

        // Common fields we always try to capture
        var desc = new Dictionary<string, object?>
        {
            ["name"] = name,
            ["feature_type"] = MapFeatureType(typeStr, f),
        };

        string ft = desc["feature_type"]?.ToString() ?? "unknown";

        try
        {
            // Extract type-specific parameters. Order of ifs matters for patterns vs base features.
            if (ft == "extrude" || typeStr.Contains("extrude"))
            {
                desc["feature_type"] = "extrude";
                TryExtractExtrude(f, desc);
            }
            else if (ft == "revolve" || typeStr.Contains("revolve"))
            {
                desc["feature_type"] = "revolve";
                TryExtractRevolve(f, desc);
            }
            else if (ft == "fillet" || typeStr.Contains("fillet"))
            {
                desc["feature_type"] = "fillet";
                TryExtractFillet(f, desc);
            }
            else if (ft == "chamfer" || typeStr.Contains("chamfer"))
            {
                desc["feature_type"] = "chamfer";
                TryExtractChamfer(f, desc);
            }
            else if (ft == "hole" || typeStr.Contains("hole"))
            {
                desc["feature_type"] = "hole";
                TryExtractHole(f, desc);
            }
            else if (ft == "thread" || typeStr.Contains("thread"))
            {
                desc["feature_type"] = "thread";
                TryExtractThread(f, desc);
            }
            else if (ft == "shell" || typeStr.Contains("shell"))
            {
                desc["feature_type"] = "shell";
                TryExtractShell(f, desc);
            }
            else if (ft == "draft" || typeStr.Contains("draft"))
            {
                desc["feature_type"] = "draft";
                TryExtractDraft(f, desc);
            }
            else if (ft == "circular_pattern" || typeStr.Contains("circularpattern"))
            {
                desc["feature_type"] = "circular_pattern";
                TryExtractCircularPattern(f, desc);
            }
            else if (ft == "rectangular_pattern" || typeStr.Contains("rectangularpattern"))
            {
                desc["feature_type"] = "rectangular_pattern";
                TryExtractRectangularPattern(f, desc);
            }
            else if (ft == "mirror_feature" || typeStr.Contains("mirror"))
            {
                desc["feature_type"] = "mirror_feature";
                TryExtractMirror(f, desc);
            }
            else if (ft == "sweep" || typeStr.Contains("sweep"))
            {
                desc["feature_type"] = "sweep";
                TryExtractSweep(f, desc);
            }
            else if (ft == "loft" || typeStr.Contains("loft"))
            {
                desc["feature_type"] = "loft";
                TryExtractLoft(f, desc);
            }
            else if (ft == "coil" || typeStr.Contains("coil"))
            {
                desc["feature_type"] = "coil";
                TryExtractCoil(f, desc);
            }
            else if (ft == "rib" || typeStr.Contains("rib"))
            {
                desc["feature_type"] = "rib";
                TryExtractRib(f, desc);
            }
            else if (ft == "split" || typeStr.Contains("split"))
            {
                desc["feature_type"] = "split";
                TryExtractSplit(f, desc);
            }
            else if (ft == "combine" || typeStr.Contains("combine"))
            {
                desc["feature_type"] = "combine";
                TryExtractCombine(f, desc);
            }
            else if (ft == "thicken" || typeStr.Contains("thicken"))
            {
                desc["feature_type"] = "thicken";
                TryExtractThicken(f, desc);
            }
            else if (ft == "emboss" || typeStr.Contains("emboss"))
            {
                desc["feature_type"] = "emboss";
                TryExtractEmboss(f, desc);
            }
            else if (ft == "derive" || typeStr.Contains("derive"))
            {
                desc["feature_type"] = "derive";
                TryExtractDerive(f, desc);
            }
            else
            {
                // Unsupported or unknown — emit warning but still return a minimal descriptor
                // so the caller can decide (per spec we warn and continue)
                warnings.Add($"Unsupported or unrecognized feature type '{typeStr}' for feature '{name}'. Descriptor emitted with minimal data.");
                desc["feature_type"] = "unsupported";
                desc["original_type"] = typeStr;
            }

            // Always try to capture suppressed state
            try { desc["suppressed"] = f.Suppressed; } catch { }
        }
        catch (Exception ex)
        {
            warnings.Add($"Feature '{name}' descriptor extraction warning: {ex.Message}");
            return null; // drop this one but continue
        }

        return desc;
    }

    private static string MapFeatureType(string typeStr, dynamic f)
    {
        if (string.IsNullOrWhiteSpace(typeStr)) return "unknown";
        var t = typeStr.ToLowerInvariant();

        if (t.Contains("extrude")) return "extrude";
        if (t.Contains("revolve")) return "revolve";
        if (t.Contains("fillet")) return "fillet";
        if (t.Contains("chamfer")) return "chamfer";
        if (t.Contains("hole")) return "hole";
        if (t.Contains("thread")) return "thread";
        if (t.Contains("shell")) return "shell";
        if (t.Contains("draft")) return "draft";
        if (t.Contains("circularpattern")) return "circular_pattern";
        if (t.Contains("rectangularpattern")) return "rectangular_pattern";
        if (t.Contains("mirror")) return "mirror_feature";
        if (t.Contains("sweep")) return "sweep";
        if (t.Contains("loft")) return "loft";
        if (t.Contains("coil")) return "coil";
        if (t.Contains("rib")) return "rib";
        if (t.Contains("split")) return "split";
        if (t.Contains("combine")) return "combine";
        if (t.Contains("thicken")) return "thicken";
        if (t.Contains("emboss")) return "emboss";
        if (t.Contains("derive")) return "derive";

        return "unknown";
    }

    // ── Typed extraction helpers (best-effort safe access) ─────────────────

    private static void TryExtractExtrude(dynamic f, Dictionary<string, object?> desc)
    {
        // Profile is usually on Definition or accessible via Profile property
        try
        {
            var prof = f.Profile ?? f.Definition?.Profile;
            desc["profile"] = ResolveProfileIndex(prof) ?? "1";
        }
        catch { desc["profile"] = "1"; }

        try
        {
            dynamic ext = f.Extent ?? f.Definition?.Extent;
            if (ext != null)
            {
                // DistanceExtent or similar
                desc["distance"] = SafeDouble(ext.Distance ?? ext.Length ?? ext.Value, 1.0);
            }
        }
        catch { }

        try { desc["direction"] = MapDirection(f.Direction ?? f.Definition?.Direction); } catch { desc["direction"] = "positive"; }
        try { desc["operation"] = MapOperation(f.Operation ?? f.Definition?.Operation); } catch { desc["operation"] = "new_body"; }
        try
        {
            var taper = f.Taper ?? f.Definition?.Taper;
            desc["taper"] = SafeDouble(taper, 0.0);
        }
        catch { desc["taper"] = 0.0; }
    }

    private static void TryExtractRevolve(dynamic f, Dictionary<string, object?> desc)
    {
        try
        {
            var prof = f.Profile ?? f.Definition?.Profile;
            desc["profile"] = ResolveProfileIndex(prof) ?? "1";
        }
        catch { desc["profile"] = "1"; }

        try { desc["axis"] = ResolveAxis(f.Axis ?? f.Definition?.Axis); } catch { desc["axis"] = "Y"; }
        try
        {
            dynamic angle = f.Angle ?? f.Definition?.Angle ?? f.Extent?.Angle;
            desc["angle"] = SafeDouble(angle, 360.0);
        }
        catch { desc["angle"] = 360.0; }
        try { desc["operation"] = MapOperation(f.Operation ?? f.Definition?.Operation); } catch { desc["operation"] = "join"; }
    }

    private static void TryExtractFillet(dynamic f, Dictionary<string, object?> desc)
    {
        try
        {
            // Fillet features expose EdgeCollection or similar
            var edges = f.Edges ?? f.EdgeCollection ?? f.Definition?.Edges;
            desc["edges"] = ResolveEdgeList(edges) ?? "1";
        }
        catch { desc["edges"] = "1"; }

        try
        {
            var r = f.Radius ?? f.ConstantRadius ?? f.Definition?.Radius;
            desc["radius"] = SafeDouble(r, 0.5);
        }
        catch { desc["radius"] = 0.5; }

        desc["mode"] = "constant"; // v1 default; can be extended for variable-radius later
    }

    private static void TryExtractChamfer(dynamic f, Dictionary<string, object?> desc)
    {
        try
        {
            var edges = f.Edges ?? f.Definition?.Edges;
            desc["edges"] = ResolveEdgeList(edges) ?? "1";
        }
        catch { desc["edges"] = "1"; }

        try
        {
            var d = f.Distance ?? f.Definition?.Distance;
            desc["distance"] = SafeDouble(d, 0.5);
        }
        catch { desc["distance"] = 0.5; }

        desc["mode"] = "equal_distance";
    }

    private static void TryExtractHole(dynamic f, Dictionary<string, object?> desc)
    {
        // Holes are often placed via sketch points; best-effort coords
        try
        {
            // Many holes have Placement or SketchPoint
            var pt = f.PlacementPoint ?? f.Definition?.PlacementPoint ?? f.SketchPoint;
            if (pt != null)
            {
                desc["x"] = SafeDouble(pt.X ?? pt.x, 0.0);
                desc["y"] = SafeDouble(pt.Y ?? pt.y, 0.0);
            }
        }
        catch { desc["x"] = 0; desc["y"] = 0; }

        try
        {
            var diam = f.Diameter ?? f.Definition?.Diameter;
            desc["diameter"] = SafeDouble(diam, 0.5);
        }
        catch { desc["diameter"] = 0.5; }

        try
        {
            var depth = f.Depth ?? f.Definition?.Depth ?? f.Extent?.Distance;
            desc["depth"] = SafeDouble(depth, 1.0);
        }
        catch { desc["depth"] = 1.0; }

        desc["type"] = "drilled";
    }

    private static void TryExtractThread(dynamic f, Dictionary<string, object?> desc)
    {
        try
        {
            var face = f.Face ?? f.ThreadedFace ?? f.Definition?.Face;
            desc["face"] = ResolveFace(face) ?? "1";
        }
        catch { desc["face"] = "1"; }

        try { desc["specification"] = (f.ThreadInfo?.Designation ?? f.Specification ?? "M10x1.5").ToString(); } catch { desc["specification"] = "M10x1.5"; }
        try { desc["direction"] = (f.Direction ?? f.ThreadDirection ?? "right").ToString().ToLower(); } catch { desc["direction"] = "right"; }
    }

    private static void TryExtractShell(dynamic f, Dictionary<string, object?> desc)
    {
        try
        {
            var faces = f.Faces ?? f.RemovedFaces ?? f.Definition?.RemovedFaces;
            desc["faces"] = ResolveFaceList(faces) ?? "1";
        }
        catch { desc["faces"] = "1"; }

        try
        {
            var t = f.Thickness ?? f.Definition?.Thickness;
            desc["thickness"] = SafeDouble(t, 0.2);
        }
        catch { desc["thickness"] = 0.2; }

        desc["direction"] = "inside";
    }

    private static void TryExtractDraft(dynamic f, Dictionary<string, object?> desc)
    {
        try
        {
            var faces = f.Faces ?? f.Definition?.Faces;
            desc["faces"] = ResolveFaceList(faces) ?? "1";
        }
        catch { desc["faces"] = "1"; }

        try { desc["angle"] = SafeDouble(f.Angle ?? f.Definition?.Angle, 5.0); } catch { desc["angle"] = 5.0; }
        desc["pull_direction"] = "z";
        desc["fixed_entity"] = "";
    }

    private static void TryExtractCircularPattern(dynamic f, Dictionary<string, object?> desc)
    {
        try
        {
            var prof = f.Profiles ?? f.Definition?.Profiles ?? f.ParentFeature;
            desc["profile"] = ResolveProfileIndex(prof) ?? "1";
        }
        catch { desc["profile"] = "1"; }

        try { desc["axis"] = ResolveAxis(f.Axis ?? f.Definition?.Axis); } catch { desc["axis"] = "Y"; }
        try { desc["count"] = SafeInt(f.Count ?? f.Definition?.Count, 6); } catch { desc["count"] = 6; }
        try { desc["angle"] = SafeDouble(f.Angle ?? f.Definition?.Angle, 360.0); } catch { desc["angle"] = 360.0; }

        // NOTE (v1 limitation): parent_feature_index deliberately omitted.
        // Proper index resolution requires walking the Features collection by name match.
        // Tracked for future work; see design review for template-full-part.
    }

    private static void TryExtractRectangularPattern(dynamic f, Dictionary<string, object?> desc)
    {
        try
        {
            var prof = f.Profiles ?? f.Definition?.Profiles;
            desc["profile"] = ResolveProfileIndex(prof) ?? "1";
        }
        catch { desc["profile"] = "1"; }

        try { desc["x_count"] = SafeInt(f.XCount ?? f.Definition?.XCount, 2); } catch { desc["x_count"] = 2; }
        try { desc["x_spacing"] = SafeDouble(f.XSpacing ?? f.Definition?.XSpacing, 1.0); } catch { desc["x_spacing"] = 1.0; }
        try { desc["y_count"] = SafeInt(f.YCount ?? f.Definition?.YCount, 1); } catch { desc["y_count"] = 1; }
        try { desc["y_spacing"] = SafeDouble(f.YSpacing ?? f.Definition?.YSpacing, 0.0); } catch { desc["y_spacing"] = 0.0; }
    }

    private static void TryExtractMirror(dynamic f, Dictionary<string, object?> desc)
    {
        try
        {
            var prof = f.Profiles ?? f.Definition?.Profiles ?? f.ParentFeature;
            desc["profile"] = ResolveProfileIndex(prof) ?? "1";
        }
        catch { desc["profile"] = "1"; }

        try { desc["mirror_plane"] = ResolvePlane(f.Plane ?? f.Definition?.Plane ?? "XZ"); } catch { desc["mirror_plane"] = "XZ"; }
    }

    private static void TryExtractSweep(dynamic f, Dictionary<string, object?> desc)
    {
        try
        {
            var prof = f.Profile ?? f.Definition?.Profile;
            desc["profile"] = ResolveProfileIndex(prof) ?? "1";
        }
        catch { desc["profile"] = "1"; }

        try
        {
            var path = f.Path ?? f.Definition?.Path;
            desc["path"] = ResolvePath(path) ?? "1";
        }
        catch { desc["path"] = "1"; }

        try { desc["operation"] = MapOperation(f.Operation ?? f.Definition?.Operation); } catch { desc["operation"] = "new_body"; }
        desc["taper"] = 0.0;
    }

    private static void TryExtractLoft(dynamic f, Dictionary<string, object?> desc)
    {
        try
        {
            var profs = f.Profiles ?? f.Sections ?? f.Definition?.Profiles;
            desc["profiles"] = ResolveProfileList(profs) ?? "1,2";
        }
        catch { desc["profiles"] = "1,2"; }

        try { desc["operation"] = MapOperation(f.Operation ?? f.Definition?.Operation); } catch { desc["operation"] = "new_body"; }
    }

    private static void TryExtractCoil(dynamic f, Dictionary<string, object?> desc)
    {
        try
        {
            var prof = f.Profile ?? f.Definition?.Profile;
            desc["profile"] = ResolveProfileIndex(prof) ?? "1";
        }
        catch { desc["profile"] = "1"; }

        try { desc["axis"] = ResolveAxis(f.Axis ?? f.Definition?.Axis); } catch { desc["axis"] = "Y"; }
        try { desc["pitch"] = SafeDouble(f.Pitch ?? f.Definition?.Pitch, 1.0); } catch { desc["pitch"] = 1.0; }
        try { desc["revolutions"] = SafeDouble(f.Revolutions ?? f.Definition?.Revolutions, 5.0); } catch { desc["revolutions"] = 5.0; }
    }

    private static void TryExtractRib(dynamic f, Dictionary<string, object?> desc)
    {
        try
        {
            var prof = f.Profile ?? f.Definition?.Profile;
            desc["profile"] = ResolveProfileIndex(prof) ?? "1";
        }
        catch { desc["profile"] = "1"; }

        try { desc["thickness"] = SafeDouble(f.Thickness ?? f.Definition?.Thickness, 0.5); } catch { desc["thickness"] = 0.5; }
        desc["direction"] = "normal";
    }

    private static void TryExtractSplit(dynamic f, Dictionary<string, object?> desc)
    {
        try { desc["split_tool"] = ResolvePlane(f.SplitTool ?? f.Definition?.SplitTool ?? "WorkPlane1"); } catch { desc["split_tool"] = "WorkPlane1"; }
        desc["remove_side"] = "positive";
    }

    private static void TryExtractCombine(dynamic f, Dictionary<string, object?> desc)
    {
        try { desc["base_body"] = ResolveBody(f.BaseBody ?? f.Definition?.BaseBody ?? "1"); } catch { desc["base_body"] = "1"; }
        try { desc["tool_bodies"] = ResolveBodyList(f.ToolBodies ?? f.Definition?.ToolBodies ?? "2"); } catch { desc["tool_bodies"] = "2"; }
        try { desc["operation"] = MapOperation(f.Operation ?? f.Definition?.Operation); } catch { desc["operation"] = "join"; }
    }

    private static void TryExtractThicken(dynamic f, Dictionary<string, object?> desc)
    {
        try
        {
            var faces = f.Faces ?? f.Definition?.Faces;
            desc["faces"] = ResolveFaceList(faces) ?? "1";
        }
        catch { desc["faces"] = "1"; }

        try { desc["thickness"] = SafeDouble(f.Thickness ?? f.Definition?.Thickness, 0.2); } catch { desc["thickness"] = 0.2; }
        desc["direction"] = "positive";
    }

    private static void TryExtractEmboss(dynamic f, Dictionary<string, object?> desc)
    {
        try
        {
            var prof = f.Profile ?? f.Definition?.Profile;
            desc["profile"] = ResolveProfileIndex(prof) ?? "1";
        }
        catch { desc["profile"] = "1"; }

        try { desc["depth"] = SafeDouble(f.Depth ?? f.Definition?.Depth, 0.3); } catch { desc["depth"] = 0.3; }
        desc["type"] = "emboss_from_face";
    }

    private static void TryExtractDerive(dynamic f, Dictionary<string, object?> desc)
    {
        try
        {
            var src = f.ReferencedDocumentDescriptor?.FullDocumentName ?? f.SourcePath ?? f.Definition?.SourcePath;
            desc["source_path"] = src?.ToString() ?? "";
        }
        catch { desc["source_path"] = ""; }
    }

    // ── Safe value + resolution helpers (mirrors SketchReader style) ────────

    private static string SafeString(object? v, string def)
    {
        if (v is string s) return s;
        if (v != null) return v.ToString() ?? def;
        return def;
    }

    private static double SafeDouble(object? v, double def)
    {
        if (v is double d) return d;
        if (v is float f) return f;
        if (v is int i) return i;
        if (v != null && double.TryParse(v.ToString(), out var p)) return p;
        return def;
    }

    private static int SafeInt(object? v, int def)
    {
        if (v is int i) return i;
        if (v is double d) return (int)d;
        if (v != null && int.TryParse(v.ToString(), out var p)) return p;
        return def;
    }

    private static string MapDirection(object? dir)
    {
        if (dir == null) return "positive";
        var s = dir.ToString()?.ToLowerInvariant() ?? "";
        if (s.Contains("neg")) return "negative";
        if (s.Contains("sym")) return "symmetric";
        return "positive";
    }

    private static string MapOperation(object? op)
    {
        if (op == null) return "new_body";
        var s = op.ToString()?.ToLowerInvariant() ?? "";
        if (s.Contains("join") || s.Contains("add")) return "join";
        if (s.Contains("cut") || s.Contains("subtract")) return "cut";
        if (s.Contains("intersect")) return "intersect";
        return "new_body";
    }

    private static string? ResolveProfileIndex(dynamic? prof)
    {
        if (prof == null) return null;
        try
        {
            // If it's a collection, take first. Use safe count (some profiles are single objects, not collections).
            int c = 0;
            try { c = prof.Count; } catch { c = 0; }
            if (c > 0)
            {
                var p0 = prof.Item(1);
                return p0?.Name?.ToString() ?? "1";
            }
            return prof.Name?.ToString() ?? prof.Index?.ToString() ?? "1";
        }
        catch { return "1"; }
    }

    private static string? ResolveProfileList(dynamic? profs)
    {
        if (profs == null) return null;
        try
        {
            var list = new List<string>();
            int c = 0; try { c = profs.Count; } catch { }
            for (int i = 1; i <= c; i++)
            {
                var p = profs.Item(i);
                list.Add(p?.Name?.ToString() ?? i.ToString());
            }
            return string.Join(",", list);
        }
        catch { return null; }
    }

    private static string? ResolveEdgeList(dynamic? edges)
    {
        if (edges == null) return null;
        try
        {
            var list = new List<string>();
            int c = 0; try { c = edges.Count; } catch { }
            for (int i = 1; i <= c; i++)
            {
                list.Add(i.ToString());
            }
            return list.Count > 0 ? string.Join(",", list) : "1";
        }
        catch { return "1"; }
    }

    private static string? ResolveFaceList(dynamic? faces)
    {
        if (faces == null) return null;
        try
        {
            var list = new List<string>();
            int c = 0; try { c = faces.Count; } catch { }
            for (int i = 1; i <= c; i++) list.Add(i.ToString());
            return list.Count > 0 ? string.Join(",", list) : "1";
        }
        catch { return "1"; }
    }

    private static string? ResolveFace(dynamic? face)
    {
        if (face == null) return null;
        try { return face.Name?.ToString() ?? "1"; } catch { return "1"; }
    }

    private static string ResolveAxis(dynamic? axis)
    {
        if (axis == null) return "Y";
        try
        {
            var n = axis.Name?.ToString()?.ToUpper() ?? "";
            if (n.Contains("X")) return "X";
            if (n.Contains("Z")) return "Z";
            return "Y";
        }
        catch { return "Y"; }
    }

    private static string ResolvePlane(dynamic? plane)
    {
        if (plane == null) return "XZ";
        try
        {
            var n = plane.Name?.ToString()?.ToUpper() ?? "";
            if (n.Contains("XY")) return "XY";
            if (n.Contains("YZ")) return "YZ";
            return "XZ";
        }
        catch { return "XZ"; }
    }

    private static string? ResolvePath(dynamic? path)
    {
        if (path == null) return null;
        try { return path.Name?.ToString() ?? "1"; } catch { return "1"; }
    }

    private static string? ResolveBody(dynamic? body)
    {
        if (body == null) return null;
        try { return body.Name?.ToString() ?? "1"; } catch { return "1"; }
    }

    private static string? ResolveBodyList(dynamic? bodies)
    {
        if (bodies == null) return null;
        try
        {
            var list = new List<string>();
            int c = 0; try { c = bodies.Count; } catch { }
            for (int i = 1; i <= c; i++) list.Add(i.ToString());
            return string.Join(",", list);
        }
        catch { return "2"; }
    }

    // NOTE: ResolveFeatureIndex removed for v1.
    // parent_feature_index is a known limitation (see design review).
    // Proper implementation would walk the Features collection by name to find index.
}
