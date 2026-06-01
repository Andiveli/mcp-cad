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

    #endregion

    #region Features

    Dictionary<string, object?> Extrude(string profile, double distance, string direction = "positive", double taper = 0.0, string operation = "new_body");
    Dictionary<string, object?> Revolve(string profile, string axis, double angle = 360.0, string direction = "positive", string operation = "join");
    Dictionary<string, object?> Fillet(string edges, double radius, string mode = "constant");
    Dictionary<string, object?> Chamfer(string edges, double distance, string mode = "equal_distance");
    Dictionary<string, object?> CircularPattern(string profile, string axis, int count, double angle = 360.0, bool fitWithinAngle = true, bool naturalDirection = true);
    Dictionary<string, object?> Hole(double x, double y, double diameter, double depth, string type = "drilled", string operation = "join");
    Dictionary<string, object?> Thread(string face, string specification, string direction = "right");
    Dictionary<string, object?> InspectEdges();

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
}
