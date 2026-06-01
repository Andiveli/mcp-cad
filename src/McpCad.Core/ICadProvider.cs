namespace McpCad.Core;

/// <summary>
/// Provider interface for CAD operations.
/// Implementations connect to a specific CAD backend (e.g., Inventor via COM).
/// All methods return a dictionary with operation results or error information.
/// </summary>
public interface ICadProvider
{
    #region Connection

    /// <summary>Connect to the CAD application.</summary>
    Dictionary<string, object?> Connect();

    /// <summary>Release the connection to the CAD application.</summary>
    Dictionary<string, object?> Disconnect();

    /// <summary>Check connection health and document state.</summary>
    Dictionary<string, object?> Health();

    #endregion

    #region Documents

    /// <summary>Open an existing document.</summary>
    Dictionary<string, object?> DocOpen(string path);

    /// <summary>Create a new part document.</summary>
    Dictionary<string, object?> DocNewPart(string template = "");

    /// <summary>Create a new assembly document.</summary>
    Dictionary<string, object?> DocNewAssembly(string template = "");

    /// <summary>Save the active document.</summary>
    Dictionary<string, object?> DocSave();

    /// <summary>Save the active document to a new path.</summary>
    Dictionary<string, object?> DocSaveAs(string path);

    /// <summary>Close the active document.</summary>
    Dictionary<string, object?> DocClose(bool save = true);

    #endregion

    #region Sketch

    /// <summary>Create a new sketch on the specified work plane.</summary>
    Dictionary<string, object?> SketchCreate(string plane = "XY");

    /// <summary>Draw a line segment in the active sketch.</summary>
    Dictionary<string, object?> SketchLine(
        double x1, double y1, double x2, double y2,
        string? tag = null, bool connect = false);

    /// <summary>Draw a circle in the active sketch.</summary>
    Dictionary<string, object?> SketchCircle(
        double cx, double cy, double radius, string? tag = null);

    /// <summary>Draw an arc in the active sketch.</summary>
    Dictionary<string, object?> SketchArc(
        double cx, double cy, double radius,
        double startAngle, double endAngle);

    /// <summary>Draw a rectangle in the active sketch.</summary>
    Dictionary<string, object?> SketchRectangle(
        double x1, double y1, double x2, double y2);

    /// <summary>Add a dimension constraint to the active sketch.</summary>
    Dictionary<string, object?> SketchDimension(
        string mode, string entity1, string entity2 = "",
        double? value = null, string orientation = "aligned",
        double? positionX = null, double? positionY = null);

    /// <summary>Draw a point in the active sketch.</summary>
    Dictionary<string, object?> SketchPoint(double x, double y);

    /// <summary>Draw a spline through fit points.</summary>
    Dictionary<string, object?> SketchSpline(string points, string fitMethod = "sweet");

    /// <summary>Draw an ellipse in the active sketch.</summary>
    Dictionary<string, object?> SketchEllipse(
        double cx, double cy, double majorRadius, double minorRadius,
        double majorAxisAngle = 0.0);

    /// <summary>Create a circular pattern of sketch entities.</summary>
    Dictionary<string, object?> SketchCircularPattern(
        string entities, string axis, int count,
        double angle = 360.0, bool fitted = true, bool symmetric = false);

    /// <summary>Create a rectangular pattern of sketch entities.</summary>
    Dictionary<string, object?> SketchRectangularPattern(
        string entities, string xAxis, int xCount, double xSpacing,
        string yAxis = "", int yCount = 1, double ySpacing = 0.0);

    /// <summary>Offset sketch entities through a point.</summary>
    Dictionary<string, object?> SketchOffset(
        string entities, double offsetX, double offsetY,
        bool includeConnected = false);

    /// <summary>Move sketch entities by a vector.</summary>
    Dictionary<string, object?> SketchMove(
        string entities, double dx, double dy, bool copy = false);

    /// <summary>Rotate sketch entities around a center point.</summary>
    Dictionary<string, object?> SketchRotate(
        string entities, double cx, double cy, double angle, bool copy = false);

    /// <summary>Delete the active sketch (must not be used by a feature).</summary>
    Dictionary<string, object?> SketchDelete();

    /// <summary>Add a geometric constraint between sketch entities.</summary>
    Dictionary<string, object?> SketchConstraint(
        string mode, string entity1, string entity2 = "",
        string symLine = "", string axis = "major");

    /// <summary>Trim a sketch entity to its intersection with another.</summary>
    Dictionary<string, object?> SketchTrim(
        string entity, string cuttingEntity, string side = "end");

    /// <summary>Scale sketch entities around a center point.</summary>
    Dictionary<string, object?> SketchScale(
        string entities, double cx, double cy, double factor);

    /// <summary>Mirror sketch entities across a mirror line.</summary>
    Dictionary<string, object?> SketchMirror(string entities, string mirrorEntity);

    #endregion

    #region Skills

    /// <summary>Close the connected profile by linking the last endpoint to the first.</summary>
    Dictionary<string, object?> SketchLineClose();

    #endregion

    #region Features

    /// <summary>Extrude a sketch profile to create a 3D feature.</summary>
    Dictionary<string, object?> Extrude(
        string profile, double distance, string direction = "positive",
        double taper = 0.0, string operation = "new_body");

    /// <summary>Revolve a profile around an axis.</summary>
    Dictionary<string, object?> Revolve(
        string profile, string axis, double angle = 360.0,
        string direction = "positive", string operation = "join");

    /// <summary>Apply a fillet to the specified edges.</summary>
    Dictionary<string, object?> Fillet(string edges, double radius, string mode = "constant");

    /// <summary>Apply a chamfer to the specified edges.</summary>
    Dictionary<string, object?> Chamfer(string edges, double distance, string mode = "equal_distance");

    /// <summary>Create a circular pattern of a feature around an axis.</summary>
    Dictionary<string, object?> CircularPattern(
        string profile, string axis, int count,
        double angle = 360.0, bool fitWithinAngle = true,
        bool naturalDirection = true);

    /// <summary>Create a hole feature at the specified point.</summary>
    Dictionary<string, object?> Hole(
        double x, double y, double diameter, double depth,
        string type = "drilled", string operation = "join");

    /// <summary>Create a thread feature on a cylindrical face.</summary>
    Dictionary<string, object?> Thread(
        string face, string specification, string direction = "right");

    /// <summary>List all edges of the active body with geometry info.</summary>
    Dictionary<string, object?> InspectEdges();

    #endregion

    #region Parameters

    /// <summary>List model parameters, optionally filtered by name pattern.</summary>
    Dictionary<string, object?> ParamList(string? filterPattern = null);

    /// <summary>Get a specific model parameter by name.</summary>
    Dictionary<string, object?> ParamGet(string name);

    /// <summary>Set a model parameter value by name.</summary>
    Dictionary<string, object?> ParamSet(string name, double value);

    /// <summary>Set a model parameter using an expression.</summary>
    Dictionary<string, object?> ParamSetExpression(string name, string expression);

    #endregion

    #region Properties

    /// <summary>Get an iProperty value by name.</summary>
    Dictionary<string, object?> IPropertyGet(string name, string propertySet = "Summary");

    /// <summary>Set an iProperty value by name.</summary>
    Dictionary<string, object?> IPropertySet(string name, object value, string propertySet = "Summary");

    /// <summary>Get all Summary iProperties.</summary>
    Dictionary<string, object?> IPropertySummary();

    /// <summary>Get a custom iProperty by name.</summary>
    Dictionary<string, object?> IPropertyCustomGet(string name);

    /// <summary>Set a custom iProperty. Creates it if it doesn't exist.</summary>
    Dictionary<string, object?> IPropertyCustomSet(string name, object value);

    #endregion

    #region Export

    /// <summary>Export the active document to STEP format.</summary>
    Dictionary<string, object?> ExportStep(string path, Dictionary<string, object?>? options = null);

    /// <summary>Export the active document to STL format.</summary>
    Dictionary<string, object?> ExportStl(string path, Dictionary<string, object?>? options = null);

    /// <summary>Export the active document to PDF format.</summary>
    Dictionary<string, object?> ExportPdf(string path, Dictionary<string, object?>? options = null);

    /// <summary>Export the active document's sketch or flat pattern to DXF.</summary>
    Dictionary<string, object?> ExportDxf(string path, Dictionary<string, object?>? options = null);

    #endregion
}