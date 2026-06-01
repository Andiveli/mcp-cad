using McpCad.Core;
using McpCad.Core.Exceptions;
using ModelContextProtocol.Server;
using System.ComponentModel;

namespace McpCad.Tools;

/// <summary>
/// Atomic MCP tools — one method per CAD operation.
/// Each method delegates to ICadProvider and catches COM exceptions,
/// returning standardized error dictionaries per the error contract (D7).
/// Tool method names use snake_case to match Python tool names (S5.1).
/// </summary>
[McpServerToolType]
public class AtomicTools(ICadProvider provider)
{
    // ── Connection (3) ────────────────────────────────────────────────

    [McpServerTool, Description("Connect to a running Autodesk Inventor instance.")]
    public Dictionary<string, object?> inventor_connect()
        => Catch(provider.Connect);

    [McpServerTool, Description("Disconnect from Inventor without closing the application.")]
    public Dictionary<string, object?> inventor_disconnect()
        => Catch(provider.Disconnect);

    [McpServerTool, Description("Check Inventor connection health and document state.")]
    public Dictionary<string, object?> inventor_health()
        => Catch(provider.Health);

    // ── Documents (6) ────────────────────────────────────────────────

    [McpServerTool, Description("Open an existing Inventor document.")]
    public Dictionary<string, object?> doc_open(string path)
        => Catch(() => provider.DocOpen(path));

    [McpServerTool, Description("Create a new part document.")]
    public Dictionary<string, object?> doc_new_part(string template = "")
        => Catch(() => provider.DocNewPart(template));

    [McpServerTool, Description("Create a new assembly document.")]
    public Dictionary<string, object?> doc_new_assembly(string template = "")
        => Catch(() => provider.DocNewAssembly(template));

    [McpServerTool, Description("Save the active document.")]
    public Dictionary<string, object?> doc_save()
        => Catch(provider.DocSave);

    [McpServerTool, Description("Save the active document to a new path.")]
    public Dictionary<string, object?> doc_save_as(string path)
        => Catch(() => provider.DocSaveAs(path));

    [McpServerTool, Description("Close the active document.")]
    public Dictionary<string, object?> doc_close(bool save = true)
        => Catch(() => provider.DocClose(save));

    // ── Sketch (18) ────────────────────────────────────────────────────

    [McpServerTool, Description("Create a new sketch on the specified work plane.")]
    public Dictionary<string, object?> sketch_create(string plane = "XY")
        => Catch(() => provider.SketchCreate(plane));

    [McpServerTool, Description("Draw a line segment. Use connect=True to chain lines into a profile.")]
    public Dictionary<string, object?> sketch_line(
        double x1, double y1, double x2, double y2,
        string? tag = null, bool connect = false)
        => Catch(() => provider.SketchLine(x1, y1, x2, y2, tag, connect));

    [McpServerTool, Description("Draw a circle. Use tag=\"name\" to tag for @name resolution.")]
    public Dictionary<string, object?> sketch_circle(double cx, double cy, double radius, string? tag = null)
        => Catch(() => provider.SketchCircle(cx, cy, radius, tag));

    [McpServerTool, Description("Draw an arc in the active sketch.")]
    public Dictionary<string, object?> sketch_arc(
        double cx, double cy, double radius, double start_angle, double end_angle)
        => Catch(() => provider.SketchArc(cx, cy, radius, start_angle, end_angle));

    [McpServerTool, Description("Draw a rectangle in the active sketch.")]
    public Dictionary<string, object?> sketch_rectangle(double x1, double y1, double x2, double y2)
        => Catch(() => provider.SketchRectangle(x1, y1, x2, y2));

    [McpServerTool, Description("Add a dimension constraint. Modes: linear, radius, diameter, angle.")]
    public Dictionary<string, object?> sketch_dimension(
        string mode, string entity1, string entity2 = "",
        double? value = null, string orientation = "aligned",
        double? position_x = null, double? position_y = null)
        => Catch(() => provider.SketchDimension(mode, entity1, entity2, value, orientation, position_x, position_y));

    [McpServerTool, Description("Draw a point in the active sketch.")]
    public Dictionary<string, object?> sketch_point(double x, double y)
        => Catch(() => provider.SketchPoint(x, y));

    [McpServerTool, Description("Draw a spline through fit points.")]
    public Dictionary<string, object?> sketch_spline(string points, string fit_method = "sweet")
        => Catch(() => provider.SketchSpline(points, fit_method));

    [McpServerTool, Description("Draw an ellipse in the active sketch.")]
    public Dictionary<string, object?> sketch_ellipse(
        double cx, double cy, double major_radius, double minor_radius, double major_axis_angle = 0.0)
        => Catch(() => provider.SketchEllipse(cx, cy, major_radius, minor_radius, major_axis_angle));

    [McpServerTool, Description("Create a circular pattern of sketch entities.")]
    public Dictionary<string, object?> sketch_circular_pattern(
        string entities, string axis, int count,
        double angle = 360.0, bool fitted = true, bool symmetric = false)
        => Catch(() => provider.SketchCircularPattern(entities, axis, count, angle, fitted, symmetric));

    [McpServerTool, Description("Create a rectangular pattern of sketch entities.")]
    public Dictionary<string, object?> sketch_rectangular_pattern(
        string entities, string x_axis, int x_count, double x_spacing,
        string y_axis = "", int y_count = 1, double y_spacing = 0.0)
        => Catch(() => provider.SketchRectangularPattern(entities, x_axis, x_count, x_spacing, y_axis, y_count, y_spacing));

    [McpServerTool, Description("Offset sketch entities through a point (cm).")]
    public Dictionary<string, object?> sketch_offset(
        string entities, double offset_x, double offset_y, bool include_connected = false)
        => Catch(() => provider.SketchOffset(entities, offset_x, offset_y, include_connected));

    [McpServerTool, Description("Move sketch entities by a vector (cm).")]
    public Dictionary<string, object?> sketch_move(string entities, double dx, double dy, bool copy = false)
        => Catch(() => provider.SketchMove(entities, dx, dy, copy));

    [McpServerTool, Description("Rotate sketch entities around a center point (degrees).")]
    public Dictionary<string, object?> sketch_rotate(string entities, double cx, double cy, double angle, bool copy = false)
        => Catch(() => provider.SketchRotate(entities, cx, cy, angle, copy));

    [McpServerTool, Description("Delete the active sketch (must not be used by a feature).")]
    public Dictionary<string, object?> sketch_delete()
        => Catch(provider.SketchDelete);

    [McpServerTool, Description("Add a geometric constraint between sketch entities.")]
    public Dictionary<string, object?> sketch_constraint(
        string mode, string entity1, string entity2 = "",
        string sym_line = "", string axis = "major")
        => Catch(() => provider.SketchConstraint(mode, entity1, entity2, sym_line, axis));

    [McpServerTool, Description("Trim a sketch entity to its intersection with another.")]
    public Dictionary<string, object?> sketch_trim(string entity, string cutting_entity, string side = "end")
        => Catch(() => provider.SketchTrim(entity, cutting_entity, side));

    [McpServerTool, Description("Scale sketch entities around a center point.")]
    public Dictionary<string, object?> sketch_scale(string entities, double cx, double cy, double factor)
        => Catch(() => provider.SketchScale(entities, cx, cy, factor));

    [McpServerTool, Description("Mirror sketch entities across a mirror line.")]
    public Dictionary<string, object?> sketch_mirror(string entities, string mirror_entity)
        => Catch(() => provider.SketchMirror(entities, mirror_entity));

    // ── Features (5) ──────────────────────────────────────────────────

    [McpServerTool, Description("Extrude a sketch profile to create a 3D feature.")]
    public Dictionary<string, object?> extrude(
        string profile, double distance, string direction = "positive",
        double taper = 0.0, string operation = "new_body")
        => Catch(() => provider.Extrude(profile, distance, direction, taper, operation));

    [McpServerTool, Description("Revolve a profile around an axis (360° torus or partial sweep).")]
    public Dictionary<string, object?> revolve(
        string profile, string axis, double angle = 360.0,
        string direction = "positive", string operation = "join")
        => Catch(() => provider.Revolve(profile, axis, angle, direction, operation));

    [McpServerTool, Description("Apply a fillet to the specified edges.")]
    public Dictionary<string, object?> fillet(string edges, double radius, string mode = "constant")
        => Catch(() => provider.Fillet(edges, radius, mode));

    [McpServerTool, Description("Apply a chamfer to the specified edges.")]
    public Dictionary<string, object?> chamfer(string edges, double distance, string mode = "equal_distance")
        => Catch(() => provider.Chamfer(edges, distance, mode));

    [McpServerTool, Description("Create a circular pattern of a feature around an axis.")]
    public Dictionary<string, object?> circular_pattern(
        string profile, string axis, int count,
        double angle = 360.0, bool fit_within_angle = true, bool natural_direction = true)
        => Catch(() => provider.CircularPattern(profile, axis, count, angle, fit_within_angle, natural_direction));

    [McpServerTool, Description("Create a hole feature at the specified position.")]
    public Dictionary<string, object?> hole(
        double x, double y, double diameter, double depth,
        string type = "drilled", string operation = "join")
        => Catch(() => provider.Hole(x, y, diameter, depth, type, operation));

    [McpServerTool, Description("Create a thread feature on a cylindrical face.")]
    public Dictionary<string, object?> thread(string face, string specification, string direction = "right")
        => Catch(() => provider.Thread(face, specification, direction));

    [McpServerTool, Description("List all edges of the active body with geometry info for selection.")]
    public Dictionary<string, object?> inspect_edges()
        => Catch(provider.InspectEdges);

    // ── Parameters (4) ────────────────────────────────────────────────

    [McpServerTool, Description("List model parameters, optionally filtered by name pattern.")]
    public Dictionary<string, object?> param_list(string? filter_pattern = null)
        => Catch(() => provider.ParamList(filter_pattern));

    [McpServerTool, Description("Get a specific model parameter by name.")]
    public Dictionary<string, object?> param_get(string name)
        => Catch(() => provider.ParamGet(name));

    [McpServerTool, Description("Set a model parameter value by name.")]
    public Dictionary<string, object?> param_set(string name, double value)
        => Catch(() => provider.ParamSet(name, value));

    [McpServerTool, Description("Set a model parameter using an expression (e.g. 'd0 * 2').")]
    public Dictionary<string, object?> param_set_expression(string name, string expression)
        => Catch(() => provider.ParamSetExpression(name, expression));

    // ── Properties (5) ────────────────────────────────────────────────

    [McpServerTool, Description("Get an iProperty value by name.")]
    public Dictionary<string, object?> iproperty_get(string name, string property_set = "Summary")
        => Catch(() => provider.IPropertyGet(name, property_set));

    [McpServerTool, Description("Set an iProperty value by name.")]
    public Dictionary<string, object?> iproperty_set(string name, object value, string property_set = "Summary")
        => Catch(() => provider.IPropertySet(name, value, property_set));

    [McpServerTool, Description("Get all Summary iProperties.")]
    public Dictionary<string, object?> iproperty_summary()
        => Catch(provider.IPropertySummary);

    [McpServerTool, Description("Get a custom iProperty by name.")]
    public Dictionary<string, object?> iproperty_custom_get(string name)
        => Catch(() => provider.IPropertyCustomGet(name));

    [McpServerTool, Description("Set a custom iProperty. Creates it if it doesn't exist.")]
    public Dictionary<string, object?> iproperty_custom_set(string name, object value)
        => Catch(() => provider.IPropertyCustomSet(name, value));

    // ── Export (4) ─────────────────────────────────────────────────────

    [McpServerTool, Description("Export the active document to STEP format.")]
    public Dictionary<string, object?> export_step(string path, Dictionary<string, object?>? options = null)
        => Catch(() => provider.ExportStep(path, options));

    [McpServerTool, Description("Export the active document to STL format.")]
    public Dictionary<string, object?> export_stl(string path, Dictionary<string, object?>? options = null)
        => Catch(() => provider.ExportStl(path, options));

    [McpServerTool, Description("Export the active document to PDF format.")]
    public Dictionary<string, object?> export_pdf(string path, Dictionary<string, object?>? options = null)
        => Catch(() => provider.ExportPdf(path, options));

    [McpServerTool, Description("Export the active document's sketch or flat pattern to DXF.")]
    public Dictionary<string, object?> export_dxf(string path, Dictionary<string, object?>? options = null)
        => Catch(() => provider.ExportDxf(path, options));

    // ── Error catching helper (D7: tool-layer catches COM exceptions) ─

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