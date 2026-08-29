using System.Text.Json;
using McpCad.Core;
using McpCad.Core.Exceptions;
using McpCad.Core.Models;
using McpCad.Onshape;
using McpCad.Onshape.Builders;
using ModelContextProtocol.Server;
using System.ComponentModel;

namespace McpCad.Tools.Onshape;

/// <summary>
/// Atomic MCP tools for Onshape (cloud CAD). Consumes <see cref="ICadWebProvider"/>.
/// API keys are loaded at server startup from configuration / environment / user secrets — not from MCP tool arguments.
/// </summary>
[McpServerToolType]
public class OnshapeAtomicTools(ICadWebProvider provider)
{
    [McpServerTool, Description("Connect / mark Onshape session ready.")]
    public Dictionary<string, object?> onshape_connect()
        => Catch(provider.Connect);

    [McpServerTool, Description("Disconnect Onshape session marker.")]
    public Dictionary<string, object?> onshape_disconnect()
        => Catch(provider.Disconnect);

    [McpServerTool, Description("Onshape provider health.")]
    public Dictionary<string, object?> onshape_health()
        => Catch(provider.Health);

    [McpServerTool, Description("Create a new Onshape document from scratch.")]
    public Dictionary<string, object?> onshape_doc_create(string name)
        => Catch(() => provider.DocCreate(name));

    [McpServerTool, Description("Open an Onshape document by document id.")]
    public Dictionary<string, object?> onshape_doc_open(string document_id)
        => Catch(() => provider.DocOpen(document_id));

    [McpServerTool, Description("List workspaces for a document.")]
    public Dictionary<string, object?> onshape_ws_list(string? document_id = null)
        => Catch(() => provider.WsList(document_id));

    [McpServerTool, Description("Get current workspace for a document.")]
    public Dictionary<string, object?> onshape_ws_current(string document_id)
        => Catch(() => provider.WsCurrent(document_id));

    [McpServerTool, Description("List elements in a document workspace/version.")]
    public Dictionary<string, object?> onshape_element_list(string document_id, string wvm, string wvm_id)
        => Catch(() => provider.ElementList(document_id, wvm, wvm_id));

    [McpServerTool, Description("Get element metadata by address JSON (CadElementAddress).")]
    public Dictionary<string, object?> onshape_element_get(string address_json)
        => Catch(() => provider.ElementGet(ParseAddress(address_json)));

    [McpServerTool, Description("Create a Part Studio or other element.")]
    public Dictionary<string, object?> onshape_element_create(
        string address_json, string name, string element_type = "PARTSTUDIO")
        => Catch(() => provider.ElementCreate(ParseAddress(address_json), name, element_type));

    [McpServerTool, Description("Rename an element.")]
    public Dictionary<string, object?> onshape_element_rename(string address_json, string name)
        => Catch(() => provider.ElementRename(ParseAddress(address_json), name));

    [McpServerTool, Description("Create a rectangle sketch in a Part Studio using BTMSketch. Coordinates in cm.")]
    public Dictionary<string, object?> onshape_sketch_rectangle(
        string address_json,
        double x1_cm,
        double y1_cm,
        double x2_cm,
        double y2_cm,
        string? plane = null)
        => Catch(() =>
        {
            var address = ParseAddress(address_json);
            using var sketch = OnshapeSketchBuilder.BuildRectangle(
                OnshapeUnitConversion.CmToMeters(x1_cm),
                OnshapeUnitConversion.CmToMeters(y1_cm),
                OnshapeUnitConversion.CmToMeters(x2_cm),
                OnshapeUnitConversion.CmToMeters(y2_cm),
                plane);
            var request = JsonSerializer.Deserialize<Dictionary<string, object?>>(sketch.RootElement.GetRawText())
                          ?? new Dictionary<string, object?>();
            return provider.SketchReplace(address, request);
        });

    [McpServerTool, Description("Create a point sketch in a Part Studio using BTMSketch. Coordinates in cm.")]
    public Dictionary<string, object?> onshape_sketch_point(
        string address_json,
        double x_cm,
        double y_cm,
        string? plane = null,
        bool construction = false)
        => ReplaceSketch(address_json, () => OnshapeSketchBuilder.BuildPoint(
            OnshapeUnitConversion.CmToMeters(x_cm),
            OnshapeUnitConversion.CmToMeters(y_cm),
            plane,
            construction));

    [McpServerTool, Description("Create a line sketch in a Part Studio using BTMSketch. Coordinates and optional length dimension in cm.")]
    public Dictionary<string, object?> onshape_sketch_line(
        string address_json,
        double x1_cm,
        double y1_cm,
        double x2_cm,
        double y2_cm,
        string? plane = null,
        double? length_cm = null)
        => ReplaceSketch(address_json, () =>
        {
            ValidateOptionalDimension(length_cm, nameof(length_cm));
            return OnshapeSketchBuilder.BuildLine(
                OnshapeUnitConversion.CmToMeters(x1_cm),
                OnshapeUnitConversion.CmToMeters(y1_cm),
                OnshapeUnitConversion.CmToMeters(x2_cm),
                OnshapeUnitConversion.CmToMeters(y2_cm),
                plane,
                length_cm is double value ? OnshapeUnitConversion.CmToMeters(value) : null);
        });

    [McpServerTool, Description("Create a circle sketch in a Part Studio using BTMSketch. Coordinates, radius, and optional diameter dimension in cm.")]
    public Dictionary<string, object?> onshape_sketch_circle(
        string address_json,
        double cx_cm,
        double cy_cm,
        double radius_cm,
        string? plane = null,
        double? diameter_cm = null)
        => ReplaceSketch(address_json, () =>
        {
            ValidateOptionalDimension(diameter_cm, nameof(diameter_cm));
            return OnshapeSketchBuilder.BuildCircle(
                OnshapeUnitConversion.CmToMeters(cx_cm),
                OnshapeUnitConversion.CmToMeters(cy_cm),
                OnshapeUnitConversion.CmToMeters(radius_cm),
                plane,
                diameter_cm is double value ? OnshapeUnitConversion.CmToMeters(value) : null);
        });

    [McpServerTool, Description("Create a one-row linear pattern of circles in a Part Studio using BTMSketch. Coordinates, radius, and spacing are in cm.")]
    public Dictionary<string, object?> onshape_sketch_linear_pattern_circle(
        string address_json,
        double cx_cm,
        double cy_cm,
        double radius_cm,
        int columns,
        double spacing_cm,
        string? plane = null)
        => ReplaceSketch(address_json, () => OnshapeSketchBuilder.BuildLinearPatternCircle(
            OnshapeUnitConversion.CmToMeters(cx_cm),
            OnshapeUnitConversion.CmToMeters(cy_cm),
            OnshapeUnitConversion.CmToMeters(radius_cm),
            columns,
            OnshapeUnitConversion.CmToMeters(spacing_cm),
            plane));

    [McpServerTool, Description("Create a circular pattern of lines in a Part Studio using BTMSketch. Coordinates, orbit radius, and line length are in cm.")]
    public Dictionary<string, object?> onshape_sketch_circular_pattern_line(
        string address_json,
        double center_x_cm,
        double center_y_cm,
        double orbit_radius_cm,
        double line_length_cm,
        int instances,
        string? plane = null)
        => ReplaceSketch(address_json, () => OnshapeSketchBuilder.BuildCircularPatternLine(
            OnshapeUnitConversion.CmToMeters(center_x_cm),
            OnshapeUnitConversion.CmToMeters(center_y_cm),
            OnshapeUnitConversion.CmToMeters(orbit_radius_cm),
            OnshapeUnitConversion.CmToMeters(line_length_cm),
            instances,
            plane));

    [McpServerTool, Description("Create an ellipse sketch in a Part Studio using BTMSketch. Coordinates, radii, and optional diameter dimensions are in cm; rotation is in degrees.")]
    public Dictionary<string, object?> onshape_sketch_ellipse(
        string address_json,
        double cx_cm,
        double cy_cm,
        double major_radius_cm,
        double minor_radius_cm,
        double rotation_deg = 0,
        string? plane = null,
        double? major_diameter_cm = null,
        double? minor_diameter_cm = null)
        => ReplaceSketch(address_json, () =>
        {
            ValidateOptionalDimension(major_diameter_cm, nameof(major_diameter_cm));
            ValidateOptionalDimension(minor_diameter_cm, nameof(minor_diameter_cm));
            return OnshapeSketchBuilder.BuildEllipse(
                OnshapeUnitConversion.CmToMeters(cx_cm),
                OnshapeUnitConversion.CmToMeters(cy_cm),
                OnshapeUnitConversion.CmToMeters(major_radius_cm),
                OnshapeUnitConversion.CmToMeters(minor_radius_cm),
                rotation_deg,
                plane,
                major_diameter_cm is double majorDiameter ? OnshapeUnitConversion.CmToMeters(majorDiameter) : null,
                minor_diameter_cm is double minorDiameter ? OnshapeUnitConversion.CmToMeters(minorDiameter) : null);
        });

    [McpServerTool, Description("Create an arc sketch in a Part Studio using BTMSketch. Coordinates in cm and angles in degrees.")]
    public Dictionary<string, object?> onshape_sketch_arc(
        string address_json,
        double cx_cm,
        double cy_cm,
        double radius_cm,
        double start_angle_deg,
        double end_angle_deg,
        string? plane = null)
        => ReplaceSketch(address_json, () => OnshapeSketchBuilder.BuildArc(
            OnshapeUnitConversion.CmToMeters(cx_cm),
            OnshapeUnitConversion.CmToMeters(cy_cm),
            OnshapeUnitConversion.CmToMeters(radius_cm),
            start_angle_deg,
            end_angle_deg,
            plane));

    [McpServerTool, Description("Create an open or closed axis-aligned polyline sketch in a Part Studio using BTMSketch. Points JSON contains x_cm/y_cm coordinates.")]
    public Dictionary<string, object?> onshape_sketch_polyline(
        string address_json,
        string points_json,
        bool closed = false,
        string? plane = null)
        => ReplaceSketch(address_json, () => OnshapeSketchBuilder.BuildPolyline(
            ParsePolylinePoints(points_json)
                .Select(point => new SketchPoint(
                    OnshapeUnitConversion.CmToMeters(point.X),
                    OnshapeUnitConversion.CmToMeters(point.Y)))
                .ToArray(),
            closed,
            plane));

    [McpServerTool, Description("Create a non-periodic interpolated spline sketch in a Part Studio using BTMSketch. Points JSON contains x_cm/y_cm coordinates.")]
    public Dictionary<string, object?> onshape_sketch_spline(
        string address_json,
        string points_json,
        string? plane = null,
        bool construction = false)
        => ReplaceSketch(address_json, () => OnshapeSketchBuilder.BuildSpline(
            ParseSplinePoints(points_json)
                .Select(point => new SketchPoint(
                    OnshapeUnitConversion.CmToMeters(point.X),
                    OnshapeUnitConversion.CmToMeters(point.Y)))
                .ToArray(),
            plane,
            construction));

    [McpServerTool, Description("Extrude a sketch profile by a given depth in cm.")]
    public Dictionary<string, object?> onshape_extrude(
        string address_json,
        string sketch_feature_id,
        double depth_cm,
        string direction = "positive",
        string boolean_op = "new")
        => Catch(() =>
        {
            var address = ParseAddress(address_json);
            using var envelope = OnshapeFeatureTemplates.ExtrudeFromSketch(
                sketch_feature_id,
                OnshapeUnitConversion.CmToMeters(depth_cm),
                direction,
                boolean_op);
            return provider.FeatureAdd(address, "extrude", null, envelope.RootElement.GetRawText());
        });

    [McpServerTool, Description("List features in a Part Studio (analogous to Inventor sketch_profiles).")]
    public Dictionary<string, object?> onshape_feature_list(string address_json)
        => Catch(() => provider.FeatureList(ParseAddress(address_json)));

    [McpServerTool, Description("(P1) Revolve a sketch profile by an angle in degrees using the Onshape feature API.")]
    public Dictionary<string, object?> onshape_revolve(
        string address_json,
        string sketch_feature_id,
        string axis_sketch_feature_id,
        double angle_deg,
        string direction = "positive",
        string boolean_op = "new")
        => Catch(() =>
        {
            var address = ParseAddress(address_json);
            using var envelope = OnshapeFeatureTemplates.RevolveFromSketch(
                sketch_feature_id, axis_sketch_feature_id, angle_deg, direction, boolean_op);
            return provider.FeatureAdd(address, "revolve", null, envelope.RootElement.GetRawText());
        });

    [McpServerTool, Description("Create a feature-level circular pattern around an axis sketch edge using the Onshape feature API.")]
    public Dictionary<string, object?> onshape_circular_pattern(
        string address_json,
        string source_feature_id,
        string axis_sketch_feature_id,
        int instance_count,
        double total_angle_deg,
        bool opposite_direction = false,
        bool equal_spacing = true)
        => Catch(() =>
        {
            var address = ParseAddress(address_json);
            using var envelope = OnshapeFeatureTemplates.CircularPatternFromFeature(
                source_feature_id,
                axis_sketch_feature_id,
                instance_count,
                total_angle_deg,
                opposite_direction,
                equal_spacing);
            return provider.FeatureAdd(address, "circularPattern", null, envelope.RootElement.GetRawText());
        });

    [McpServerTool, Description("Create a rectangular pattern of a source feature along one or two sketch-defined directions using the Onshape feature API.")]
    public Dictionary<string, object?> onshape_rectangular_pattern(
            string address_json,
            string source_feature_id,
            string direction_one_sketch_feature_id,
            double distance_cm,
            int instance_count,
            string? direction_two_sketch_feature_id = null,
            double? distance_two_cm = null,
            int? instance_count_two = null,
            bool opposite_direction = false,
            bool is_centered = false,
            bool opposite_direction_two = false,
            bool is_centered_two = false)
            => Catch(() =>
            {
                var address = ParseAddress(address_json);
                using var envelope = OnshapeFeatureTemplates.RectangularPatternFromFeature(
                    source_feature_id,
                    direction_one_sketch_feature_id,
                    OnshapeUnitConversion.CmToMeters(distance_cm),
                    instance_count,
                    direction_two_sketch_feature_id,
                    distance_two_cm is double value ? OnshapeUnitConversion.CmToMeters(value) : null,
                    instance_count_two,
                    opposite_direction,
                    is_centered,
                    opposite_direction_two,
                    is_centered_two);
                return provider.FeatureAdd(address, "linearPattern", null, envelope.RootElement.GetRawText());
            });

        [McpServerTool, Description("Mirror a source feature across a canonical Top, Front, or Right plane using the Onshape feature API.")]
        public Dictionary<string, object?> onshape_mirror_feature(
        string address_json,
        string source_feature_id,
        string mirror_plane)
        => Catch(() =>
        {
            var address = ParseAddress(address_json);
            using var envelope = OnshapeFeatureTemplates.MirrorFeature(source_feature_id, mirror_plane);
            return provider.FeatureAdd(address, "mirror", null, envelope.RootElement.GetRawText());
        });

    [McpServerTool, Description("(P1) Sweep a profile sketch along a path sketch using the Onshape feature API.")]
    public Dictionary<string, object?> onshape_sweep(
        string address_json,
        string profile_sketch_feature_id,
        string path_sketch_feature_id,
        string boolean_op = "new")
        => Catch(() =>
        {
            var address = ParseAddress(address_json);
            using var envelope = OnshapeFeatureTemplates.SweepFromSketch(
                profile_sketch_feature_id, path_sketch_feature_id, boolean_op);
            return provider.FeatureAdd(address, "sweep", null, envelope.RootElement.GetRawText());
        });

    [McpServerTool, Description("(P1) Apply an equal-radius fillet to Part Studio edges; radius is in cm.")]
    public Dictionary<string, object?> onshape_fillet(
        string address_json,
        string[] edge_ids,
        double radius_cm)
        => Catch(() =>
        {
            var address = ParseAddress(address_json);
            using var envelope = OnshapeFeatureTemplates.Fillet(
                edge_ids, OnshapeUnitConversion.CmToMeters(radius_cm));
            return provider.FeatureAdd(address, "fillet", null, envelope.RootElement.GetRawText());
        });

    [McpServerTool, Description("(P1) Apply an equal-distance chamfer to Part Studio edges; distance is in cm.")]
    public Dictionary<string, object?> onshape_chamfer(
        string address_json,
        string[] edge_ids,
        double distance_cm)
        => Catch(() =>
        {
            var address = ParseAddress(address_json);
            using var envelope = OnshapeFeatureTemplates.Chamfer(
                edge_ids, OnshapeUnitConversion.CmToMeters(distance_cm));
            return provider.FeatureAdd(address, "chamfer", null, envelope.RootElement.GetRawText());
        });

    [McpServerTool, Description("(P1) Shell a Part Studio by removing selected faces; thickness is in cm.")]
    public Dictionary<string, object?> onshape_shell(
        string address_json,
        string[] face_ids,
        double thickness_cm,
        bool opposite_direction = false)
        => Catch(() =>
        {
            var address = ParseAddress(address_json);
            using var envelope = OnshapeFeatureTemplates.Shell(
                face_ids, OnshapeUnitConversion.CmToMeters(thickness_cm), opposite_direction);
            return provider.FeatureAdd(address, "shell", null, envelope.RootElement.GetRawText());
        });

    [McpServerTool, Description("List instances in an Onshape assembly.")]
    public Dictionary<string, object?> onshape_asm_list_instances(string address_json)
        => Catch(() => provider.AssemblyListInstances(ParseAddress(address_json)));

    [McpServerTool, Description("Create an instance of a part in an Onshape assembly.")]
    public Dictionary<string, object?> onshape_asm_create_instance(
        string address_json,
        string part_document_id,
        string part_element_id)
        => Catch(() => provider.AssemblyCreateInstance(
            ParseAddress(address_json), part_document_id, part_element_id));

    [McpServerTool, Description("Delete an instance from an Onshape assembly.")]
    public Dictionary<string, object?> onshape_asm_delete_instance(string address_json, string instance_id)
        => Catch(() => provider.AssemblyDeleteInstance(ParseAddress(address_json), instance_id));

    [McpServerTool, Description("DEPRECATED: use onshape_sketch_rectangle. Creates a default 10cm rectangle sketch when corners omitted.")]
    public Dictionary<string, object?> create_sketch(
        string address_json,
        double? x1_cm = null,
        double? y1_cm = null,
        double? x2_cm = null,
        double? y2_cm = null)
        => onshape_sketch_rectangle(
            address_json,
            x1_cm ?? 0,
            y1_cm ?? 0,
            x2_cm ?? 10,
            y2_cm ?? 10);

    [McpServerTool, Description("DEPRECATED: use onshape_extrude. Extrudes using legacy distance_cm; requires sketch_feature_id.")]
    public Dictionary<string, object?> add_extrude(
        string address_json,
        double distance_cm,
        string direction = "positive",
        string sketch_feature_id = "sketch-1")
        => onshape_extrude(address_json, sketch_feature_id, distance_cm, direction);

    [McpServerTool, Description("Export Part Studio to STEP (polls translation, blocking).")]
    public Dictionary<string, object?> onshape_export_step(string address_json)
        => Catch(() => provider.ExportStep(ParseAddress(address_json)));

    [McpServerTool, Description("Export Part Studio to STL synchronously.")]
    public Dictionary<string, object?> onshape_export_stl(string address_json)
        => Catch(() => provider.ExportStl(ParseAddress(address_json)));

    [McpServerTool, Description("Copy an Onshape document.")]
    public Dictionary<string, object?> onshape_doc_copy(string document_id, string? name = null)
        => Catch(() => provider.DocCopy(document_id, name));

    [McpServerTool, Description("Delete an Onshape document (requires confirm=true; irreversible).")]
    public Dictionary<string, object?> onshape_doc_delete(string document_id, bool confirm = false)
        => Catch(() => provider.DocDelete(document_id, confirm));

    [McpServerTool, Description("Create a document version.")]
    public Dictionary<string, object?> onshape_version_create(string document_id, string? name = null)
        => Catch(() => provider.VersionCreate(document_id, name));

    [McpServerTool, Description("Read microversion for an element.")]
    public Dictionary<string, object?> onshape_microversion_get(string address_json)
        => Catch(() => provider.MicroversionGet(ParseAddress(address_json)));

    private static CadElementAddress ParseAddress(string addressJson) =>
        CadElementAddress.Deserialize(addressJson);

    private static IReadOnlyList<SketchPoint> ParseSplinePoints(string pointsJson) =>
        ParseSketchPoints(pointsJson);

    private static IReadOnlyList<SketchPoint> ParsePolylinePoints(string pointsJson) =>
        ParseSketchPoints(pointsJson);

    private static IReadOnlyList<SketchPoint> ParseSketchPoints(string pointsJson)
    {
        using var document = JsonDocument.Parse(pointsJson);
        if (document.RootElement.ValueKind != JsonValueKind.Array)
        {
            throw new ArgumentException("points_json must be a JSON array.", nameof(pointsJson));
        }

        var points = new List<SketchPoint>();
        foreach (var point in document.RootElement.EnumerateArray())
        {
            if (point.ValueKind != JsonValueKind.Object ||
                !point.TryGetProperty("x_cm", out var x) ||
                !point.TryGetProperty("y_cm", out var y) ||
                x.ValueKind != JsonValueKind.Number ||
                y.ValueKind != JsonValueKind.Number ||
                !x.TryGetDouble(out var xCm) ||
                !y.TryGetDouble(out var yCm) ||
                !double.IsFinite(xCm) ||
                !double.IsFinite(yCm))
            {
                throw new ArgumentException(
                    "Each points_json item must contain finite numeric x_cm and y_cm properties.",
                    nameof(pointsJson));
            }

            points.Add(new SketchPoint(xCm, yCm));
        }

        return points;
    }

    private static void ValidateOptionalDimension(double? dimensionCm, string parameterName)
    {
        if (dimensionCm is double value && (!double.IsFinite(value) || value <= 0))
        {
            throw new ArgumentOutOfRangeException(parameterName, "Dimension must be finite and greater than zero.");
        }
    }

    private Dictionary<string, object?> ReplaceSketch(
        string addressJson,
        Func<JsonDocument> buildSketch)
        => Catch(() =>
        {
            using var sketch = buildSketch();
            var request = JsonSerializer.Deserialize<Dictionary<string, object?>>(sketch.RootElement.GetRawText())
                          ?? new Dictionary<string, object?>();
            return provider.SketchReplace(ParseAddress(addressJson), request);
        });

    private static Dictionary<string, object?> Catch(Func<Dictionary<string, object?>> action)
    {
        try
        {
            return action();
        }
        catch (CadProviderException ex)
        {
            return ToolHelpers.Error(ex.Message);
        }
        catch (Exception ex)
        {
            return ToolHelpers.Error($"Unexpected error: {ex.Message}");
        }
    }
}
