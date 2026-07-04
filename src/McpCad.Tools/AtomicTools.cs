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
public class AtomicTools(IProviderRouter router)
{
    // ── Connection (3) ────────────────────────────────────────────────

    [McpServerTool, Description("Connect to the active CAD provider.")]
    public Dictionary<string, object?> connect()
        => RouterToolDispatch.CatchProvider(router, p => p.Connect());

    [McpServerTool, Description("Disconnect from the active CAD provider.")]
    public Dictionary<string, object?> disconnect()
        => RouterToolDispatch.CatchProvider(router, p => p.Disconnect());

    [McpServerTool, Description("Check connection health and document state for the active provider.")]
    public Dictionary<string, object?> health()
        => RouterToolDispatch.CatchProvider(router, p => p.Health());

    // ── Documents (6) ────────────────────────────────────────────────

    [McpServerTool, Description("Open an existing Inventor document.")]
    public Dictionary<string, object?> doc_open(string path)
        => RouterToolDispatch.CatchMechanical(router, "doc_open", mech => mech.DocOpen(path));

    [McpServerTool, Description("Create a new part document.")]
    public Dictionary<string, object?> doc_new_part(string template = "")
        => RouterToolDispatch.CatchMechanical(router, "doc_new_part", mech => mech.DocNewPart(template));

    [McpServerTool, Description("Create a new assembly document.")]
    public Dictionary<string, object?> doc_new_assembly(string template = "")
        => RouterToolDispatch.CatchMechanical(router, "doc_new_assembly", mech => mech.DocNewAssembly(template));

    [McpServerTool, Description("Save the active document.")]
    public Dictionary<string, object?> doc_save()
        => RouterToolDispatch.CatchMechanical(router, "doc_save", mech => mech.DocSave());

    [McpServerTool, Description("Save the active document to a new path.")]
    public Dictionary<string, object?> doc_save_as(string path)
        => RouterToolDispatch.CatchMechanical(router, "doc_save_as", mech => mech.DocSaveAs(path));

    [McpServerTool, Description("Close the active document.")]
    public Dictionary<string, object?> doc_close(bool save = true)
        => RouterToolDispatch.CatchMechanical(router, "doc_close", mech => mech.DocClose(save));

    // ── Sketch (18) ────────────────────────────────────────────────────

    [McpServerTool, Description("Create a new sketch on the specified work plane.")]
    public Dictionary<string, object?> sketch_create(string plane = "XY")
        => RouterToolDispatch.CatchMechanical(router, "sketch_create", mech => mech.SketchCreate(plane));

    [McpServerTool, Description("Draw a line segment. Use connect=True to chain lines into a profile.")]
    public Dictionary<string, object?> sketch_line(
        double x1, double y1, double x2, double y2,
        string? tag = null, bool connect = false)
        => RouterToolDispatch.CatchMechanical(router, "sketch_line", mech => mech.SketchLine(x1, y1, x2, y2, tag, connect));

    [McpServerTool, Description("Draw a circle. Use tag=\"name\" to tag for @name resolution.")]
    public Dictionary<string, object?> sketch_circle(double cx, double cy, double radius, string? tag = null)
        => RouterToolDispatch.CatchMechanical(router, "sketch_circle", mech => mech.SketchCircle(cx, cy, radius, tag));

    [McpServerTool, Description("Draw an arc in the active sketch.")]
    public Dictionary<string, object?> sketch_arc(
        double cx, double cy, double radius, double start_angle, double end_angle)
        => RouterToolDispatch.CatchMechanical(router, "sketch_arc", mech => mech.SketchArc(cx, cy, radius, start_angle, end_angle));

    [McpServerTool, Description("Draw a rectangle in the active sketch.")]
    public Dictionary<string, object?> sketch_rectangle(double x1, double y1, double x2, double y2)
        => RouterToolDispatch.CatchMechanical(router, "sketch_rectangle", mech => mech.SketchRectangle(x1, y1, x2, y2));

    [McpServerTool, Description("Add a dimension constraint. Modes: linear, radius, diameter, angle.")]
    public Dictionary<string, object?> sketch_dimension(
        string mode, string entity1, string entity2 = "",
        double? value = null, string orientation = "aligned",
        double? position_x = null, double? position_y = null)
        => RouterToolDispatch.CatchMechanical(router, "sketch_dimension", mech => mech.SketchDimension(mode, entity1, entity2, value, orientation, position_x, position_y));

    [McpServerTool, Description("Draw a point in the active sketch.")]
    public Dictionary<string, object?> sketch_point(double x, double y)
        => RouterToolDispatch.CatchMechanical(router, "sketch_point", mech => mech.SketchPoint(x, y));

    [McpServerTool, Description("Draw a spline through fit points.")]
    public Dictionary<string, object?> sketch_spline(string points, string fit_method = "sweet")
        => RouterToolDispatch.CatchMechanical(router, "sketch_spline", mech => mech.SketchSpline(points, fit_method));

    [McpServerTool, Description("Draw an ellipse in the active sketch.")]
    public Dictionary<string, object?> sketch_ellipse(
        double cx, double cy, double major_radius, double minor_radius, double major_axis_angle = 0.0)
        => RouterToolDispatch.CatchMechanical(router, "sketch_ellipse", mech => mech.SketchEllipse(cx, cy, major_radius, minor_radius, major_axis_angle));

    [McpServerTool, Description("Create a circular pattern of sketch entities.")]
    public Dictionary<string, object?> sketch_circular_pattern(
        string entities, string axis, int count,
        double angle = 360.0, bool fitted = true, bool symmetric = false)
        => RouterToolDispatch.CatchMechanical(router, "sketch_circular_pattern", mech => mech.SketchCircularPattern(entities, axis, count, angle, fitted, symmetric));

    [McpServerTool, Description("Create a rectangular pattern of sketch entities.")]
    public Dictionary<string, object?> sketch_rectangular_pattern(
        string entities, string x_axis, int x_count, double x_spacing,
        string y_axis = "", int y_count = 1, double y_spacing = 0.0)
        => RouterToolDispatch.CatchMechanical(router, "sketch_rectangular_pattern", mech => mech.SketchRectangularPattern(entities, x_axis, x_count, x_spacing, y_axis, y_count, y_spacing));

    [McpServerTool, Description("Offset sketch entities through a point (cm).")]
    public Dictionary<string, object?> sketch_offset(
        string entities, double offset_x, double offset_y, bool include_connected = false)
        => RouterToolDispatch.CatchMechanical(router, "sketch_offset", mech => mech.SketchOffset(entities, offset_x, offset_y, include_connected));

    [McpServerTool, Description("Move sketch entities by a vector (cm).")]
    public Dictionary<string, object?> sketch_move(string entities, double dx, double dy, bool copy = false)
        => RouterToolDispatch.CatchMechanical(router, "sketch_move", mech => mech.SketchMove(entities, dx, dy, copy));

    [McpServerTool, Description("Rotate sketch entities around a center point (degrees).")]
    public Dictionary<string, object?> sketch_rotate(string entities, double cx, double cy, double angle, bool copy = false)
        => RouterToolDispatch.CatchMechanical(router, "sketch_rotate", mech => mech.SketchRotate(entities, cx, cy, angle, copy));

    [McpServerTool, Description("Delete the active sketch (must not be used by a feature).")]
    public Dictionary<string, object?> sketch_delete()
        => RouterToolDispatch.CatchMechanical(router, "sketch_delete", mech => mech.SketchDelete());

    [McpServerTool, Description("Add a geometric constraint between sketch entities.")]
    public Dictionary<string, object?> sketch_constraint(
        string mode, string entity1, string entity2 = "",
        string sym_line = "", string axis = "major")
        => RouterToolDispatch.CatchMechanical(router, "sketch_constraint", mech => mech.SketchConstraint(mode, entity1, entity2, sym_line, axis));

    [McpServerTool, Description("Trim a sketch entity to its intersection with another.")]
    public Dictionary<string, object?> sketch_trim(string entity, string cutting_entity, string side = "end")
        => RouterToolDispatch.CatchMechanical(router, "sketch_trim", mech => mech.SketchTrim(entity, cutting_entity, side));

    [McpServerTool, Description("Scale sketch entities around a center point.")]
    public Dictionary<string, object?> sketch_scale(string entities, double cx, double cy, double factor)
        => RouterToolDispatch.CatchMechanical(router, "sketch_scale", mech => mech.SketchScale(entities, cx, cy, factor));

    [McpServerTool, Description("Mirror sketch entities across a mirror line.")]
    public Dictionary<string, object?> sketch_mirror(string entities, string mirror_entity)
        => RouterToolDispatch.CatchMechanical(router, "sketch_mirror", mech => mech.SketchMirror(entities, mirror_entity));

    [McpServerTool, Description("List all closed profiles in the active sketch with area info. Use before extrude/revolve to pick the correct profile index.")]
    public Dictionary<string, object?> sketch_profiles()
        => RouterToolDispatch.CatchMechanical(router, "sketch_profiles", mech => mech.SketchProfiles());

    // ── Features (5) ──────────────────────────────────────────────────

    [McpServerTool, Description("Extrude a sketch profile to create a 3D feature. Use profile: \"1\" for single region, or \"2,4\" for multiple regions (comma-separated indices). Run sketch_profiles() first to see available profiles and their centroids.")]
    public Dictionary<string, object?> extrude(
        string profile, double distance, string direction = "positive",
        double taper = 0.0, string operation = "new_body")
        => RouterToolDispatch.CatchMechanical(router, "extrude", mech => mech.Extrude(profile, distance, direction, taper, operation));

    [McpServerTool, Description("Revolve a profile around an axis (360° torus or partial sweep).")]
    public Dictionary<string, object?> revolve(
        string profile, string axis, double angle = 360.0,
        string direction = "positive", string operation = "join")
        => RouterToolDispatch.CatchMechanical(router, "revolve", mech => mech.Revolve(profile, axis, angle, direction, operation));

    [McpServerTool, Description("Sweep a profile along a path of connected sketch entities. Create profile sketch first, then path sketch. Use profile_sketch=\"last\" when profile is in the previous sketch.")]
    public Dictionary<string, object?> sweep(
        string profile, string path,
        string sweep_type = "path", string operation = "new_body",
        double taper = 0, string path_sketch = "", string profile_sketch = "")
        => RouterToolDispatch.CatchMechanical(router, "sweep", mech => mech.Sweep(profile, path, sweep_type, operation, taper, path_sketch, profile_sketch));

    [McpServerTool, Description("Apply a fillet to the specified edges.")]
    public Dictionary<string, object?> fillet(string edges, double radius, string mode = "constant")
        => RouterToolDispatch.CatchMechanical(router, "fillet", mech => mech.Fillet(edges, radius, mode));

    [McpServerTool, Description("Apply a chamfer to the specified edges.")]
    public Dictionary<string, object?> chamfer(string edges, double distance, string mode = "equal_distance")
        => RouterToolDispatch.CatchMechanical(router, "chamfer", mech => mech.Chamfer(edges, distance, mode));

    [McpServerTool, Description("Create a circular pattern of a feature around an axis.")]
    public Dictionary<string, object?> circular_pattern(
        string profile, string axis, int count,
        double angle = 360.0, bool fit_within_angle = true, bool natural_direction = true)
        => RouterToolDispatch.CatchMechanical(router, "circular_pattern", mech => mech.CircularPattern(profile, axis, count, angle, fit_within_angle, natural_direction));

    [McpServerTool, Description("Mirror a feature across a work plane.")]
    public Dictionary<string, object?> mirror_feature(string profile, string mirror_plane)
        => RouterToolDispatch.CatchMechanical(router, "mirror_feature", mech => mech.MirrorFeature(profile, mirror_plane));

    [McpServerTool, Description("Create a rectangular pattern of a feature along one or two axes.")]
    public Dictionary<string, object?> rectangular_pattern(
        string profile, string x_axis, int x_count, double x_spacing,
        string y_axis = "", int y_count = 1, double y_spacing = 0.0)
        => RouterToolDispatch.CatchMechanical(router, "rectangular_pattern", mech => mech.RectangularPattern(profile, x_axis, x_count, x_spacing, y_axis, y_count, y_spacing));

    [McpServerTool, Description("Create a loft feature between two or more profiles.")]
    public Dictionary<string, object?> loft(string profiles, string operation = "new_body")
        => RouterToolDispatch.CatchMechanical(router, "loft", mech => mech.Loft(profiles, operation));

    [McpServerTool, Description("Create a coil (spring) feature.")]
    public Dictionary<string, object?> coil(string profile, string axis, double pitch, double revolutions, string operation = "new_body")
        => RouterToolDispatch.CatchMechanical(router, "coil", mech => mech.Coil(profile, axis, pitch, revolutions, operation));

    [McpServerTool, Description("Create a rib (reinforcement) feature.")]
    public Dictionary<string, object?> rib(string profile, double thickness, string direction = "normal", string operation = "new_body")
        => RouterToolDispatch.CatchMechanical(router, "rib", mech => mech.Rib(profile, thickness, direction, operation));

    [McpServerTool, Description("Create an emboss feature from a profile.")]
    public Dictionary<string, object?> emboss(string profile, double depth, string type = "emboss_from_face")
        => RouterToolDispatch.CatchMechanical(router, "emboss", mech => mech.Emboss(profile, depth, type));

    [McpServerTool, Description("Derive a part from an external file.")]
    public Dictionary<string, object?> derive(string source_path)
        => RouterToolDispatch.CatchMechanical(router, "derive", mech => mech.Derive(source_path));

    [McpServerTool, Description("Create a hole feature at the specified position.")]
    public Dictionary<string, object?> hole(
        double x, double y, double diameter, double depth,
        string type = "drilled", string operation = "join")
        => RouterToolDispatch.CatchMechanical(router, "hole", mech => mech.Hole(x, y, diameter, depth, type, operation));

    [McpServerTool, Description("Create a thread feature on a cylindrical face.")]
    public Dictionary<string, object?> thread(string face, string specification, string direction = "right")
        => RouterToolDispatch.CatchMechanical(router, "thread", mech => mech.Thread(face, specification, direction));

    [McpServerTool, Description("List all edges of the active body with geometry info for selection.")]
    public Dictionary<string, object?> inspect_edges()
        => RouterToolDispatch.CatchMechanical(router, "inspect_edges", mech => mech.InspectEdges());

    // ── Modify Features (5) ────────────────────────────────────────────

    [McpServerTool, Description("Create a shell feature by removing selected faces and applying uniform thickness to the remaining faces.")]
    public Dictionary<string, object?> shell(
        string faces, double thickness, string direction = "inside", string operation = "new_body")
        => RouterToolDispatch.CatchMechanical(router, "shell", mech => mech.Shell(faces, thickness, direction, operation));

    [McpServerTool, Description("Create a solid feature by offsetting selected faces by a thickness value. Requires a surface body as input.")]
    public Dictionary<string, object?> thicken(
        string faces, double thickness, string direction = "positive", string operation = "new_body")
        => RouterToolDispatch.CatchMechanical(router, "thicken", mech => mech.Thicken(faces, thickness, direction, operation));

    [McpServerTool, Description("Combine bodies via boolean operation (join, cut, intersect). Base body and tool bodies are 1-based body indices (tool_bodies may be comma-separated).")]
    public Dictionary<string, object?> combine(
        string base_body, string tool_bodies, string operation = "join", bool keep_tool_bodies = false)
        => RouterToolDispatch.CatchMechanical(router, "combine", mech => mech.Combine(base_body, tool_bodies, operation, keep_tool_bodies));

    [McpServerTool, Description("Split a body using a work plane as the splitting tool.")]
    public Dictionary<string, object?> split(
        string split_tool, string remove_side = "positive", string target_body = "")
        => RouterToolDispatch.CatchMechanical(router, "split", mech => mech.Split(split_tool, remove_side, target_body));

    [McpServerTool, Description("Apply a draft angle to the specified faces. Pull direction: x, y, or z. Fixed entity: eN for a specific edge, or empty to draft all edges of the selected faces.")]
    public Dictionary<string, object?> draft(
        string faces, double angle, string mode = "fixed_edge",
        string pull_direction = "z", string fixed_entity = "")
        => RouterToolDispatch.CatchMechanical(router, "draft", mech => mech.Draft(faces, angle, mode, pull_direction, fixed_entity));

    // ── Parameters (4) ────────────────────────────────────────────────

    [McpServerTool, Description("List model parameters, optionally filtered by name pattern.")]
    public Dictionary<string, object?> param_list(string? filter_pattern = null)
        => RouterToolDispatch.CatchMechanical(router, "param_list", mech => mech.ParamList(filter_pattern));

    [McpServerTool, Description("Get a specific model parameter by name.")]
    public Dictionary<string, object?> param_get(string name)
        => RouterToolDispatch.CatchMechanical(router, "param_get", mech => mech.ParamGet(name));

    [McpServerTool, Description("Set a model parameter value by name.")]
    public Dictionary<string, object?> param_set(string name, double value)
        => RouterToolDispatch.CatchMechanical(router, "param_set", mech => mech.ParamSet(name, value));

    [McpServerTool, Description("Set a model parameter using an expression (e.g. 'd0 * 2').")]
    public Dictionary<string, object?> param_set_expression(string name, string expression)
        => RouterToolDispatch.CatchMechanical(router, "param_set_expression", mech => mech.ParamSetExpression(name, expression));

    // ── Properties (5) ────────────────────────────────────────────────

    [McpServerTool, Description("Get an iProperty value by name.")]
    public Dictionary<string, object?> iproperty_get(string name, string property_set = "Summary")
        => RouterToolDispatch.CatchMechanical(router, "iproperty_get", mech => mech.IPropertyGet(name, property_set));

    [McpServerTool, Description("Set an iProperty value by name.")]
    public Dictionary<string, object?> iproperty_set(string name, string? value, string property_set = "Summary")
        => RouterToolDispatch.CatchMechanical(router, "iproperty_set", mech => mech.IPropertySet(name, value, property_set));

    [McpServerTool, Description("Get all Summary iProperties.")]
    public Dictionary<string, object?> iproperty_summary()
        => RouterToolDispatch.CatchMechanical(router, "iproperty_summary", mech => mech.IPropertySummary());

    [McpServerTool, Description("Get a custom iProperty by name.")]
    public Dictionary<string, object?> iproperty_custom_get(string name)
        => RouterToolDispatch.CatchMechanical(router, "iproperty_custom_get", mech => mech.IPropertyCustomGet(name));

    [McpServerTool, Description("Set a custom iProperty. Creates it if it doesn't exist.")]
    public Dictionary<string, object?> iproperty_custom_set(string name, string? value)
        => RouterToolDispatch.CatchMechanical(router, "iproperty_custom_set", mech => mech.IPropertyCustomSet(name, value));

    // ── Export (4) ─────────────────────────────────────────────────────

    [McpServerTool, Description("Export the active document to STEP format.")]
    public Dictionary<string, object?> export_step(string path, Dictionary<string, object?>? options = null)
        => RouterToolDispatch.CatchMechanical(router, "export_step", mech => mech.ExportStep(path, options));

    [McpServerTool, Description("Export the active document to STL format.")]
    public Dictionary<string, object?> export_stl(string path, Dictionary<string, object?>? options = null)
        => RouterToolDispatch.CatchMechanical(router, "export_stl", mech => mech.ExportStl(path, options));

    [McpServerTool, Description("Export the active document to PDF format.")]
    public Dictionary<string, object?> export_pdf(string path, Dictionary<string, object?>? options = null)
        => RouterToolDispatch.CatchMechanical(router, "export_pdf", mech => mech.ExportPdf(path, options));

    [McpServerTool, Description("Export the active document's sketch or flat pattern to DXF.")]
    public Dictionary<string, object?> export_dxf(string path, Dictionary<string, object?>? options = null)
        => RouterToolDispatch.CatchMechanical(router, "export_dxf", mech => mech.ExportDxf(path, options));

    // ── Work Features (3) ──────────────────────────────────────────────

    [McpServerTool, Description("Create a work plane in the active part document. Supported definitions: default(1-3), offset_from_plane, through_three_points, normal_to_curve.")]
    public Dictionary<string, object?> work_plane(
        string definition, string reference1 = "", string reference2 = "", double? offset = null)
        => RouterToolDispatch.CatchMechanical(router, "work_plane", mech => mech.WorkPlane(definition, reference1, reference2, offset ?? 0.0));

    [McpServerTool, Description("Create a work axis in the active part document. Supported definitions: default(1-3), through_two_points, normal_to_plane, along_edge.")]
    public Dictionary<string, object?> work_axis(
        string definition, string reference1 = "", string reference2 = "")
        => RouterToolDispatch.CatchMechanical(router, "work_axis", mech => mech.WorkAxis(definition, reference1, reference2));

    [McpServerTool, Description("Create a work point in the active part document. Supported definitions: default, at_coordinates, on_curve, intersection.")]
    public Dictionary<string, object?> work_point(
        string definition, string reference1 = "", string reference2 = "", string reference3 = "")
        => RouterToolDispatch.CatchMechanical(router, "work_point", mech => mech.WorkPoint(definition, reference1, reference2, reference3));

    // ── Assembly (16) ──────────────────────────────────────────────────

    [McpServerTool, Description("List all placed components (occurrences) in the active assembly with their names, grounded state, and source paths.")]
    public Dictionary<string, object?> asm_list_components()
        => RouterToolDispatch.CatchMechanical(router, "asm_list_components", mech => mech.AsmListComponents());

    [McpServerTool, Description("List all assembly constraints with their type, offset, and suppressed state.")]
    public Dictionary<string, object?> asm_list_constraints()
        => RouterToolDispatch.CatchMechanical(router, "asm_list_constraints", mech => mech.AsmListConstraints());

    [McpServerTool, Description("Place a component into the assembly at an optional x,y,z position (cm). Path must be an absolute file path to an .ipt or .iam.")]
    public Dictionary<string, object?> asm_place_component(
        string path, double x = 0.0, double y = 0.0, double z = 0.0)
        => RouterToolDispatch.CatchMechanical(router, "asm_place_component", mech => mech.AsmPlaceComponent(path, x, y, z));

    [McpServerTool, Description("Ground (fix in place) a component by name or 1-based occurrence index.")]
    public Dictionary<string, object?> asm_ground_component(string occurrence)
        => RouterToolDispatch.CatchMechanical(router, "asm_ground_component", mech => mech.AsmGroundComponent(occurrence));

    [McpServerTool, Description("Replace an occurrence with a different part file, retaining compatible constraints.")]
    public Dictionary<string, object?> asm_replace_component(string occurrence, string new_path)
        => RouterToolDispatch.CatchMechanical(router, "asm_replace_component", mech => mech.AsmReplaceComponent(occurrence, new_path));

    [McpServerTool, Description("Delete an assembly constraint by name or 1-based index.")]
    public Dictionary<string, object?> asm_delete_constraint(string constraint)
        => RouterToolDispatch.CatchMechanical(router, "asm_delete_constraint", mech => mech.AsmDeleteConstraint(constraint));

    [McpServerTool, Description("Create a mate constraint between two entities. Entity format: 'OccName/N' for face, 'OccName/eN' for edge, '@PlaneName' for work plane.")]
    public Dictionary<string, object?> asm_constraint_mate(
        string entity_one, string entity_two, double offset = 0.0)
        => RouterToolDispatch.CatchMechanical(router, "asm_constraint_mate", mech => mech.AsmConstraintMate(entity_one, entity_two, offset));

    [McpServerTool, Description("Create a flush constraint between two planar entities. Entity format: 'OccName/N' for face, '@PlaneName' for work plane.")]
    public Dictionary<string, object?> asm_constraint_flush(
        string entity_one, string entity_two, double offset = 0.0)
        => RouterToolDispatch.CatchMechanical(router, "asm_constraint_flush", mech => mech.AsmConstraintFlush(entity_one, entity_two, offset));

    [McpServerTool, Description("Create an angle constraint between two entities. Solution: directed, undirected, reference_vector.")]
    public Dictionary<string, object?> asm_constraint_angle(
        string entity_one, string entity_two, double angle, string solution = "directed")
        => RouterToolDispatch.CatchMechanical(router, "asm_constraint_angle", mech => mech.AsmConstraintAngle(entity_one, entity_two, angle, solution));

    [McpServerTool, Description("Create an insert constraint (concentric + planar) between two circular entities.")]
    public Dictionary<string, object?> asm_constraint_insert(
        string entity_one, string entity_two, double offset = 0.0)
        => RouterToolDispatch.CatchMechanical(router, "asm_constraint_insert", mech => mech.AsmConstraintInsert(entity_one, entity_two, offset));

    [McpServerTool, Description("Create a tangent constraint between two entities.")]
    public Dictionary<string, object?> asm_constraint_tangent(
        string entity_one, string entity_two, double offset = 0.0)
        => RouterToolDispatch.CatchMechanical(router, "asm_constraint_tangent", mech => mech.AsmConstraintTangent(entity_one, entity_two, offset));

    [McpServerTool, Description("Create a circular pattern of an assembly occurrence around an axis (X, Y, Z, work axis index, or eN for edge).")]
    public Dictionary<string, object?> asm_pattern_circular(
        string occurrence, string axis, int count, double angle = 360.0)
        => RouterToolDispatch.CatchMechanical(router, "asm_circular_pattern", mech => mech.AsmCircularPattern(occurrence, axis, count, angle));

    [McpServerTool, Description("Create a rectangular pattern of an assembly occurrence along one or two axes.")]
    public Dictionary<string, object?> asm_pattern_rectangular(
        string occurrence, string x_axis, int x_count, double x_spacing,
        string? y_axis = null, int y_count = 1, double y_spacing = 0.0)
        => RouterToolDispatch.CatchMechanical(router, "asm_rectangular_pattern", mech => mech.AsmRectangularPattern(occurrence, x_axis, x_count, x_spacing, y_axis, y_count, y_spacing));

    [McpServerTool, Description("Create an extrude cut across multiple components at the assembly level. Requires an existing sketch.")]
    public Dictionary<string, object?> asm_extrude_cut(
        string profile, double distance, string direction = "positive")
        => RouterToolDispatch.CatchMechanical(router, "asm_extrude_cut", mech => mech.AsmExtrudeCut(profile, distance, direction));

    [McpServerTool, Description("Create a hole feature at the assembly level. Requires an existing sketch with a point.")]
    public Dictionary<string, object?> asm_hole(
        double x, double y, double diameter, double depth, string type = "drilled")
        => RouterToolDispatch.CatchMechanical(router, "asm_hole", mech => mech.AsmHole(x, y, diameter, depth, type));

    [McpServerTool, Description("Get the bill of materials (BOM) for the active assembly with part numbers, quantities, and descriptions.")]
    public Dictionary<string, object?> asm_bom()
        => RouterToolDispatch.CatchMechanical(router, "asm_bom", mech => mech.AsmBom());

}