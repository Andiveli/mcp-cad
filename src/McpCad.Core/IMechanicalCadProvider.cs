namespace McpCad.Core;

/// <summary>
/// Provider interface for mechanical CAD operations (sketch, 3D features,
/// parameters, properties). Extends the common <see cref="ICadProvider"/>.
/// Implement for Inventor, SolidWorks, FreeCAD, etc.
/// </summary>
public interface IMechanicalCadProvider : ICadProvider
{
    #region Sketch

    Dictionary<string, object?> SketchCreate(string plane = "XY");
    Dictionary<string, object?> SketchLine(double x1, double y1, double x2, double y2, string? tag = null, bool connect = false);
    Dictionary<string, object?> SketchCircle(double cx, double cy, double radius, string? tag = null);
    Dictionary<string, object?> SketchArc(double cx, double cy, double radius, double startAngle, double endAngle);
    Dictionary<string, object?> SketchRectangle(double x1, double y1, double x2, double y2);
    Dictionary<string, object?> SketchDimension(string mode, string entity1, string entity2 = "", double? value = null, string orientation = "aligned", double? positionX = null, double? positionY = null);
    Dictionary<string, object?> SketchPoint(double x, double y);
    Dictionary<string, object?> SketchSpline(string points, string fitMethod = "sweet");
    Dictionary<string, object?> SketchEllipse(double cx, double cy, double majorRadius, double minorRadius, double majorAxisAngle = 0.0);
    Dictionary<string, object?> SketchCircularPattern(string entities, string axis, int count, double angle = 360.0, bool fitted = true, bool symmetric = false);
    Dictionary<string, object?> SketchRectangularPattern(string entities, string xAxis, int xCount, double xSpacing, string yAxis = "", int yCount = 1, double ySpacing = 0.0);
    Dictionary<string, object?> SketchOffset(string entities, double offsetX, double offsetY, bool includeConnected = false);
    Dictionary<string, object?> SketchMove(string entities, double dx, double dy, bool copy = false);
    Dictionary<string, object?> SketchRotate(string entities, double cx, double cy, double angle, bool copy = false);
    Dictionary<string, object?> SketchDelete();
    Dictionary<string, object?> SketchConstraint(string mode, string entity1, string entity2 = "", string symLine = "", string axis = "major");
    Dictionary<string, object?> SketchTrim(string entity, string cuttingEntity, string side = "end");
    Dictionary<string, object?> SketchScale(string entities, double cx, double cy, double factor);
    Dictionary<string, object?> SketchMirror(string entities, string mirrorEntity);
    Dictionary<string, object?> SketchLineClose();
    Dictionary<string, object?> SketchProfiles();

    /// <summary>
    /// Reads existing sketch geometry (entities) by 1-based sketch index into
    /// structured data (entities + warnings + parameters) compatible with
    /// macro_god_part sketch_* phases and the template capture system.
    /// Implementation uses SketchReader (COM traversal in Inventor layer).
    /// </summary>
    Dictionary<string, object?> ReadSketchData(int sketchIndex = 1);

    /// <summary>
    /// Reads the feature tree (all PartFeature objects) in creational order into
    /// typed descriptors for template capture and multi-feature replay.
    /// Returns envelope: { "success": bool, "features": FeatureDescriptor[], "warnings": string[] }
    /// Implementation uses FeatureReader (COM traversal in Inventor layer).
    /// Mirrors ReadSketchData pattern exactly.
    /// </summary>
    Dictionary<string, object?> ReadFeatureData();

    #endregion

    #region Features

    Dictionary<string, object?> Extrude(string profile, double distance, string direction = "positive", double taper = 0.0, string operation = "new_body");
    Dictionary<string, object?> Revolve(string profile, string axis, double angle = 360.0, string direction = "positive", string operation = "join");
    Dictionary<string, object?> Sweep(string profile, string path, string sweepType = "path", string operation = "new_body", double taper = 0, string pathSketch = "", string profileSketch = "");
    Dictionary<string, object?> Fillet(string edges, double radius, string mode = "constant");
    Dictionary<string, object?> Chamfer(string edges, double distance, string mode = "equal_distance");
    Dictionary<string, object?> CircularPattern(string profile, string axis, int count, double angle = 360.0, bool fitWithinAngle = true, bool naturalDirection = true);
    Dictionary<string, object?> MirrorFeature(string profile, string mirrorPlane);
    Dictionary<string, object?> RectangularPattern(string profile, string xAxis, int xCount, double xSpacing, string yAxis = "", int yCount = 1, double ySpacing = 0.0);
    Dictionary<string, object?> Loft(string profiles, string operation = "new_body");
    Dictionary<string, object?> Coil(string profile, string axis, double pitch, double revolutions, string operation = "new_body");
    Dictionary<string, object?> Rib(string profile, double thickness, string direction = "normal", string operation = "new_body");
    Dictionary<string, object?> Emboss(string profile, double depth, string type = "emboss_from_face");
    Dictionary<string, object?> Derive(string sourcePath);
    Dictionary<string, object?> Hole(double x, double y, double diameter, double depth, string type = "drilled", string operation = "join");
    Dictionary<string, object?> Thread(string face, string specification, string direction = "right");
    Dictionary<string, object?> InspectEdges();
    Dictionary<string, object?> Shell(string faces, double thickness, string direction = "inside", string operation = "new_body");
    Dictionary<string, object?> Draft(string faces, double angle, string mode = "fixed_edge", string pullDirection = "z", string fixedEntity = "");
    Dictionary<string, object?> Split(string splitTool, string removeSide = "positive", string targetBody = "");
    Dictionary<string, object?> Combine(string baseBody, string toolBodies, string operation = "join", bool keepToolBodies = false);
    Dictionary<string, object?> Thicken(string faces, double thickness, string direction = "positive", string operation = "new_body");

    // Weld features (fillet/groove/cosmetic). String face refs support numeric indices or @name (resolved via FaceResolver).
    // Requires weldment-capable document (assembly converted to weldment or part with weld support); errors helpfully otherwise.
    Dictionary<string, object?> WeldFillet(
        string legFaces1,   // faces for first leg (e.g. "1,2" or "@leg_a"); typically one set per adjoining part
        string legFaces2,   // faces for second leg
        double legSize,     // leg length (equal legs for v1; cm)
        double? length = null,   // full length if null/omitted; limited bead length otherwise
        bool intermittent = false,
        double? pitch = null,    // spacing for intermittent welds
        double? gap = null,      // gap between intermittent segments
        string? name = null);    // optional feature name override

    Dictionary<string, object?> WeldGroove(
        string faces1,
        string faces2,
        double size,
        string grooveType = "square", // square, v, bevel, j, u, etc. (mapped internally)
        double? length = null);

    Dictionary<string, object?> WeldCosmetic(
        string faces,
        double size,
        double? length = null);

    /// <summary>
    /// Converts the active assembly (or part with weld support) to a weldment document.
    /// This enables WeldFillet / WeldGroove / WeldCosmetic features.
    /// Uses best-effort command execution; may require the document to be savable.
    /// </summary>
    Dictionary<string, object?> ConvertToWeldment();

    #endregion

    #region Parameters

    Dictionary<string, object?> ParamList(string? filterPattern = null);
    Dictionary<string, object?> ParamGet(string name);
    Dictionary<string, object?> ParamSet(string name, double value);
    Dictionary<string, object?> ParamSetExpression(string name, string expression);

    #endregion

    #region Properties

    Dictionary<string, object?> IPropertyGet(string name, string propertySet = "Summary");
    Dictionary<string, object?> IPropertySet(string name, string? value, string propertySet = "Summary");
    Dictionary<string, object?> IPropertySummary();
    Dictionary<string, object?> IPropertyCustomGet(string name);
    Dictionary<string, object?> IPropertyCustomSet(string name, string? value);

    #endregion

    #region WorkFeatures

    Dictionary<string, object?> WorkPlane(string definition, string reference1, string reference2, double offset);
    Dictionary<string, object?> WorkAxis(string definition, string reference1, string reference2);
    Dictionary<string, object?> WorkPoint(string definition, string reference1, string reference2, string reference3);

    #endregion

    #region Assembly

    Dictionary<string, object?> AsmListComponents();
    Dictionary<string, object?> AsmListConstraints();
    Dictionary<string, object?> AsmPlaceComponent(string path, double x = 0, double y = 0, double z = 0);
    Dictionary<string, object?> AsmGroundComponent(string occurrence);
    Dictionary<string, object?> AsmReplaceComponent(string occurrence, string newPath);
    Dictionary<string, object?> AsmDeleteConstraint(string constraint);
    Dictionary<string, object?> AsmConstraintMate(string entityOne, string entityTwo, double offset = 0);
    Dictionary<string, object?> AsmConstraintFlush(string entityOne, string entityTwo, double offset = 0);
    Dictionary<string, object?> AsmConstraintAngle(string entityOne, string entityTwo, double angle, string solution = "directed");
    Dictionary<string, object?> AsmConstraintInsert(string entityOne, string entityTwo, double offset = 0);
    Dictionary<string, object?> AsmConstraintTangent(string entityOne, string entityTwo, double offset = 0);
    Dictionary<string, object?> AsmCircularPattern(string occurrence, string axis, int count, double angle = 360);
    Dictionary<string, object?> AsmRectangularPattern(string occurrence, string xAxis, int xCount, double xSpacing, string? yAxis = null, int yCount = 1, double ySpacing = 0);
    Dictionary<string, object?> AsmExtrudeCut(string profile, double distance, string direction = "positive");
    Dictionary<string, object?> AsmHole(double x, double y, double diameter, double depth, string type = "drilled");
    Dictionary<string, object?> AsmBom();

    #endregion

    #region Inspection & Visual Feedback (for LLM self-verification / retroalimentación)

    /// <summary>
    /// Captures a screenshot of the active 3D viewport or CAD window.
    /// Returns Base64 encoded image for multimodal (vision) feedback to the LLM.
    /// Use after operations so the vision model can visually verify geometry, collisions, etc.
    /// </summary>
    Dictionary<string, object?> CaptureViewportImage(string view = "Iso", int width = 1024, int height = 768, string format = "png");

    /// <summary>
    /// Returns the full feature/operation tree of the active document.
    /// Allows the LLM to inspect the model structure (features, their order, dependencies)
    /// directly via data (no vision needed). Supports "Árbol de Operaciones" verification.
    /// </summary>
    Dictionary<string, object?> GetFeatureTree();

    /// <summary>
    /// Returns bounding box information for the whole model, a body, or a tagged entity.
    /// Useful for geometric verification (intersections, sizes) using direct vector data from Inventor API.
    /// </summary>
    Dictionary<string, object?> GetBoundingBox(string target = "");

    #endregion
}
