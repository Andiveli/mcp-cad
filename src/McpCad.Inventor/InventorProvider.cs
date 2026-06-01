using McpCad.Core;
using McpCad.Inventor.Managers;

namespace McpCad.Inventor;

/// <summary>
/// Implements IMechanicalCadProvider by delegating to the six managers.
/// Each manager handles a domain (sketch, features, documents, etc.)
/// and the driver handles COM connection lifecycle.
/// </summary>
public class InventorProvider : IMechanicalCadProvider
{
    private readonly InventorDriver _driver;
    private readonly SketchManager _sketch;
    private readonly FeatureManager _feature;
    private readonly DocumentManager _document;
    private readonly ParameterManager _parameter;
    private readonly PropertyManager _property;
    private readonly ExportManager _export;

    public InventorProvider(InventorDriver driver)
    {
        _driver = driver;
        _sketch = new SketchManager(driver);
        _feature = new FeatureManager(driver);
        _document = new DocumentManager(driver);
        _parameter = new ParameterManager(driver);
        _property = new PropertyManager(driver);
        _export = new ExportManager(driver);
    }

    // ── Connection ────────────────────────────────────────────────────

    public Dictionary<string, object?> Connect() => _driver.Connect();
    public Dictionary<string, object?> Disconnect() => _driver.Disconnect();
    public Dictionary<string, object?> Health() => _driver.Health();

    // ── Documents ─────────────────────────────────────────────────────

    public Dictionary<string, object?> DocOpen(string path) => _document.DocOpen(path);
    public Dictionary<string, object?> DocNewPart(string template = "") => _document.DocNewPart(template);
    public Dictionary<string, object?> DocNewAssembly(string template = "") => _document.DocNewAssembly(template);
    public Dictionary<string, object?> DocSave() => _document.DocSave();
    public Dictionary<string, object?> DocSaveAs(string path) => _document.DocSaveAs(path);
    public Dictionary<string, object?> DocClose(bool save = true) => _document.DocClose(save);

    // ── Sketch ────────────────────────────────────────────────────────

    public Dictionary<string, object?> SketchCreate(string plane = "XY") => _sketch.SketchCreate(plane);
    public Dictionary<string, object?> SketchLine(
        double x1, double y1, double x2, double y2,
        string? tag = null, bool connect = false)
        => _sketch.SketchLine(x1, y1, x2, y2, tag, connect);

    public Dictionary<string, object?> SketchCircle(double cx, double cy, double radius, string? tag = null)
        => _sketch.SketchCircle(cx, cy, radius, tag);

    public Dictionary<string, object?> SketchArc(
        double cx, double cy, double radius, double startAngle, double endAngle)
        => _sketch.SketchArc(cx, cy, radius, startAngle, endAngle);

    public Dictionary<string, object?> SketchRectangle(double x1, double y1, double x2, double y2)
        => _sketch.SketchRectangle(x1, y1, x2, y2);

    public Dictionary<string, object?> SketchDimension(
        string mode, string entity1, string entity2 = "",
        double? value = null, string orientation = "aligned",
        double? positionX = null, double? positionY = null)
        => _sketch.SketchDimension(mode, entity1, entity2, value, orientation, positionX, positionY);

    public Dictionary<string, object?> SketchPoint(double x, double y) => _sketch.SketchPoint(x, y);
    public Dictionary<string, object?> SketchSpline(string points, string fitMethod = "sweet")
        => _sketch.SketchSpline(points, fitMethod);

    public Dictionary<string, object?> SketchEllipse(
        double cx, double cy, double majorRadius, double minorRadius, double majorAxisAngle = 0.0)
        => _sketch.SketchEllipse(cx, cy, majorRadius, minorRadius, majorAxisAngle);

    public Dictionary<string, object?> SketchCircularPattern(
        string entities, string axis, int count,
        double angle = 360.0, bool fitted = true, bool symmetric = false)
        => _sketch.SketchCircularPattern(entities, axis, count, angle, fitted, symmetric);

    public Dictionary<string, object?> SketchRectangularPattern(
        string entities, string xAxis, int xCount, double xSpacing,
        string yAxis = "", int yCount = 1, double ySpacing = 0.0)
        => _sketch.SketchRectangularPattern(entities, xAxis, xCount, xSpacing, yAxis, yCount, ySpacing);

    public Dictionary<string, object?> SketchOffset(
        string entities, double offsetX, double offsetY, bool includeConnected = false)
        => _sketch.SketchOffset(entities, offsetX, offsetY, includeConnected);

    public Dictionary<string, object?> SketchMove(string entities, double dx, double dy, bool copy = false)
        => _sketch.SketchMove(entities, dx, dy, copy);

    public Dictionary<string, object?> SketchRotate(
        string entities, double cx, double cy, double angle, bool copy = false)
        => _sketch.SketchRotate(entities, cx, cy, angle, copy);

    public Dictionary<string, object?> SketchDelete() => _sketch.SketchDelete();

    public Dictionary<string, object?> SketchConstraint(
        string mode, string entity1, string entity2 = "",
        string symLine = "", string axis = "major")
        => _sketch.SketchConstraint(mode, entity1, entity2, symLine, axis);

    public Dictionary<string, object?> SketchTrim(string entity, string cuttingEntity, string side = "end")
        => _sketch.SketchTrim(entity, cuttingEntity, side);

    public Dictionary<string, object?> SketchScale(string entities, double cx, double cy, double factor)
        => _sketch.SketchScale(entities, cx, cy, factor);

    public Dictionary<string, object?> SketchMirror(string entities, string mirrorEntity)
        => _sketch.SketchMirror(entities, mirrorEntity);

    // ── Skills ────────────────────────────────────────────────────────

    public Dictionary<string, object?> SketchLineClose() => _sketch.SketchLineClose();

    // ── Features ──────────────────────────────────────────────────────

    public Dictionary<string, object?> Extrude(
        string profile, double distance, string direction = "positive",
        double taper = 0.0, string operation = "new_body")
        => _feature.Extrude(profile, distance, direction, taper, operation);

    public Dictionary<string, object?> Revolve(
        string profile, string axis, double angle = 360.0,
        string direction = "positive", string operation = "join")
        => _feature.Revolve(profile, axis, angle, direction, operation);

    public Dictionary<string, object?> Fillet(string edges, double radius, string mode = "constant")
        => _feature.Fillet(edges, radius, mode);

    public Dictionary<string, object?> Chamfer(string edges, double distance, string mode = "equal_distance")
        => _feature.Chamfer(edges, distance, mode);

public Dictionary<string, object?> CircularPattern(
        string profile, string axis, int count,
        double angle = 360.0, bool fitWithinAngle = true,
        bool naturalDirection = true)
        => _feature.CircularPattern(profile, axis, count, angle, fitWithinAngle, naturalDirection);

    public Dictionary<string, object?> Hole(
        double x, double y, double diameter, double depth,
        string type = "drilled", string operation = "join")
        => _feature.Hole(x, y, diameter, depth, type, operation);

    public Dictionary<string, object?> Thread(string face, string specification, string direction = "right")
        => _feature.Thread(face, specification, direction);

    public Dictionary<string, object?> InspectEdges() => _feature.InspectEdges();

    // ── Parameters ───────────────────────────────────────────────────

    public Dictionary<string, object?> ParamList(string? filterPattern = null)
        => _parameter.ParamList(filterPattern);

    public Dictionary<string, object?> ParamGet(string name) => _parameter.ParamGet(name);
    public Dictionary<string, object?> ParamSet(string name, double value) => _parameter.ParamSet(name, value);
    public Dictionary<string, object?> ParamSetExpression(string name, string expression)
        => _parameter.ParamSetExpression(name, expression);

    // ── Properties ─────────────────────────────────────────────────────

    public Dictionary<string, object?> IPropertyGet(string name, string propertySet = "Summary")
        => _property.IPropertyGet(name, propertySet);

    public Dictionary<string, object?> IPropertySet(string name, string? value, string propertySet = "Summary")
        => _property.IPropertySet(name, value, propertySet);

    public Dictionary<string, object?> IPropertySummary() => _property.IPropertySummary();
    public Dictionary<string, object?> IPropertyCustomGet(string name) => _property.IPropertyCustomGet(name);
    public Dictionary<string, object?> IPropertyCustomSet(string name, string? value) => _property.IPropertyCustomSet(name, value);

    // ── Export ─────────────────────────────────────────────────────────

    public Dictionary<string, object?> ExportStep(string path, Dictionary<string, object?>? options = null)
        => _export.ExportStep(path, options);

    public Dictionary<string, object?> ExportStl(string path, Dictionary<string, object?>? options = null)
        => _export.ExportStl(path, options);

    public Dictionary<string, object?> ExportPdf(string path, Dictionary<string, object?>? options = null)
        => _export.ExportPdf(path, options);

    public Dictionary<string, object?> ExportDxf(string path, Dictionary<string, object?>? options = null)
        => _export.ExportDxf(path, options);
}