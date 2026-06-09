using McpCad.Core;
using McpCad.Core.Exceptions;
using ModelContextProtocol.Server;
using System.ComponentModel;

namespace McpCad.Tools;

/// <summary>
/// Skill tools — composed MCP operations that combine multiple atomic provider calls.
/// Skills are higher-level workflows (e.g., skill_revolve = sketch + circle + line + revolve).
/// Each method delegates to ICadProvider and catches COM exceptions,
/// returning standardized error dictionaries per the error contract (D7).
/// Tool method names use snake_case to match Python tool names (S5.1).
/// </summary>
[McpServerToolType]
public class SkillTools(IMechanicalCadProvider provider)
{
    // ── Drawing skills (8) ──────────────────────────────────────────────

    [McpServerTool, Description("Create or activate a sketch on a work plane.")]
    public Dictionary<string, object?> skill_sketch(string plane = "XY")
        => Catch(() => provider.SketchCreate(plane));

    [McpServerTool, Description("Draw a line. Modes: simple (start→end) or midpoint (centered line).")]
    public Dictionary<string, object?> skill_line(
        string mode = "simple",
        double start_x = 0.0, double start_y = 0.0,
        double end_x = 0.0, double end_y = 0.0,
        double mid_x = 0.0, double mid_y = 0.0)
    {
        return Catch(() =>
        {
            if (mode == "simple")
                return provider.SketchLine(start_x, start_y, end_x, end_y);

            if (mode == "midpoint")
            {
                // Midpoint line: compute opposite endpoint from center
                double oppX = 2.0 * mid_x - end_x;
                double oppY = 2.0 * mid_y - end_y;
                return provider.SketchLine(oppX, oppY, end_x, end_y);
            }

            return ToolHelpers.Error($"Unknown mode '{mode}'. Use 'simple' or 'midpoint'.");
        });
    }

    [McpServerTool, Description("Draw a circle. Modes: center (cx,cy,radius) or 3point (through 3 perimeter points).")]
    public Dictionary<string, object?> skill_circle(
        string mode = "center",
        double cx = 0.0, double cy = 0.0, double radius = 1.0,
        double x1 = 0.0, double y1 = 0.0,
        double x2 = 0.0, double y2 = 0.0,
        double x3 = 0.0, double y3 = 0.0)
    {
        return Catch(() =>
        {
            if (mode == "center")
                return provider.SketchCircle(cx, cy, radius);

            if (mode == "3point")
            {
                var (calcCx, calcCy, calcR) = CircleFromThreePoints(x1, y1, x2, y2, x3, y3);
                return provider.SketchCircle(calcCx, calcCy, calcR);
            }

            return ToolHelpers.Error($"Unknown mode '{mode}'. Use 'center' or '3point'.");
        });
    }

    [McpServerTool, Description("Draw an arc. Modes: center (cx,sy,ex), sweep (radius+angles), or 3point.")]
    public Dictionary<string, object?> skill_arc(
        string mode = "center",
        double cx = 0.0, double cy = 0.0,
        double sx = 1.0, double sy = 0.0,
        double ex = 0.0, double ey = 1.0,
        bool ccw = true,
        double radius = 1.0, double start_angle = 0.0, double sweep_angle = 90.0,
        double x1 = 0.0, double y1 = 0.0,
        double x_mid = 0.0, double y_mid = 0.0,
        double x_end = 0.0, double y_end = 0.0)
    {
        return Catch(() =>
        {
            if (mode == "center")
            {
                double r = Math.Sqrt((sx - cx) * (sx - cx) + (sy - cy) * (sy - cy));
                double saRad = Math.Atan2(sy - cy, sx - cx);
                double swRad = SweepBetween(cx, cy, sx, sy, ex, ey, ccw);
                return provider.SketchArc(cx, cy, r, saRad * 180.0 / Math.PI, (saRad + swRad) * 180.0 / Math.PI);
            }

            if (mode == "sweep")
            {
                return provider.SketchArc(cx, cy, radius, start_angle, start_angle + sweep_angle);
            }

            if (mode == "3point")
            {
                var (acx, acy, ar) = CircleFromThreePoints(x1, y1, x_mid, y_mid, x_end, y_end);
                double saRad = Math.Atan2(y1 - acy, x1 - acx);
                double eaRad = Math.Atan2(y_end - acy, x_end - acx);
                double maRad = Math.Atan2(y_mid - acy, x_mid - acx);
                // Ensure arc passes through midpoint
                if (eaRad <= saRad) eaRad += 2.0 * Math.PI;
                if (maRad <= saRad) maRad += 2.0 * Math.PI;
                if (maRad > eaRad) eaRad -= 2.0 * Math.PI;
                return provider.SketchArc(acx, acy, ar, saRad * 180.0 / Math.PI, eaRad * 180.0 / Math.PI);
            }

            return ToolHelpers.Error($"Unknown mode '{mode}'. Use 'center', 'sweep', or '3point'.");
        });
    }

    [McpServerTool, Description("Draw a rectangle. Modes: diagonal (two corners) or center (center+corner).")]
    public Dictionary<string, object?> skill_rect(
        string mode = "diagonal",
        double x1 = 0.0, double y1 = 0.0,
        double x2 = 10.0, double y2 = 10.0,
        double cx = 0.0, double cy = 0.0,
        double corner_x = 5.0, double corner_y = 5.0)
    {
        return Catch(() =>
        {
            if (mode == "diagonal")
                return provider.SketchRectangle(x1, y1, x2, y2);

            if (mode == "center")
            {
                double oppX = 2.0 * cx - corner_x;
                double oppY = 2.0 * cy - corner_y;
                return provider.SketchRectangle(corner_x, corner_y, oppX, oppY);
            }

            return ToolHelpers.Error($"Unknown mode '{mode}'. Use 'diagonal' or 'center'.");
        });
    }

    [McpServerTool, Description("Draw a point.")]
    public Dictionary<string, object?> skill_point(double x = 0.0, double y = 0.0)
        => Catch(() => provider.SketchPoint(x, y));

    [McpServerTool, Description("Draw an ellipse. Center (cx,cy), major and minor radii, optional angle.")]
    public Dictionary<string, object?> skill_ellipse(
        double cx = 0.0, double cy = 0.0,
        double major_radius = 5.0, double minor_radius = 3.0,
        double angle = 0.0)
        => Catch(() => provider.SketchEllipse(cx, cy, major_radius, minor_radius, angle));

    [McpServerTool, Description("Draw a spline through fit points. Points format: 'x1,y1,x2,y2,...'")]
    public Dictionary<string, object?> skill_spline(string points = "", string fit_method = "sweet")
    {
        return Catch(() =>
        {
            if (string.IsNullOrWhiteSpace(points))
                return ToolHelpers.Error("Need at least 3 points for a spline.");
            return provider.SketchSpline(points, fit_method);
        });
    }

    // ── Pattern skills (2) ───────────────────────────────────────────────

    [McpServerTool, Description("Circular pattern of sketch entities around an axis point.")]
    public Dictionary<string, object?> skill_pattern_circular(
        string entities = "1", string axis = "1", int count = 6,
        double angle = 360.0, bool fitted = true, bool symmetric = false)
        => Catch(() => provider.SketchCircularPattern(entities, axis, count, angle, fitted, symmetric));

    [McpServerTool, Description("Rectangular pattern of sketch entities.")]
    public Dictionary<string, object?> skill_pattern_rectangular(
        string entities = "1", string x_axis = "1", int x_count = 2, double x_spacing = 5.0,
        string y_axis = "", int y_count = 1, double y_spacing = 0.0)
        => Catch(() => provider.SketchRectangularPattern(entities, x_axis, x_count, x_spacing, y_axis, y_count, y_spacing));

    // ── Modify skills (6) ───────────────────────────────────────────────

    [McpServerTool, Description("Move sketch entities by a vector (cm).")]
    public Dictionary<string, object?> skill_move(string entities = "1", double dx = 0.0, double dy = 0.0, bool copy = false)
        => Catch(() => provider.SketchMove(entities, dx, dy, copy));

    [McpServerTool, Description("Rotate sketch entities around a center point (degrees).")]
    public Dictionary<string, object?> skill_rotate(string entities = "1", double cx = 0.0, double cy = 0.0, double angle = 90.0, bool copy = false)
        => Catch(() => provider.SketchRotate(entities, cx, cy, angle, copy));

    [McpServerTool, Description("Scale sketch entities around a center point.")]
    public Dictionary<string, object?> skill_scale(string entities = "1", double cx = 0.0, double cy = 0.0, double factor = 2.0)
        => Catch(() => provider.SketchScale(entities, cx, cy, factor));

    [McpServerTool, Description("Offset sketch entities through a point (cm).")]
    public Dictionary<string, object?> skill_offset(string entities = "1", double offset_x = 0.0, double offset_y = 1.0, bool include_connected = false)
        => Catch(() => provider.SketchOffset(entities, offset_x, offset_y, include_connected));

    [McpServerTool, Description("Mirror sketch entities across a mirror line.")]
    public Dictionary<string, object?> skill_mirror(string entities = "1", string mirror_entity = "2")
        => Catch(() => provider.SketchMirror(entities, mirror_entity));

    [McpServerTool, Description("Trim a sketch entity to its intersection with another.")]
    public Dictionary<string, object?> skill_trim(string entity = "1", string cutting_entity = "2", string side = "end")
        => Catch(() => provider.SketchTrim(entity, cutting_entity, side));

    // ── Constraint + dimension skills (2) ────────────────────────────────

    [McpServerTool, Description("Add a geometric constraint. Modes: coincident, collinear, concentric, parallel, perpendicular, tangent, horizontal, vertical, equal, midpoint, symmetric, smooth.")]
    public Dictionary<string, object?> skill_constraint(
        string mode = "parallel", string entity1 = "1", string entity2 = "",
        string sym_line = "", string axis = "major")
        => Catch(() => provider.SketchConstraint(mode, entity1, entity2, sym_line, axis));

    [McpServerTool, Description("Add a dimension constraint. Modes: linear, radius, diameter, angle.")]
    public Dictionary<string, object?> skill_dimension(
        string mode = "linear", string entity1 = "1", string entity2 = "",
        double? value = null, string orientation = "aligned",
        double? position_x = null, double? position_y = null)
        => Catch(() => provider.SketchDimension(mode, entity1, entity2, value, orientation, position_x, position_y));

    // ── 3D skills (2) ───────────────────────────────────────────────────

    [McpServerTool, Description("Extrude a sketch profile. Auto-defaults to profile=1. Use profile=\"2,4\" for multi-region extrusion.")]
    public Dictionary<string, object?> skill_extrude(
        double distance,
        string direction = "positive",
        double taper = 0.0,
        string operation = "new_body",
        string profile = "1")
        => Catch(() => provider.Extrude(profile, distance, direction, taper, operation));

    [McpServerTool, Description("Revolve a profile around an axis. Auto-draws circle + axis if no profile provided.")]
    public Dictionary<string, object?> skill_revolve(
        string plane = "XY",
        string profile = "",
        double profile_cx = 3.0, double profile_cy = 0.0, double profile_radius = 1.0,
        double axis_x = 0.0, double axis_y1 = -1.0, double axis_y2 = 5.0,
        double angle = 360.0, string operation = "join")
    {
        var results = new Dictionary<string, object?>();

        try
        {
            // 1. Create sketch only when auto-drawing the profile
            if (string.IsNullOrEmpty(profile))
            {
                var sketchResult = provider.SketchCreate(plane);
                results["sketch"] = sketchResult;
                if (sketchResult.TryGetValue("success", out var ok) && ok is bool b && !b)
                    return results;
            }

            // 2. Draw profile if none provided
            string profileIndex = profile;
            if (string.IsNullOrEmpty(profile))
            {
                var profileResult = provider.SketchCircle(profile_cx, profile_cy, profile_radius);
                results["profile"] = profileResult;
                if (profileResult.TryGetValue("success", out var ok2) && ok2 is bool b2 && !b2)
                    return results;
                profileIndex = "1"; // first entity is the circle
            }

            // 3. Draw axis line (vertical at axis_x), tagged as "eje"
            var axisResult = provider.SketchLine(axis_x, axis_y1, axis_x, axis_y2, tag: "eje");
            results["axis"] = axisResult;
            if (axisResult.TryGetValue("success", out var ok3) && ok3 is bool b3 && !b3)
                return results;

            // 4. Revolve using tag reference
            var revolveResult = provider.Revolve(profileIndex, "@eje", angle, operation: operation);
            results["revolve"] = revolveResult;

            return results;
        }
        catch (InventorConnectionException ex)
        {
            return ToolHelpers.Error(ex.Message);
        }
        catch (InventorComException ex)
        {
            return ToolHelpers.Error(ex.Message);
        }
        catch (Exception ex)
        {
            return ToolHelpers.Error($"Unexpected error: {ex.Message}");
        }
    }

    // ── Sweep skill ──────────────────────────────────────────────────────

    [McpServerTool, Description("Sweep a profile along connected sketch entities. Use after creating both a profile sketch and a path sketch.")]
    public Dictionary<string, object?> skill_sweep(
        string profile, string path,
        string sweep_type = "path", string operation = "new_body",
        double taper = 0, string path_sketch = "", string profile_sketch = "")
        => Catch(() => provider.Sweep(profile, path, sweep_type, operation, taper, path_sketch, profile_sketch));

    // ── Weld skill (macrotools entry point) ────────────────────────────
    [McpServerTool, Description("Create a fillet weld (simple wrapper over atomic weld_fillet for macrotool composition). Use after placing parts in a weldment assembly.")]
    public Dictionary<string, object?> skill_weld_fillet(
        string leg_faces1, string leg_faces2, double leg_size,
        double? length = null, bool intermittent = false,
        double? pitch = null, double? gap = null, string? name = null)
        => Catch(() => provider.WeldFillet(leg_faces1, leg_faces2, leg_size, length, intermittent, pitch, gap, name));

    // ── High-level macro: macro_basic_part (core of macro-tools direction) ─
    /// <summary>
    /// High-level server-side macro for creating a basic finished part or adding a simple
    /// extrude/revolve feature to an existing part. One MCP call from the agent performs
    /// the full sequence internally (connect, context-aware fresh/continue, optional profile draw,
    /// profile discovery, feature, mandatory get_feature_tree, best-effort capture).
    /// This is the primary implementation for token/call reduction.
    /// </summary>
    [McpServerTool, Description("High-level macro: creates a basic Inventor part (YZ sketch + closed profile via rect/circle or pre-drawn + extrude/revolve) or adds feature to existing active part. Context-aware (fresh vs continue). Supports any closed profile including compound/hollow (concentric circles for tube, offset rects for hollow box). One call replaces 5-8 atomic calls. Returns envelope with success, geometry_created, document_state, profile_used, operation, tree, warnings, next. Prefer this for finished basic geometry.")]
    public Dictionary<string, object?> macro_basic_part(
        string plane = "YZ",
        string profile = "auto",
        double? width = null,
        double? height = null,
        double? radius = null,
        double? inner_radius = null,
        double? thickness = null,
        double? distance = null,
        double? angle = null,
        string operation = "new_body",
        bool force_new = false)
    {
        var warnings = new List<string>();
        string documentState = "existing";
        string profileUsed = (string.IsNullOrWhiteSpace(profile) || profile.Equals("auto", StringComparison.OrdinalIgnoreCase)) ? "auto-resolved" : profile;
        bool hasTube = radius.HasValue && inner_radius.HasValue;
        bool isRevolve = (angle.HasValue && angle.Value > 0.0001) || hasTube;
        string operationUsed = isRevolve ? "revolve" : "extrude";
        Dictionary<string, object?>? finalTree = null;
        bool geometryCreated = false;
        bool isCompound = false; // set true for hollow box (offset rects)

        try
        {
            // 1. Connect (idempotent)
            var conn = provider.Connect();
            if (conn.TryGetValue("success", out var connOk) && connOk is bool cok && !cok)
            {
                return MakeMacroError("Failed to connect to Inventor. Is Inventor open and with an active document?", geometryCreated: false, documentState: "unknown", profileUsed: profileUsed, operationUsed: operationUsed, warnings: new List<string>(), tree: null);
            }

            // 2. Inspect current state for context awareness (robust for no-document fresh-start case)
            // Use Health() first (safe, never throws on missing doc) to decide whether GetFeatureTree/SketchProfiles are viable.
            // "No active document" / "no component" states are treated as empty so fresh creation branch is reachable.
            bool docHasContent = false;
            bool hasActiveProfiles = false;
            try
            {
                var h = provider.Health();
                bool hasActiveDoc = false;
                if (h.TryGetValue("active_document", out var adObj) && adObj is string adStr && !string.IsNullOrEmpty(adStr))
                    hasActiveDoc = true;

                if (hasActiveDoc)
                {
                    var initialTree = provider.GetFeatureTree();
                    if (initialTree.TryGetValue("feature_count", out var fcObj) && fcObj is int fc && fc > 0)
                        docHasContent = true;
                    else if (initialTree.TryGetValue("features", out var fl) && fl is System.Collections.IList list && list.Count > 0)
                        docHasContent = true;
                }
            }
            catch { /* no active doc or COM issues — fall through to treat as no content */ }

            // P4 (lower priority): lightweight check after Health() for assembly vs part.
            // Limitation (surgical constraint, not obvious w/o bigger changes or other-file schema): prior code in this file only reads "active_document" from Health(); no "document_type"/"is_assembly" usage visible here.
            // We do not add runtime positive detection+error (risk of wrong key or false behavior on assemblies). Assemblies will surface via later sketch/profile or use force_new to override to part.
            // Documented here per rules; no mutation to control flow or new calls.

            try
            {
                var initProf = provider.SketchProfiles();
                if (initProf.TryGetValue("success", out var pOk) && pOk is bool pok && pok &&
                    initProf.TryGetValue("profile_count", out var pcObj) && pcObj is int pci && pci > 0)
                {
                    hasActiveProfiles = true;
                }
            }
            catch { /* no active sketch or not a sketch context */ }

            bool createFresh = force_new || (!hasActiveProfiles && !docHasContent);
            // documentState set to "new" only on successful DocNewPart below (per envelope contract)

            // P3a: for fresh + no width/height/radius, error BEFORE DocNewPart+SketchCreate to avoid leaving skeleton (empty YZ part+sketch) on this core-failure path.
            // Uses early-computed profileUsed/operationUsed (P2) for correct error envelope. createFresh no-dims never reaches profile discovery.
            if (createFresh && !width.HasValue && !height.HasValue && !radius.HasValue && !inner_radius.HasValue && !thickness.HasValue)
            {
                string hint = "Provide width+height (for rect), radius (for circle), radius+inner_radius (for tube), or width+height+thickness (for hollow box) when creating a fresh part, or draw a closed profile with atomic sketch tools before calling the macro.";
                return MakeMacroError("No closed profile detected. " + hint, geometryCreated: false, documentState: documentState, profileUsed: profileUsed, operationUsed: operationUsed, warnings: warnings, tree: finalTree);
            }

            // 3. Fresh creation path (doc + sketch YZ + optional basic profile draw)
            if (createFresh)
            {
                documentState = "new";
                var newDoc = provider.DocNewPart();
                if (!IsSuccess(newDoc))
                    return MakeMacroError("Failed to create new part document.", geometryCreated: false, details: GetErrorMessage(newDoc), documentState: documentState, profileUsed: profileUsed, operationUsed: operationUsed, warnings: warnings, tree: finalTree);

                documentState = "new";  // set only after successful doc creation (for partial success + envelope contract: "new" if we called doc_new_part)
                var sk = provider.SketchCreate(plane);
                if (!IsSuccess(sk))
                    return MakeMacroError($"Failed to create sketch on plane '{plane}'.", geometryCreated: false, details: GetErrorMessage(sk), documentState: documentState, profileUsed: profileUsed, operationUsed: operationUsed, warnings: warnings, tree: finalTree);

                // Draw profile(s) only if dimensions supplied — supports any closed profile shape
                if (radius.HasValue && inner_radius.HasValue)
                {
                    // Tube: draw wall cross-section as rect offset from origin, then revolve around Y axis
                    // This avoids the concentric-circles issue where sketch_profiles doesn't detect the annular region.
                    if (inner_radius.Value >= radius.Value)
                        return MakeMacroError("inner_radius must be less than radius for tube.", geometryCreated: false, documentState: documentState, profileUsed: profileUsed, operationUsed: operationUsed, warnings: warnings, tree: finalTree);
                    if (!distance.HasValue)
                        return MakeMacroError("distance (tube length) is required for tube.", geometryCreated: false, documentState: documentState, profileUsed: profileUsed, operationUsed: operationUsed, warnings: warnings, tree: finalTree);

                    double wallThickness = radius.Value - inner_radius.Value;
                    double tubeLength = distance.Value;
                    // Wall cross-section: rect from (inner_radius, 0) to (radius, tubeLength)
                    var wallRes = provider.SketchRectangle(inner_radius.Value, 0, radius.Value, tubeLength);
                    if (!IsSuccess(wallRes))
                        return MakeMacroError("Failed to draw tube wall cross-section.", geometryCreated: false, details: GetErrorMessage(wallRes), documentState: documentState, profileUsed: profileUsed, operationUsed: operationUsed, warnings: warnings, tree: finalTree);
                }
                else if (width.HasValue && height.HasValue && thickness.HasValue)
                {
                    // Compound: two rectangles → hollow box profile
                    double w = width.Value;
                    double h = height.Value;
                    double t = thickness.Value;
                    if (t * 2 >= w || t * 2 >= h)
                        return MakeMacroError("thickness is too large for the given width and height.", geometryCreated: false, documentState: documentState, profileUsed: profileUsed, operationUsed: operationUsed, warnings: warnings, tree: finalTree);

                    var outerRes = provider.SketchRectangle(0, 0, w, h);
                    if (!IsSuccess(outerRes))
                        return MakeMacroError("Failed to draw outer rectangle.", geometryCreated: false, details: GetErrorMessage(outerRes), documentState: documentState, profileUsed: profileUsed, operationUsed: operationUsed, warnings: warnings, tree: finalTree);

                    var innerRes = provider.SketchRectangle(t, t, w - t, h - t);
                    if (!IsSuccess(innerRes))
                        return MakeMacroError("Failed to draw inner rectangle.", geometryCreated: false, details: GetErrorMessage(innerRes), documentState: documentState, profileUsed: profileUsed, operationUsed: operationUsed, warnings: warnings, tree: finalTree);

                    isCompound = true;
                }
                else if (width.HasValue && height.HasValue)
                {
                    // Simple solid rectangle
                    double w = width.Value;
                    double h = height.Value;
                    var rectRes = provider.SketchRectangle(0, 0, w, h);
                    if (!IsSuccess(rectRes))
                        return MakeMacroError("Failed to draw rectangle profile.", geometryCreated: false, details: GetErrorMessage(rectRes), documentState: documentState, profileUsed: profileUsed, operationUsed: operationUsed, warnings: warnings, tree: finalTree);
                }
                else if (radius.HasValue)
                {
                    // Simple solid circle
                    double r = radius.Value;
                    var circRes = provider.SketchCircle(0, 0, r);
                    if (!IsSuccess(circRes))
                        return MakeMacroError("Failed to draw circle profile.", geometryCreated: false, details: GetErrorMessage(circRes), documentState: documentState, profileUsed: profileUsed, operationUsed: operationUsed, warnings: warnings, tree: finalTree);
                }
                // If no dims: proceed; profile discovery below will catch "no profile" and give actionable error.
                if (isRevolve)
                {
                    // Place axis through origin / rect left side (x=0) for solid basic revolution results
                    // (avoids rings/toroids from x=-20 offset on origin-based circle/rect profiles).
                    // P3b: drawn for fresh+revolve BEFORE the SketchProfiles + resolution (this block is inside createFresh), so chosen profile index is resolved against the sketch state Revolve will actually see.
                    var axisRes = provider.SketchLine(0, -40, 0, 40, tag: "macro_axis");
                    // Axis creation is best-effort; if it fails we still attempt revolve (may use numeric or fail with good error).
                }
            }

            // 4. Profile discovery (always required before feature; supports auto + explicit + manual pre-drawn)
            var profRes = provider.SketchProfiles();
            if (!IsSuccess(profRes))
                return MakeMacroError("Failed to list sketch profiles. Ensure a sketch with a closed profile is active.", geometryCreated: geometryCreated, details: GetErrorMessage(profRes), documentState: documentState, profileUsed: profileUsed, operationUsed: operationUsed, warnings: warnings, tree: finalTree);

            int profCount = 0;
            if (profRes.TryGetValue("profile_count", out var pc2) && pc2 is int pcount)
                profCount = pcount;

            if (profCount <= 0)
            {
                string hint = createFresh
                    ? "Provide width+height (for rect) or radius (for circle) when creating a fresh part, or draw a closed profile with atomic sketch tools before calling the macro."
                    : "No closed profile detected on the active sketch. Draw a closed shape (rect, circle, or lines+constraints) then call macro_basic_part again, or use force_new=true for a new part.";
                return MakeMacroError("No closed profile detected. " + hint, geometryCreated: geometryCreated, documentState: documentState, profileUsed: profileUsed, operationUsed: operationUsed, warnings: warnings, tree: finalTree);
            }

            // Choose profile: "auto" → primary (usually 1 after sorting in provider), or explicit like "2" or "2,4"
            // For compound profiles (concentric circles, offset rects), pick the annular/hollow region
            string chosenProfile;
            bool usedAuto = string.IsNullOrWhiteSpace(profile) || profile.Equals("auto", StringComparison.OrdinalIgnoreCase);
            if (usedAuto && isCompound && profCount > 1)
            {
                // Annular region is typically index 2 (inner=1, ring=2, outside=3+)
                chosenProfile = "2";
                profileUsed = "auto-resolved(annular)";
            }
            else
            {
                chosenProfile = ResolveProfileIndex(profile, profRes);
                profileUsed = usedAuto ? "auto-resolved" : chosenProfile;
            }

            // 5. Execute the core geometry (extrude or revolve). Only proven paths.
            Dictionary<string, object?> featRes;
            if (isRevolve)
            {
                operationUsed = "revolve";
                double useAngle = angle ?? (hasTube ? 360.0 : 0); // tube defaults to full revolve

                // Axis: for fresh+revolve, drawn earlier in create block (P3b: before SketchProfiles/resolution so index chosen matches Revolve's view post-axis mutation).
                // For continue+revolve (P1 critical): NEVER draw (would mutate user's sketch); instead precise pre-geo error using preserved documentState/profileUsed/operationUsed (P2).
                // Limitation: no detection of pre-existing user axis here (bigger change, non-minimal); user provides via atomic or has tagged line.
                string axisRef = "@macro_axis";
                if (!createFresh)
                {
                    return MakeMacroError("Revolve on continue requires a suitable axis already present in the active sketch (tagged @macro_axis or use numeric index). Do not use auto for continue+revolve; use atomic revolve with explicit axis instead, or force_new for auto-axis.", geometryCreated: false, documentState: documentState, profileUsed: profileUsed, operationUsed: operationUsed, warnings: warnings, tree: finalTree);
                }

                featRes = provider.Revolve(chosenProfile, axisRef, useAngle, direction: "positive", operation: operation);
            }
            else
            {
                operationUsed = "extrude";
                double useDist = distance ?? 10.0; // sensible default for quick basic parts if omitted
                featRes = provider.Extrude(chosenProfile, useDist, direction: "positive", taper: 0.0, operation: operation);
            }

            if (!IsSuccess(featRes))
            {
                return MakeMacroError($"Feature creation failed ({operationUsed}).", geometryCreated: false, details: GetErrorMessage(featRes), documentState: documentState, profileUsed: profileUsed, operationUsed: operationUsed, warnings: warnings, tree: finalTree);
            }

            geometryCreated = true;

            // 6. Full verification suite — all best-effort, none fails the operation
            Dictionary<string, object?>? bbox = null;
            Dictionary<string, object?>? parameters = null;
            var viewportImages = new List<Dictionary<string, object?>?>();

            // 6a. Feature tree (mandatory)
            try { finalTree = provider.GetFeatureTree(); }
            catch (Exception ex) { warnings.Add($"get_feature_tree failed: {ex.Message}"); }

            // 6b. Bounding box (precise geometry data)
            try { bbox = provider.GetBoundingBox(""); }
            catch (Exception ex) { warnings.Add($"get_bounding_box failed: {ex.Message}"); }

            // 6c. Model parameters
            try { parameters = provider.ParamList(); }
            catch (Exception ex) { warnings.Add($"param_list failed: {ex.Message}"); }

            // 6d. Viewport images (two views for visual verification)
            foreach (string view in new[] { "Iso", "Top" })
            {
                try
                {
                    var cap = provider.CaptureViewportImage(view: view, width: 800, height: 600, format: "png");
                    if (IsSuccess(cap))
                        viewportImages.Add(cap);
                    else
                        warnings.Add($"capture_viewport_image({view}) failed: {GetErrorMessage(cap) ?? "unknown"}");
                }
                catch (Exception ex)
                {
                    warnings.Add($"capture_viewport_image({view}) failed: {ex.Message}");
                }
            }

            // 7. Success envelope (exact contract) — includes full verification data
            return new Dictionary<string, object?>
            {
                ["success"] = true,
                ["geometry_created"] = true,
                ["document_state"] = documentState,
                ["profile_used"] = profileUsed,
                ["operation"] = operationUsed,
                ["warnings"] = warnings,
                ["tree"] = finalTree,
                ["bounding_box"] = bbox,
                ["parameters"] = parameters,
                ["viewport_images"] = viewportImages,
                ["next"] = "ready for fillets, patterns, holes, or material via iproperty_set / iproperty_custom_set"
            };
        }
        catch (InventorConnectionException ex)
        {
            return MakeMacroError(ex.Message, geometryCreated: geometryCreated, documentState: documentState, profileUsed: profileUsed, operationUsed: operationUsed, warnings: warnings, tree: finalTree);
        }
        catch (InventorComException ex)
        {
            return MakeMacroError(ex.Message, geometryCreated: geometryCreated, documentState: documentState, profileUsed: profileUsed, operationUsed: operationUsed, warnings: warnings, tree: finalTree);
        }
        catch (Exception ex)
        {
            return MakeMacroError($"Unexpected error in macro_basic_part: {ex.Message}", geometryCreated: geometryCreated, documentState: documentState, profileUsed: profileUsed, operationUsed: operationUsed, warnings: warnings, tree: finalTree);
        }
    }

    // Small helpers for the macro (local to this orchestration; keep focused)
    private static bool IsSuccess(Dictionary<string, object?>? d)
        => d != null && d.TryGetValue("success", out var s) && s is bool b && b;

    private static string? GetErrorMessage(Dictionary<string, object?>? d)
    {
        if (d == null) return null;
        if (d.TryGetValue("error", out var e) && e is string es && !string.IsNullOrWhiteSpace(es)) return es;
        return null;
    }

    private static Dictionary<string, object?> MakeMacroError(string message, bool geometryCreated, string? details = null,
        string? documentState = "unknown", string? profileUsed = null, string? operationUsed = null,
        List<string>? warnings = null, Dictionary<string, object?>? tree = null)
    {
        var err = ToolHelpers.Error(message);
        err["geometry_created"] = geometryCreated;
        err["document_state"] = documentState ?? "unknown";
        err["profile_used"] = profileUsed;
        err["operation"] = operationUsed;
        err["warnings"] = warnings ?? new List<string>();
        err["tree"] = tree;
        if (!string.IsNullOrWhiteSpace(details))
            err["details"] = details;
        // error key already set by ToolHelpers.Error
        return err;
    }

    /// <summary>
    /// Resolve profile selection from "auto" (pick first/primary after provider sort) or explicit "2" / "2,4".
    /// Returns the string to pass to extrude/revolve (supports multi-region).
    /// </summary>
    private static string ResolveProfileIndex(string requested, Dictionary<string, object?> profRes)
    {
        if (string.IsNullOrWhiteSpace(requested) || requested.Equals("auto", StringComparison.OrdinalIgnoreCase))
        {
            // Provider already sorts profiles; primary is index 1 in the returned list
            return "1";
        }

        // Pass through explicit (including multi like "2,4")
        return requested;
    }

    // ── Delete sketch skill (1) ─────────────────────────────────────────

    [McpServerTool, Description("Delete the active sketch (must not be used by a feature).")]
    public Dictionary<string, object?> skill_delete_sketch()
        => Catch(provider.SketchDelete);

    // ── Helpers ──────────────────────────────────────────────────────────

    /// <summary>
    /// Compute center (cx, cy) and radius from three perimeter points
    /// using perpendicular bisector intersection.
    /// </summary>
    private static (double cx, double cy, double radius) CircleFromThreePoints(
        double x1, double y1, double x2, double y2, double x3, double y3)
    {
        double mx1 = (x1 + x2) / 2.0;
        double my1 = (y1 + y2) / 2.0;
        double mx2 = (x2 + x3) / 2.0;
        double my2 = (y2 + y3) / 2.0;

        double dx1 = x2 - x1;
        double dy1 = y2 - y1;
        double dx2 = x3 - x2;
        double dy2 = y3 - y2;

        const double eps = 1e-12;
        double ccx, ccy;

        if (Math.Abs(dy1) < eps)
        {
            if (Math.Abs(dy2) < eps)
                throw new ArgumentException("Points are collinear — cannot form a circle");
            double s2 = -dx2 / dy2;
            ccx = mx1;
            ccy = s2 * (ccx - mx2) + my2;
        }
        else if (Math.Abs(dy2) < eps)
        {
            double s1 = -dx1 / dy1;
            ccx = mx2;
            ccy = s1 * (ccx - mx1) + my1;
        }
        else
        {
            double s1 = -dx1 / dy1;
            double s2 = -dx2 / dy2;

            if (Math.Abs(s1 - s2) < eps)
                throw new ArgumentException("Points are collinear — cannot form a circle");

            ccx = (my2 - my1 + s1 * mx1 - s2 * mx2) / (s1 - s2);
            ccy = s1 * (ccx - mx1) + my1;
        }

        double r = Math.Sqrt((ccx - x1) * (ccx - x1) + (ccy - y1) * (ccy - y1));
        return (ccx, ccy, r);
    }

    /// <summary>
    /// Compute sweep angle in radians from start to end around center.
    /// </summary>
    private static double SweepBetween(double cx, double cy, double sx, double sy, double ex, double ey, bool ccw)
    {
        double aStart = Math.Atan2(sy - cy, sx - cx);
        double aEnd = Math.Atan2(ey - cy, ex - cx);

        if (ccw)
        {
            if (aEnd <= aStart)
                aEnd += 2.0 * Math.PI;
        }
        else
        {
            if (aEnd >= aStart)
                aEnd -= 2.0 * Math.PI;
        }

        return aEnd - aStart;
    }

    /// <summary>
    /// Error-catching helper matching AtomicTools pattern (D7).
    /// </summary>
    private static Dictionary<string, object?> Catch(Func<Dictionary<string, object?>> action)
    {
        try
        {
            return action();
        }
        catch (InventorConnectionException ex)
        {
            return ToolHelpers.Error(ex.Message);
        }
        catch (InventorComException ex)
        {
            return ToolHelpers.Error(ex.Message);
        }
        catch (Exception ex)
        {
            return ToolHelpers.Error($"Unexpected error: {ex.Message}");
        }
    }
}