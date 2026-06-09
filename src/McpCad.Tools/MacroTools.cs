using McpCad.Core;
using McpCad.Core.Exceptions;
using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace McpCad.Tools;

/// <summary>
/// DTO for a single sketch entity parsed from JSON.
/// Supports line, circle, rect, arc, spline, point, ellipse, polygon.
/// JSON uses snake_case for multi-word keys (start_angle, fit_method, etc.).
/// </summary>
public record SketchEntity(
    string Type,
    double? X1 = null,
    double? Y1 = null,
    double? X2 = null,
    double? Y2 = null,
    double? Cx = null,
    double? Cy = null,
    double? Radius = null,
    double? Width = null,
    double? Height = null,
    [property: JsonPropertyName("start_angle")] double? StartAngle = null,
    [property: JsonPropertyName("end_angle")] double? EndAngle = null,
    [property: JsonPropertyName("inner_radius")] double? InnerRadius = null,
    double? Thickness = null,
    string? Points = null,
    [property: JsonPropertyName("fit_method")] string? FitMethod = null,
    int? Sides = null,
    [property: JsonPropertyName("start_angle_deg")] double? StartAngleDeg = null,
    string? Tag = null,
    // Aliases for point (x/y) and ellipse to support documented JSON schema
    [property: JsonPropertyName("x")] double? X = null,
    [property: JsonPropertyName("y")] double? Y = null,
    [property: JsonPropertyName("major_radius")] double? MajorRadius = null,
    [property: JsonPropertyName("minor_radius")] double? MinorRadius = null,
    double? Angle = null
);

/// <summary>
/// DTO for a sketch constraint parsed from JSON.
/// </summary>
public record SketchConstraint(
    string Mode,
    string Entity1,
    string? Entity2 = null,
    [property: JsonPropertyName("sym_line")] string? SymLine = null,
    string? Axis = null
);

/// <summary>
/// DTO for a sketch dimension parsed from JSON.
/// </summary>
public record SketchDimension(
    string Mode,
    string Entity1,
    string? Entity2 = null,
    double? Value = null
);

/// <summary>
/// Per-phase status envelope for macro_god_part composition.
/// Each phase (sketch/feature/pattern/modify/verify) reports independently.
/// Used inside the top-level "phase_status" object.
/// </summary>
public record MacroPhaseStatus(
    bool Success,
    string? Phase = null,
    string? Error = null,
    int? EntityCount = null,
    int? ConstraintCount = null,
    string? FeatureType = null,
    string? FeatureName = null
);

/// <summary>
/// DTO for sketch modify operations (move, rotate, scale, offset, mirror, trim) parsed from sketch_modify JSON.
/// </summary>
public record SketchModifyOp(
    string Op,
    string? Entities = null,
    double? Dx = null,
    double? Dy = null,
    double? Cx = null,
    double? Cy = null,
    double? Angle = null,
    double? Factor = null,
    double? OffsetX = null,
    double? OffsetY = null,
    bool? Copy = null,
    bool? IncludeConnected = null,
    string? Entity = null,
    string? CuttingEntity = null,
    string? Side = null,
    string? MirrorEntity = null
);

/// <summary>
/// DTO for sketch pattern operations (circular, rectangular, mirror) parsed from sketch_pattern JSON.
/// </summary>
public record SketchPatternOp(
    string Type,
    string Entities,
    string? Axis = null,
    int? Count = null,
    double? Angle = null,
    bool? Fitted = null,
    bool? Symmetric = null,
    string? XAxis = null,
    int? XCount = null,
    double? XSpacing = null,
    string? YAxis = null,
    int? YCount = null,
    double? YSpacing = null,
    string? MirrorEntity = null
);

/// <summary>
/// DTO for 3D pattern operations (circular, rectangular, mirror) parsed from pattern_3d JSON.
/// </summary>
public record Pattern3DOp(
    string Type,
    string Profile,
    string? Axis = null,
    int? Count = null,
    double? Angle = null,
    bool? FitWithinAngle = null,
    bool? NaturalDirection = null,
    string? XAxis = null,
    int? XCount = null,
    double? XSpacing = null,
    string? YAxis = null,
    int? YCount = null,
    double? YSpacing = null,
    string? MirrorPlane = null
);

/// <summary>
/// DTO for 3D modify operations (fillet, chamfer, shell, draft, thread, split, ...) parsed from modify_3d JSON.
/// </summary>
public record Modify3DOp(
    string Op,
    string? Edges = null,
    string? Faces = null,
    double? Radius = null,
    double? Distance = null,
    string? Mode = null,
    double? Thickness = null,
    string? Direction = null,
    double? Angle = null,
    string? PullDirection = null,
    string? FixedEntity = null,
    string? SplitTool = null,
    string? RemoveSide = null,
    string? TargetBody = null,
    string? Face = null,
    string? Specification = null,
    string? Operation = null,
    bool? KeepToolBodies = null,
    // hole op fields
    double? X = null,
    double? Y = null,
    double? Diameter = null,
    double? Depth = null,
    [property: JsonPropertyName("hole_type")] string? HoleType = null
);

/// <summary>
/// Macro tools — high-level single-call workflows that compose multiple atomic
/// operations (sketch entities + constraints + dimensions + 3D feature + patterns + modify + verify).
/// This class is the home for `macro_god_part` (implemented in PR 2).
/// Phase 1 delivers the shared models, JSON parser, and polygon helper.
/// </summary>
[McpServerToolType]
public class MacroTools(IMechanicalCadProvider provider)
{
    private readonly IMechanicalCadProvider _provider = provider;

    private static readonly JsonSerializerOptions SketchParseOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    // ── Phase helpers (will be used by macro_god_part in PR 2) ─────────────────

    /// <summary>
    /// Error-catching helper matching the AtomicTools / SkillTools pattern (D7 contract).
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

    /// <summary>
    /// Success check matching the pattern used inside macro_basic_part.
    /// </summary>
    private static bool IsSuccess(Dictionary<string, object?>? d)
        => d != null && d.TryGetValue("success", out var s) && s is bool b && b;

    // ── Polygon helper (task 1.5) ───────────────────────────────────────────────

    /// <summary>
    /// Generate N line segments forming a regular polygon centered at (cx,cy)
    /// with the given radius. Used by the sketch phase when a "polygon" entity
    /// is present (no native polygon sketch tool exists; we emit lines + constraints).
    /// </summary>
    /// <returns>List of (x1,y1,x2,y2) tuples — one per edge.</returns>
    public static List<(double X1, double Y1, double X2, double Y2)> GeneratePolygonLines(
        double cx, double cy, double radius, int sides, double startAngleDeg = 0)
    {
        var lines = new List<(double X1, double Y1, double X2, double Y2)>();
        if (sides < 3) sides = 3;
        if (radius <= 0) radius = 1.0;

        double angleStep = 2.0 * Math.PI / sides;
        double startRad = startAngleDeg * Math.PI / 180.0;

        for (int i = 0; i < sides; i++)
        {
            double a1 = startRad + i * angleStep;
            double a2 = startRad + (i + 1) * angleStep;

            double x1 = cx + radius * Math.Cos(a1);
            double y1 = cy + radius * Math.Sin(a1);
            double x2 = cx + radius * Math.Cos(a2);
            double y2 = cy + radius * Math.Sin(a2);

            lines.Add((x1, y1, x2, y2));
        }

        return lines;
    }

    // ── JSON parsing (task 1.4) ─────────────────────────────────────────────────

    /// <summary>
    /// Parse an optional JSON string containing an array of sketch entities,
    /// constraints, or dimensions. Returns the strongly-typed list or a
    /// structured error dictionary with a clear validation message.
    ///
    /// Empty / whitespace / null input → (null, null) meaning "field not provided".
    /// Used by the sketch phase of macro_god_part.
    /// </summary>
    public static (List<T>? Items, Dictionary<string, object?>? Error)
        ParseSketchJson<T>(string? json, string fieldName) where T : notnull
    {
        if (string.IsNullOrWhiteSpace(json))
            return (null, null);

        if (json.Length > 64 * 1024)
            return (null, ToolHelpers.Error($"{fieldName}: input exceeds max length of 64KB ({json.Length} bytes)"));

        List<T>? items;
        try
        {
            items = JsonSerializer.Deserialize<List<T>>(json, SketchParseOptions);
        }
        catch (JsonException ex)
        {
            return (null, ToolHelpers.Error($"Invalid JSON in {fieldName}: {ex.Message}"));
        }
        catch (Exception ex)
        {
            return (null, ToolHelpers.Error($"Failed to parse {fieldName}: {ex.Message}"));
        }

        if (items == null)
            return (null, ToolHelpers.Error($"Failed to deserialize {fieldName} (null result)"));

        // Lightweight structural validation with actionable messages
        if (typeof(T) == typeof(SketchEntity))
        {
            for (int i = 0; i < items.Count; i++)
            {
                var raw = (object)items[i];
                var e = (SketchEntity)raw;

                if (string.IsNullOrWhiteSpace(e.Type))
                    return (null, ToolHelpers.Error($"{fieldName}[{i}]: 'type' is required"));

                string t = e.Type.Trim().ToLowerInvariant();

                switch (t)
                {
                    case "line":
                    case "rect":
                        if (!e.X1.HasValue || !e.Y1.HasValue || !e.X2.HasValue || !e.Y2.HasValue)
                            return (null, ToolHelpers.Error($"{fieldName}[{i}]: {t} requires x1, y1, x2, y2"));
                        break;

                    case "circle":
                    case "polygon":
                        if (!e.Cx.HasValue || !e.Cy.HasValue || !e.Radius.HasValue)
                            return (null, ToolHelpers.Error($"{fieldName}[{i}]: {t} requires cx, cy, radius"));
                        if (e.Radius.Value <= 0)
                            return (null, ToolHelpers.Error($"{fieldName}[{i}]: radius must be > 0"));
                        if (t == "polygon")
                        {
                            if (!e.Sides.HasValue || e.Sides.Value < 3)
                                return (null, ToolHelpers.Error($"{fieldName}[{i}]: polygon requires sides >= 3"));
                        }
                        break;

                    case "arc":
                        if (!e.Cx.HasValue || !e.Cy.HasValue || !e.Radius.HasValue ||
                            !e.StartAngle.HasValue || !e.EndAngle.HasValue)
                            return (null, ToolHelpers.Error($"{fieldName}[{i}]: arc requires cx, cy, radius, start_angle, end_angle"));
                        if (e.Radius.Value <= 0)
                            return (null, ToolHelpers.Error($"{fieldName}[{i}]: radius must be > 0"));
                        break;

                    case "spline":
                        if (string.IsNullOrWhiteSpace(e.Points))
                            return (null, ToolHelpers.Error($"{fieldName}[{i}]: spline requires 'points' (x1,y1,x2,y2,...)"));
                        break;

                    case "point":
                        bool hasX = e.X.HasValue || e.X1.HasValue;
                        bool hasY = e.Y.HasValue || e.Y1.HasValue;
                        if (!hasX || !hasY)
                            return (null, ToolHelpers.Error($"{fieldName}[{i}]: point requires x/y or x1/y1"));
                        break;

                    case "ellipse":
                        bool hasCx = e.Cx.HasValue;
                        bool hasCy = e.Cy.HasValue;
                        double? maj = e.MajorRadius ?? e.Radius;
                        double? min = e.MinorRadius ?? e.Radius;
                        if (!hasCx || !hasCy || !maj.HasValue || !min.HasValue)
                            return (null, ToolHelpers.Error($"{fieldName}[{i}]: ellipse requires cx, cy, major_radius (or radius), minor_radius (or radius)"));
                        if ((maj ?? 0) <= 0 || (min ?? 0) <= 0)
                            return (null, ToolHelpers.Error($"{fieldName}[{i}]: major_radius and minor_radius must be > 0"));
                        break;

                    default:
                        return (null, ToolHelpers.Error($"{fieldName}[{i}]: unknown entity type '{e.Type}' (supported: line,circle,rect,arc,spline,point,ellipse,polygon)"));
                }
            }
        }
        else if (typeof(T) == typeof(SketchConstraint))
        {
            for (int i = 0; i < items.Count; i++)
            {
                var c = (SketchConstraint)(object)items[i];
                if (string.IsNullOrWhiteSpace(c.Mode))
                    return (null, ToolHelpers.Error($"{fieldName}[{i}]: 'mode' is required"));
                if (string.IsNullOrWhiteSpace(c.Entity1))
                    return (null, ToolHelpers.Error($"{fieldName}[{i}]: 'entity1' is required"));
            }
        }
        else if (typeof(T) == typeof(SketchDimension))
        {
            for (int i = 0; i < items.Count; i++)
            {
                var d = (SketchDimension)(object)items[i];
                if (string.IsNullOrWhiteSpace(d.Mode))
                    return (null, ToolHelpers.Error($"{fieldName}[{i}]: 'mode' is required"));
                if (string.IsNullOrWhiteSpace(d.Entity1))
                    return (null, ToolHelpers.Error($"{fieldName}[{i}]: 'entity1' is required"));
            }
        }

        return (items, null);
    }

    /// <summary>
    /// General JSON array parser for phase operation lists (sketch_modify, sketch_pattern, pattern_3d, modify_3d).
    /// Empty/null → (null, null). Returns structured error on parse failure.
    /// Light validation for required discriminator fields; per-op execution will surface provider errors.
    /// </summary>
    public static (List<T>? Items, Dictionary<string, object?>? Error)
        ParsePhaseJson<T>(string? json, string fieldName) where T : notnull
    {
        if (string.IsNullOrWhiteSpace(json))
            return (null, null);

        if (json.Length > 64 * 1024)
            return (null, ToolHelpers.Error($"{fieldName}: input exceeds max length of 64KB ({json.Length} bytes)"));

        List<T>? items;
        try
        {
            items = JsonSerializer.Deserialize<List<T>>(json, SketchParseOptions);
        }
        catch (JsonException ex)
        {
            return (null, ToolHelpers.Error($"Invalid JSON in {fieldName}: {ex.Message}"));
        }
        catch (Exception ex)
        {
            return (null, ToolHelpers.Error($"Failed to parse {fieldName}: {ex.Message}"));
        }

        if (items == null)
            return (null, ToolHelpers.Error($"Failed to deserialize {fieldName} (null result)"));

        // Lightweight required-field validation
        if (typeof(T) == typeof(SketchModifyOp))
        {
            for (int i = 0; i < items.Count; i++)
            {
                var m = (SketchModifyOp)(object)items[i];
                if (string.IsNullOrWhiteSpace(m.Op))
                    return (null, ToolHelpers.Error($"{fieldName}[{i}]: 'op' is required"));
            }
        }
        else if (typeof(T) == typeof(SketchPatternOp))
        {
            for (int i = 0; i < items.Count; i++)
            {
                var p = (SketchPatternOp)(object)items[i];
                if (string.IsNullOrWhiteSpace(p.Type))
                    return (null, ToolHelpers.Error($"{fieldName}[{i}]: 'type' is required"));
                if (string.IsNullOrWhiteSpace(p.Entities))
                    return (null, ToolHelpers.Error($"{fieldName}[{i}]: 'entities' is required"));
            }
        }
        else if (typeof(T) == typeof(Pattern3DOp))
        {
            for (int i = 0; i < items.Count; i++)
            {
                var p = (Pattern3DOp)(object)items[i];
                if (string.IsNullOrWhiteSpace(p.Type))
                    return (null, ToolHelpers.Error($"{fieldName}[{i}]: 'type' is required"));
                if (string.IsNullOrWhiteSpace(p.Profile))
                    return (null, ToolHelpers.Error($"{fieldName}[{i}]: 'profile' is required"));
            }
        }
        else if (typeof(T) == typeof(Modify3DOp))
        {
            for (int i = 0; i < items.Count; i++)
            {
                var m = (Modify3DOp)(object)items[i];
                if (string.IsNullOrWhiteSpace(m.Op))
                    return (null, ToolHelpers.Error($"{fieldName}[{i}]: 'op' is required"));
            }
        }

        return (items, null);
    }

    // ── THE GOD MACRO (Phase 2 + 3) ─────────────────────────────────────────────

    [McpServerTool, Description("High-level god macro: single-call composition of sketch (JSON entities+constraints+dimensions) + sketch modify + sketch patterns + 3D feature (extrude|revolve|sweep|loft|coil|rib with full params) + 3D patterns (circular|rectangular|mirror) + 3D modify (fillet|chamfer|shell|draft|thread|split) + optional iProperties + mandatory verification (GetFeatureTree + best-effort bbox/params/2x viewport images). Each phase is optional (skipped if its JSON/param is null/empty) and independently try-caught via Catch() + IsSuccess(); failures are reported in warnings[] and phase_status.<phase>.success=false without aborting the rest. ask_before_modify (default true) performs Health()+GetFeatureTree() guard: if existing doc has feature_count>0, returns confirmation envelope (needs_confirmation=true) so agents can decide to force or start fresh. force_new forces DocNewPart. plane defaults to YZ. Returns standardized envelope matching macro_basic_part style plus rich phase_status and next hint. Use for complex one-shot parts that would otherwise require 8-15 atomic calls. iProperties (part_number, description, material) are applied best-effort at end if provided.")]
    public Dictionary<string, object?> macro_god_part(
        // ── General ──
        [Description("When true (default), call Health() then GetFeatureTree() and if feature_count > 0 return a safe confirmation envelope instead of mutating an existing document. Set to false to bypass and modify in place (or combine with force_new=true).")] bool ask_before_modify = true,
        [Description("Work plane for initial SketchCreate. Defaults to 'YZ' (common for mechanical parts standing on XY base). Other values: 'XY', 'XZ'.")] string? plane = "YZ",
        [Description("If true, always call DocNewPart() even if an active document exists (fresh part). If null/false, only create new if no active document is detected via Health().")] bool? force_new = null,

        // ── Sketch JSON (entities, constraints, dimensions) ──
        [Description("Optional JSON array string of sketch entities. Each item requires 'type'. Supported: line (x1,y1,x2,y2[,tag]), circle (cx,cy,radius[,tag]), rect (x1,y1,x2,y2), arc (cx,cy,radius,start_angle,end_angle), spline (points,fit_method), point (x,y or x1/y1), ellipse (cx,cy,major_radius,minor_radius[,angle]), polygon (cx,cy,radius,sides[,start_angle_deg]). Example: '[{\"type\":\"rect\",\"x1\":0,\"y1\":0,\"x2\":10,\"y2\":10},{\"type\":\"circle\",\"cx\":5,\"cy\":5,\"radius\":2}]'")] string? sketch_entities = null,
        [Description("Optional JSON array of sketch constraints. Each: { \"mode\": \"coincident|parallel|...\", \"entity1\": \"1\", \"entity2\": \"2\" (optional), \"sym_line\", \"axis\" }. Applied after entities.")] string? sketch_constraints = null,
        [Description("Optional JSON array of sketch dimensions. Each: { \"mode\": \"linear|radius|diameter|angle\", \"entity1\": \"1\", \"entity2\": \"2\" (optional), \"value\": 10.0 }. Applied after constraints.")] string? sketch_dimensions = null,

        // ── Sketch Modify & Pattern (post-entity ops on active sketch) ──
        [Description("Optional JSON array of sketch-level modify ops executed after entities/constraints/dims. Ops: move/rotate/scale/offset/mirror/trim. Example: '[{\"op\":\"move\",\"entities\":\"1,2\",\"dx\":5,\"dy\":0},{\"op\":\"rotate\",\"entities\":\"3\",\"cx\":0,\"cy\":0,\"angle\":45,\"copy\":false}]'")] string? sketch_modify = null,
        [Description("Optional JSON array of sketch patterns. Types: circular, rectangular, mirror. Example: '[{\"type\":\"circular\",\"entities\":\"1\",\"axis\":\"X\",\"count\":6,\"angle\":360},{\"type\":\"mirror\",\"entities\":\"2,3\",\"mirror_entity\":\"1\"}]'")] string? sketch_pattern = null,

        // ── Feature (type dispatch + shared params) ──
        [Description("3D feature type to create after sketch. One of: extrude, revolve, sweep, loft, coil, rib. If null/omitted, feature phase is skipped.")] string? feature_type = null,
        [Description("Profile selector for the feature (e.g. '1', '2,4' for multi-region, or named if supported). Default '1'.")] string? feature_profile = null,
        [Description("Extrude distance (cm) or revolve angle when applicable.")] double? feature_distance = null,
        [Description("Axis reference for revolve/coil (e.g. 'Y', 'X', '@my_axis' or edge index). Required for revolve/coil.")] string? feature_axis = null,
        [Description("Path selector for sweep (sketch entity or profile index for the sweep path).")] string? feature_path = null,
        [Description("Comma-separated profile list for loft (e.g. '1,2,3').")] string? feature_profiles = null,
        [Description("Pitch (cm) for coil feature.")] double? feature_pitch = null,
        [Description("Revolutions for coil feature.")] double? feature_revolutions = null,
        [Description("Thickness (cm) for rib feature.")] double? feature_thickness = null,
        [Description("Angle (deg) for revolve (full 360 default) or other angled features.")] double? feature_angle = null,
        [Description("Boolean operation for the feature: new_body | join | cut | intersect. Default 'new_body'.")] string? feature_operation = "new_body",
        [Description("Taper angle (deg) for extrude/sweep. Default 0 (no taper).")] double? feature_taper = 0.0,
        [Description("Direction for extrude/revolve: positive | negative. Default 'positive'.")] string? feature_direction = "positive",

        // ── 3D Pattern ──
        [Description("Optional JSON array of 3D feature patterns applied after the base feature. Types: circular, rectangular, mirror. 'profile' is the feature name or index from tree (e.g. 'Extrusion1' or '1'). Example: '[{\"type\":\"circular\",\"profile\":\"1\",\"axis\":\"Y\",\"count\":4},{\"type\":\"mirror\",\"profile\":\"2\",\"mirror_plane\":\"XZ\"}]'")] string? pattern_3d = null,

        // ── Modify 3D (post-feature edits) ──
        [Description("Optional JSON array of 3D modify operations. Supported ops: fillet, chamfer, shell, draft, thread, split. Example: '[{\"op\":\"fillet\",\"edges\":\"1,2\",\"radius\":0.5,\"mode\":\"constant\"},{\"op\":\"shell\",\"faces\":\"1\",\"thickness\":0.2,\"direction\":\"inside\"}]'")] string? modify_3d = null,

        // ── iProperties (applied best-effort after verify) ──
        [Description("Optional part number to set via IPropertySet (Summary set).")] string? part_number = null,
        [Description("Optional description to set via IPropertySet (Summary set).")] string? description = null,
        [Description("Optional material name to set via IPropertyCustomSet (custom property).")] string? material = null
    )
    {
        var warnings = new List<string>();
        var phaseStatus = new Dictionary<string, object?>
        {
            ["sketch"] = null,
            ["feature"] = null,
            ["sketch_pattern"] = null,
            ["pattern_3d"] = null,
            ["modify_3d"] = null
        };
        string documentState = "existing";
        bool geometryCreated = false;
        Dictionary<string, object?>? tree = null;
        Dictionary<string, object?>? bbox = null;
        Dictionary<string, object?>? parameters = null;
        var viewportImages = new List<Dictionary<string, object?>?>();

        try
        {
            // ── 0. ask_before_modify guard (Health + feature count) ───────────────
            if (ask_before_modify)
            {
                try
                {
                    var h = _provider.Health();
                    bool hasActiveDoc = false;
                    if (h.TryGetValue("active_document", out var adObj) && adObj is string adStr && !string.IsNullOrWhiteSpace(adStr))
                        hasActiveDoc = true;

                    int featureCount = 0;
                    if (hasActiveDoc)
                    {
                        try
                        {
                            var t = _provider.GetFeatureTree();
                            if (t.TryGetValue("feature_count", out var fcObj) && fcObj is int fc && fc > 0)
                                featureCount = fc;
                            else if (t.TryGetValue("features", out var fl) && fl is System.Collections.IList list)
                                featureCount = list.Count;
                        }
                        catch { /* best effort */ }
                    }

                    if (featureCount > 0)
                    {
                        Dictionary<string, object?>? currentTree = null;
                        try { currentTree = _provider.GetFeatureTree(); } catch { }

                        return new Dictionary<string, object?>
                        {
                            ["success"] = true,
                            ["needs_confirmation"] = true,
                            ["message"] = $"Document has {featureCount} existing features. Set ask_before_modify=false to proceed, or use a new part.",
                            ["current_state"] = new Dictionary<string, object?>
                            {
                                ["document_state"] = "existing",
                                ["feature_count"] = featureCount,
                                ["tree"] = currentTree
                            }
                        };
                    }
                }
                catch (Exception ex)
                {
                    // Guard itself failed — log and continue (safer to proceed than block user)
                    warnings.Add($"ask_before_modify guard inspection failed (proceeding): {ex.Message}");
                }
            }

            // ── 1. Document setup (Health + conditional DocNewPart) ───────────────
            try
            {
                var h = _provider.Health();
                bool hasActiveDoc = false;
                if (h.TryGetValue("active_document", out var adObj) && adObj is string adStr && !string.IsNullOrWhiteSpace(adStr))
                    hasActiveDoc = true;

                bool shouldCreateNew = (force_new == true) || !hasActiveDoc;
                if (shouldCreateNew)
                {
                    var newDoc = Catch(() => _provider.DocNewPart());
                    if (!IsSuccess(newDoc))
                    {
                        warnings.Add($"DocNewPart failed: {newDoc?["error"]?.ToString() ?? "unknown"}");
                    }
                    else
                    {
                        documentState = "new";
                    }
                }
            }
            catch (Exception ex)
            {
                warnings.Add($"Document setup error: {ex.Message}");
            }

            // ── 2. Sketch phase (create + entities + constraints + dimensions + modify + pattern) ──
            bool hasSketchInput = !string.IsNullOrWhiteSpace(sketch_entities) ||
                                  !string.IsNullOrWhiteSpace(sketch_constraints) ||
                                  !string.IsNullOrWhiteSpace(sketch_dimensions) ||
                                  !string.IsNullOrWhiteSpace(sketch_modify) ||
                                  !string.IsNullOrWhiteSpace(sketch_pattern);

            MacroPhaseStatus? sketchStatus = null;
            int sketchEntityCount = 0;
            int sketchConstraintCount = 0;
            bool skipRemaining = false;

            if (hasSketchInput)
            {
                try
                {
                    var skCreate = Catch(() => _provider.SketchCreate(plane ?? "YZ"));
                    if (!IsSuccess(skCreate))
                    {
                        warnings.Add($"SketchCreate failed: {skCreate?["error"]?.ToString() ?? "unknown"}");
                        sketchStatus = new MacroPhaseStatus(false, "sketch", Error: skCreate?["error"]?.ToString());
                    }
                    else
                    {
                        // Entities
                        var (entities, eerr) = ParseSketchJson<SketchEntity>(sketch_entities, "sketch_entities");
                        if (eerr != null)
                        {
                            warnings.Add(eerr["error"]?.ToString() ?? "sketch_entities parse error");
                            sketchStatus = new MacroPhaseStatus(false, "sketch", Error: eerr["error"]?.ToString());
                            skipRemaining = true;
                        }
                        else if (entities != null && entities.Count > 0)
                        {
                            foreach (var e in entities)
                            {
                                Dictionary<string, object?> res;
                                string t = (e.Type ?? "").Trim().ToLowerInvariant();
                                bool polySuccess = true;
                                int linesAdded = 0;
                                switch (t)
                                {
                                    case "line":
                                        res = Catch(() => _provider.SketchLine(e.X1!.Value, e.Y1!.Value, e.X2!.Value, e.Y2!.Value, e.Tag));
                                        break;
                                    case "circle":
                                        res = Catch(() => _provider.SketchCircle(e.Cx!.Value, e.Cy!.Value, e.Radius!.Value, e.Tag));
                                        break;
                                    case "rect":
                                        res = Catch(() => _provider.SketchRectangle(e.X1!.Value, e.Y1!.Value, e.X2!.Value, e.Y2!.Value));
                                        break;
                                    case "arc":
                                        res = Catch(() => _provider.SketchArc(e.Cx!.Value, e.Cy!.Value, e.Radius!.Value, e.StartAngle!.Value, e.EndAngle!.Value));
                                        break;
                                    case "spline":
                                        res = Catch(() => _provider.SketchSpline(e.Points!, e.FitMethod ?? "sweet"));
                                        break;
                                    case "point":
                                        double px = e.X ?? e.X1 ?? 0;
                                        double py = e.Y ?? e.Y1 ?? 0;
                                        res = Catch(() => _provider.SketchPoint(px, py));
                                        break;
                                    case "ellipse":
                                        double maj = e.MajorRadius ?? e.Radius ?? 1.0;
                                        double min = e.MinorRadius ?? e.Radius ?? 1.0;
                                        res = Catch(() => _provider.SketchEllipse(e.Cx!.Value, e.Cy!.Value, maj, min, e.Angle ?? 0.0));
                                        break;
                                    case "polygon":
                                        var lines = GeneratePolygonLines(e.Cx!.Value, e.Cy!.Value, e.Radius!.Value, e.Sides!.Value, e.StartAngleDeg ?? 0.0);
                                        int expectedSides = e.Sides!.Value;
                                        foreach (var (x1, y1, x2, y2) in lines)
                                        {
                                            var lres = Catch(() => _provider.SketchLine(x1, y1, x2, y2, e.Tag));
                                            if (IsSuccess(lres))
                                                linesAdded++;
                                            else
                                            {
                                                polySuccess = false;
                                                warnings.Add($"polygon line failed: {lres?["error"]?.ToString() ?? "unknown"}");
                                            }
                                        }
                                        res = (polySuccess && linesAdded == expectedSides) ? ToolHelpers.Success() : ToolHelpers.Error("one or more polygon lines failed");
                                        break;
                                    default:
                                        res = ToolHelpers.Error($"unknown sketch entity type '{e.Type}'");
                                        break;
                                }

                                if (IsSuccess(res) && (t != "polygon" || polySuccess))
                                    sketchEntityCount++;
                                else
                                    warnings.Add($"sketch entity '{t}' failed: {res?["error"]?.ToString() ?? "unknown"}");
                            }
                        }

                        // Constraints (always attempt if provided, even if no entities parsed above)
                        if (skipRemaining)
                        {
                            // skipped due to prior sketch_entities parse error (sketchStatus already set to failure)
                        }
                        else
                        {
                            var (constraints, cerr) = ParseSketchJson<SketchConstraint>(sketch_constraints, "sketch_constraints");
                            if (cerr != null)
                            {
                                warnings.Add(cerr["error"]?.ToString() ?? "sketch_constraints parse error");
                                phaseStatus["sketch"] = new MacroPhaseStatus(false, "sketch", Error: cerr["error"]?.ToString());
                                sketchStatus = (MacroPhaseStatus?)phaseStatus["sketch"];
                            }
                            else if (constraints != null)
                            {
                                foreach (var c in constraints)
                                {
                                    var cres = Catch(() => _provider.SketchConstraint(
                                        c.Mode, c.Entity1, c.Entity2 ?? "", c.SymLine ?? "", c.Axis ?? "major"));
                                    if (IsSuccess(cres))
                                        sketchConstraintCount++;
                                    else
                                    {
                                        warnings.Add($"sketch constraint '{c.Mode}' on {c.Entity1} failed: {cres?["error"]?.ToString() ?? "unknown"}");
                                        if (sketchStatus == null || ((MacroPhaseStatus)sketchStatus).Success)
                                        {
                                            phaseStatus["sketch"] = new MacroPhaseStatus(false, "sketch", Error: $"sketch constraint '{c.Mode}' failed: {cres?["error"]?.ToString() ?? "unknown"}");
                                            sketchStatus = (MacroPhaseStatus?)phaseStatus["sketch"];
                                        }
                                    }
                                }
                            }
                        }

                        // Dimensions
                        if (skipRemaining)
                        {
                            // skipped due to prior sketch_entities parse error
                        }
                        else
                        {
                            var (dimensions, derr) = ParseSketchJson<SketchDimension>(sketch_dimensions, "sketch_dimensions");
                            if (derr != null)
                            {
                                warnings.Add(derr["error"]?.ToString() ?? "sketch_dimensions parse error");
                                phaseStatus["sketch"] = new MacroPhaseStatus(false, "sketch", Error: derr["error"]?.ToString());
                                sketchStatus = (MacroPhaseStatus?)phaseStatus["sketch"];
                            }
                            else if (dimensions != null)
                            {
                                foreach (var d in dimensions)
                                {
                                    var dres = Catch(() => _provider.SketchDimension(
                                        d.Mode, d.Entity1, d.Entity2 ?? "", d.Value, "aligned", null, null));
                                    if (!IsSuccess(dres))
                                    {
                                        warnings.Add($"sketch dimension '{d.Mode}' on {d.Entity1} failed: {dres?["error"]?.ToString() ?? "unknown"}");
                                        if (sketchStatus == null || ((MacroPhaseStatus)sketchStatus).Success)
                                        {
                                            phaseStatus["sketch"] = new MacroPhaseStatus(false, "sketch", Error: $"sketch dimension '{d.Mode}' failed: {dres?["error"]?.ToString() ?? "unknown"}");
                                            sketchStatus = (MacroPhaseStatus?)phaseStatus["sketch"];
                                        }
                                    }
                                }
                            }
                        }

                        // Sketch modify ops (move/rotate/scale/offset/mirror/trim)
                        if (skipRemaining)
                        {
                            // skipped due to prior sketch_entities parse error
                        }
                        else
                        {
                            var (mods, merr) = ParsePhaseJson<SketchModifyOp>(sketch_modify, "sketch_modify");
                            if (merr != null)
                            {
                                warnings.Add(merr["error"]?.ToString() ?? "sketch_modify parse error");
                                phaseStatus["sketch"] = new MacroPhaseStatus(false, "sketch", Error: merr["error"]?.ToString());
                                sketchStatus = (MacroPhaseStatus?)phaseStatus["sketch"];
                            }
                            else if (mods != null)
                            {
                                foreach (var m in mods)
                                {
                                    var op = (m.Op ?? "").Trim().ToLowerInvariant();
                                    Dictionary<string, object?> mres;
                                    switch (op)
                                    {
                                        case "move":
                                            mres = Catch(() => _provider.SketchMove(m.Entities ?? "", m.Dx ?? 0, m.Dy ?? 0, m.Copy ?? false));
                                            break;
                                        case "rotate":
                                            mres = Catch(() => _provider.SketchRotate(m.Entities ?? "", m.Cx ?? 0, m.Cy ?? 0, m.Angle ?? 0, m.Copy ?? false));
                                            break;
                                        case "scale":
                                            mres = Catch(() => _provider.SketchScale(m.Entities ?? "", m.Cx ?? 0, m.Cy ?? 0, m.Factor ?? 1.0));
                                            break;
                                        case "offset":
                                            mres = Catch(() => _provider.SketchOffset(m.Entities ?? "", m.OffsetX ?? 0, m.OffsetY ?? 0, m.IncludeConnected ?? false));
                                            break;
                                        case "mirror":
                                            mres = Catch(() => _provider.SketchMirror(m.Entities ?? "", m.MirrorEntity ?? ""));
                                            break;
                                        case "trim":
                                            mres = Catch(() => _provider.SketchTrim(m.Entity ?? "", m.CuttingEntity ?? "", m.Side ?? "end"));
                                            break;
                                        default:
                                            mres = ToolHelpers.Error($"unknown sketch_modify op '{m.Op}'");
                                            break;
                                    }
                                    if (!IsSuccess(mres))
                                    {
                                        warnings.Add($"sketch_modify '{op}' failed: {mres?["error"]?.ToString() ?? "unknown"}");
                                        if (sketchStatus == null || ((MacroPhaseStatus)sketchStatus).Success)
                                        {
                                            phaseStatus["sketch"] = new MacroPhaseStatus(false, "sketch", Error: $"sketch_modify '{op}' failed: {mres?["error"]?.ToString() ?? "unknown"}");
                                            sketchStatus = (MacroPhaseStatus?)phaseStatus["sketch"];
                                        }
                                    }
                                }
                            }
                        }

                        // Sketch patterns (circular/rect/mirror) — note: key in phase_status is "sketch_pattern"
                        if (skipRemaining)
                        {
                            // skipped due to prior sketch_entities parse error
                            phaseStatus["sketch_pattern"] = new MacroPhaseStatus(false, "sketch_pattern", Error: "skipped due to sketch_entities parse error");
                        }
                        else
                        {
                            var (spats, sperr) = ParsePhaseJson<SketchPatternOp>(sketch_pattern, "sketch_pattern");
                            if (sperr != null)
                            {
                                warnings.Add(sperr["error"]?.ToString() ?? "sketch_pattern parse error");
                                phaseStatus["sketch_pattern"] = new MacroPhaseStatus(false, "sketch_pattern", Error: sperr["error"]?.ToString());
                            }
                            else if (spats != null && spats.Count > 0)
                            {
                                bool sketchPatOk = true;
                                foreach (var p in spats)
                                {
                                    var ptype = (p.Type ?? "").Trim().ToLowerInvariant();
                                    Dictionary<string, object?> pres;
                                    switch (ptype)
                                    {
                                        case "circular":
                                            pres = Catch(() => _provider.SketchCircularPattern(
                                                p.Entities, p.Axis ?? "X", p.Count ?? 6, p.Angle ?? 360.0, p.Fitted ?? true, p.Symmetric ?? false));
                                            break;
                                        case "rectangular":
                                            pres = Catch(() => _provider.SketchRectangularPattern(
                                                p.Entities, p.XAxis ?? "", p.XCount ?? 2, p.XSpacing ?? 1.0, p.YAxis ?? "", p.YCount ?? 1, p.YSpacing ?? 0.0));
                                            break;
                                        case "mirror":
                                            pres = Catch(() => _provider.SketchMirror(p.Entities, p.MirrorEntity ?? ""));
                                            break;
                                        default:
                                            pres = ToolHelpers.Error($"unknown sketch_pattern type '{p.Type}'");
                                            break;
                                    }
                                    if (!IsSuccess(pres))
                                    {
                                        warnings.Add($"sketch_pattern '{ptype}' failed: {pres?["error"]?.ToString() ?? "unknown"}");
                                        if (sketchPatOk)
                                        {
                                            phaseStatus["sketch_pattern"] = new MacroPhaseStatus(false, "sketch_pattern", Error: $"sketch_pattern '{ptype}' failed: {pres?["error"]?.ToString() ?? "unknown"}");
                                            sketchPatOk = false;
                                        }
                                    }
                                }
                                // record pattern phase separately — success only on clean path
                                if (sketchPatOk)
                                    phaseStatus["sketch_pattern"] = new MacroPhaseStatus(true, "sketch_pattern", EntityCount: spats.Count);
                            }
                        }

                        if (sketchStatus == null)
                            sketchStatus = new MacroPhaseStatus(true, "sketch", EntityCount: sketchEntityCount, ConstraintCount: sketchConstraintCount);

                    }
                }
                catch (Exception ex)
                {
                    warnings.Add($"Sketch phase unexpected error: {ex.Message}");
                    sketchStatus = new MacroPhaseStatus(false, "sketch", Error: ex.Message);
                }
            }

            if (sketchStatus != null)
                phaseStatus["sketch"] = sketchStatus;

            // If we drew entities, consider geometry started (2D)
            if (sketchEntityCount > 0)
                geometryCreated = true;

            // ── 3. Feature phase (type dispatch) ─────────────────────────────────
            MacroPhaseStatus? featureStatus = null;
            string? ft = string.IsNullOrWhiteSpace(feature_type) ? null : feature_type.Trim().ToLowerInvariant();
            if (!string.IsNullOrWhiteSpace(ft))
            {
                var profRes = Catch(() => _provider.SketchProfiles());
                int profileCount = 0;
                if (!IsSuccess(profRes))
                {
                    var err = profRes?["error"]?.ToString() ?? "unknown";
                    warnings.Add($"feature requested but SketchProfiles() failed: {err} — skipping feature phase");
                    featureStatus = new MacroPhaseStatus(false, "feature", Error: $"SketchProfiles() failed: {err}");
                }
                else if (profRes != null && profRes.TryGetValue("profile_count", out var pcObj) && pcObj is int pc && pc > 0)
                    profileCount = pc;

                if (profileCount == 0 && featureStatus == null)
                {
                    warnings.Add("feature requested but no closed profiles detected via SketchProfiles() — skipping feature phase");
                    featureStatus = new MacroPhaseStatus(false, "feature", Error: "No closed sketch profile found (profile_count == 0 after sketch phase). Provide sketch_entities producing a closed profile or draw one before requesting a feature.");
                }

                if (featureStatus == null)
                {
                    try
                    {
                        Dictionary<string, object?> featRes;
                        switch (ft)
                        {
                            case "extrude":
                                featRes = Catch(() => _provider.Extrude(
                                    feature_profile ?? "1",
                                    feature_distance ?? 10.0,
                                    feature_direction ?? "positive",
                                    feature_taper ?? 0.0,
                                    feature_operation ?? "new_body"));
                                break;
                            case "revolve":
                                featRes = Catch(() => _provider.Revolve(
                                    feature_profile ?? "1",
                                    feature_axis ?? "Y",
                                    feature_angle ?? 360.0,
                                    feature_direction ?? "positive",
                                    feature_operation ?? "new_body"));
                                break;
                            case "sweep":
                                featRes = Catch(() => _provider.Sweep(
                                    feature_profile ?? "1",
                                    feature_path ?? "1",
                                    "path",
                                    feature_operation ?? "new_body",
                                    feature_taper ?? 0.0,
                                    "",
                                    ""));
                                break;
                            case "loft":
                                featRes = Catch(() => _provider.Loft(
                                    feature_profiles ?? "1,2",
                                    feature_operation ?? "new_body"));
                                break;
                            case "coil":
                                featRes = Catch(() => _provider.Coil(
                                    feature_profile ?? "1",
                                    feature_axis ?? "Y",
                                    feature_pitch ?? 1.0,
                                    feature_revolutions ?? 5.0,
                                    feature_operation ?? "new_body"));
                                break;
                            case "rib":
                                featRes = Catch(() => _provider.Rib(
                                    feature_profile ?? "1",
                                    feature_thickness ?? 0.5,
                                    feature_direction ?? "normal",
                                    feature_operation ?? "new_body"));
                                break;
                            default:
                                featRes = ToolHelpers.Error($"unknown feature_type '{feature_type}' (supported: extrude,revolve,sweep,loft,coil,rib)");
                                break;
                        }

                        if (IsSuccess(featRes))
                        {
                            geometryCreated = true;
                            featureStatus = new MacroPhaseStatus(true, "feature", FeatureType: ft, FeatureName: null);
                        }
                        else
                        {
                            warnings.Add($"Feature '{ft}' failed: {featRes?["error"]?.ToString() ?? "unknown"}");
                            featureStatus = new MacroPhaseStatus(false, "feature", Error: featRes?["error"]?.ToString(), FeatureType: ft);
                        }
                    }
                    catch (Exception ex)
                    {
                        warnings.Add($"Feature phase error: {ex.Message}");
                        featureStatus = new MacroPhaseStatus(false, "feature", Error: ex.Message, FeatureType: ft);
                    }
                }
            }
            phaseStatus["feature"] = featureStatus;

            // ── 4. 3D Pattern phase ──────────────────────────────────────────────
            MacroPhaseStatus? pat3dStatus = null;
            var (p3ds, p3derr) = ParsePhaseJson<Pattern3DOp>(pattern_3d, "pattern_3d");
            if (p3derr != null)
            {
                warnings.Add(p3derr["error"]?.ToString() ?? "pattern_3d parse error");
                pat3dStatus = new MacroPhaseStatus(false, "pattern_3d", Error: p3derr["error"]?.ToString());
            }
            else if (p3ds != null && p3ds.Count > 0)
            {
                int okCount = 0;
                foreach (var p in p3ds)
                {
                    var ptype = (p.Type ?? "").Trim().ToLowerInvariant();
                    Dictionary<string, object?> pres;
                    switch (ptype)
                    {
                        case "circular":
                            pres = Catch(() => _provider.CircularPattern(
                                p.Profile, p.Axis ?? "Y", p.Count ?? 6, p.Angle ?? 360.0, p.FitWithinAngle ?? true, p.NaturalDirection ?? true));
                            break;
                        case "rectangular":
                            pres = Catch(() => _provider.RectangularPattern(
                                p.Profile, p.XAxis ?? "", p.XCount ?? 2, p.XSpacing ?? 1.0, p.YAxis ?? "", p.YCount ?? 1, p.YSpacing ?? 0.0));
                            break;
                        case "mirror":
                            pres = Catch(() => _provider.MirrorFeature(p.Profile, p.MirrorPlane ?? "YZ"));
                            break;
                        default:
                            pres = ToolHelpers.Error($"unknown pattern_3d type '{p.Type}'");
                            break;
                    }
                    if (IsSuccess(pres))
                        okCount++;
                    else
                        warnings.Add($"pattern_3d '{ptype}' on {p.Profile} failed: {pres?["error"]?.ToString() ?? "unknown"}");
                }
                if (okCount > 0)
                {
                    geometryCreated = true;
                    pat3dStatus = new MacroPhaseStatus(true, "pattern_3d", EntityCount: okCount);
                }
                else
                {
                    pat3dStatus = new MacroPhaseStatus(false, "pattern_3d", Error: $"all {p3ds.Count} pattern_3d operations failed", EntityCount: 0);
                }
            }
            phaseStatus["pattern_3d"] = pat3dStatus;

            // ── 5. Modify 3D phase ───────────────────────────────────────────────
            MacroPhaseStatus? mod3dStatus = null;
            var (m3ds, m3derr) = ParsePhaseJson<Modify3DOp>(modify_3d, "modify_3d");
            if (m3derr != null)
            {
                warnings.Add(m3derr["error"]?.ToString() ?? "modify_3d parse error");
                mod3dStatus = new MacroPhaseStatus(false, "modify_3d", Error: m3derr["error"]?.ToString());
            }
            else if (m3ds != null && m3ds.Count > 0)
            {
                int okCount = 0;
                foreach (var m in m3ds)
                {
                    var op = (m.Op ?? "").Trim().ToLowerInvariant();
                    Dictionary<string, object?> mres;
                    switch (op)
                    {
                        case "fillet":
                            mres = Catch(() => _provider.Fillet(m.Edges ?? "", m.Radius ?? 0.1, m.Mode ?? "constant"));
                            break;
                        case "chamfer":
                            mres = Catch(() => _provider.Chamfer(m.Edges ?? "", m.Distance ?? 0.1, m.Mode ?? "equal_distance"));
                            break;
                        case "shell":
                            mres = Catch(() => _provider.Shell(m.Faces ?? "", m.Thickness ?? 0.1, m.Direction ?? "inside", m.Operation ?? "new_body"));
                            break;
                        case "draft":
                            mres = Catch(() => _provider.Draft(m.Faces ?? "", m.Angle ?? 5.0, m.Mode ?? "fixed_edge", m.PullDirection ?? "z", m.FixedEntity ?? ""));
                            break;
                        case "thread":
                            mres = Catch(() => _provider.Thread(m.Face ?? "", m.Specification ?? "M10x1.5", m.Direction ?? "right"));
                            break;
                        case "split":
                            mres = Catch(() => _provider.Split(m.SplitTool ?? "", m.RemoveSide ?? "positive", m.TargetBody ?? ""));
                            break;
                        case "hole":
                            mres = Catch(() => _provider.Hole(m.X ?? 0, m.Y ?? 0, m.Diameter ?? 1.0, m.Depth ?? 1.0, m.HoleType ?? "drilled", m.Operation ?? "join"));
                            break;
                        default:
                            mres = ToolHelpers.Error($"unknown modify_3d op '{m.Op}'");
                            break;
                    }
                    if (IsSuccess(mres))
                        okCount++;
                    else
                        warnings.Add($"modify_3d '{op}' failed: {mres?["error"]?.ToString() ?? "unknown"}");
                }
                if (okCount > 0)
                {
                    geometryCreated = true;
                    mod3dStatus = new MacroPhaseStatus(true, "modify_3d", EntityCount: okCount);
                }
                else
                {
                    mod3dStatus = new MacroPhaseStatus(false, "modify_3d", Error: $"all {m3ds.Count} modify_3d operations failed", EntityCount: 0);
                }
            }
            phaseStatus["modify_3d"] = mod3dStatus;

            // ── 6. iProperties (best-effort, after geometry) ─────────────────────
            if (!string.IsNullOrWhiteSpace(part_number))
            {
                var ip = Catch(() => _provider.IPropertySet("Part Number", part_number, "Summary"));
                if (!IsSuccess(ip)) warnings.Add($"part_number iproperty failed: {ip?["error"]?.ToString() ?? "unknown"}");
            }
            if (!string.IsNullOrWhiteSpace(description))
            {
                var ip = Catch(() => _provider.IPropertySet("Description", description, "Summary"));
                if (!IsSuccess(ip)) warnings.Add($"description iproperty failed: {ip?["error"]?.ToString() ?? "unknown"}");
            }
            if (!string.IsNullOrWhiteSpace(material))
            {
                var ip = Catch(() => _provider.IPropertyCustomSet("Material", material));
                if (!IsSuccess(ip)) warnings.Add($"material custom iproperty failed: {ip?["error"]?.ToString() ?? "unknown"}");
            }

            // ── 7. Verify phase (ALWAYS runs, best-effort, never fails the macro) ─
            try { tree = _provider.GetFeatureTree(); }
            catch (Exception ex) { warnings.Add($"get_feature_tree failed: {ex.Message}"); }

            try { bbox = _provider.GetBoundingBox(""); }
            catch (Exception ex) { warnings.Add($"get_bounding_box failed: {ex.Message}"); }

            try { parameters = _provider.ParamList(); }
            catch (Exception ex) { warnings.Add($"param_list failed: {ex.Message}"); }

            foreach (string view in new[] { "Iso", "Top" })
            {
                try
                {
                    var cap = _provider.CaptureViewportImage(view: view, width: 800, height: 600, format: "png");
                    if (IsSuccess(cap))
                        viewportImages.Add(cap);
                    else
                        warnings.Add($"capture_viewport_image({view}) failed: {cap?["error"]?.ToString() ?? "unknown"}");
                }
                catch (Exception ex)
                {
                    warnings.Add($"capture_viewport_image({view}) failed: {ex.Message}");
                }
            }

            // compute aggregate success so top level reflects real outcome (not always true)
            bool aggregateSuccess = geometryCreated || phaseStatus.Values.Any(s => s is MacroPhaseStatus mps && mps.Success);

            // ── 8. Final envelope composition ────────────────────────────────────
            var result = new Dictionary<string, object?>
            {
                ["success"] = aggregateSuccess,
                ["geometry_created"] = geometryCreated,
                ["document_state"] = documentState,
                ["tree"] = tree,
                ["bounding_box"] = bbox,
                ["parameters"] = parameters,
                ["viewport_images"] = viewportImages,
                ["warnings"] = warnings,
                ["next"] = "ready for additional operations (fillets, patterns, holes, iproperties, or export)"
            };

            result["phase_status"] = phaseStatus;

            return result;
        }
        catch (InventorConnectionException ex)
        {
            return ToolHelpers.Error($"Inventor connection error in macro_god_part: {ex.Message}");
        }
        catch (InventorComException ex)
        {
            return ToolHelpers.Error($"Inventor COM error in macro_god_part: {ex.Message}");
        }
        catch (Exception ex)
        {
            return ToolHelpers.Error($"Unexpected error in macro_god_part: {ex.Message}");
        }
    }
}
