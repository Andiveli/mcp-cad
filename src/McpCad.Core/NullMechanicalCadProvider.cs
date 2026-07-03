namespace McpCad.Core;

/// <summary>
/// Stub <see cref="IMechanicalCadProvider"/> used when a web CAD backend is active
/// so Inventor COM is never invoked.
/// </summary>
public sealed class NullMechanicalCadProvider : IMechanicalCadProvider
{
    private const string Message =
        "Inventor mechanical CAD is not available in this process; set Provider:Active to inventor or use Onshape tools.";

    private static Dictionary<string, object?> NotAvailable() => new()
    {
        ["success"] = false,
        ["error"] = Message,
    };

    public Dictionary<string, object?> Connect() => NotAvailable();
    public Dictionary<string, object?> Disconnect() => NotAvailable();
    public Dictionary<string, object?> Health() => NotAvailable();
    public Dictionary<string, object?> DocOpen(string path) => NotAvailable();
    public Dictionary<string, object?> DocNewPart(string template = "") => NotAvailable();
    public Dictionary<string, object?> DocNewAssembly(string template = "") => NotAvailable();
    public Dictionary<string, object?> DocSave() => NotAvailable();
    public Dictionary<string, object?> DocSaveAs(string path) => NotAvailable();
    public Dictionary<string, object?> DocClose(bool save = true) => NotAvailable();
    public Dictionary<string, object?> ExportStep(string path, Dictionary<string, object?>? options = null) => NotAvailable();
    public Dictionary<string, object?> ExportStl(string path, Dictionary<string, object?>? options = null) => NotAvailable();
    public Dictionary<string, object?> ExportPdf(string path, Dictionary<string, object?>? options = null) => NotAvailable();
    public Dictionary<string, object?> ExportDxf(string path, Dictionary<string, object?>? options = null) => NotAvailable();
    public Dictionary<string, object?> SketchCreate(string plane = "XY") => NotAvailable();
    public Dictionary<string, object?> SketchLine(double x1, double y1, double x2, double y2, string? tag = null, bool connect = false) => NotAvailable();
    public Dictionary<string, object?> SketchCircle(double cx, double cy, double radius, string? tag = null) => NotAvailable();
    public Dictionary<string, object?> SketchArc(double cx, double cy, double radius, double startAngle, double endAngle) => NotAvailable();
    public Dictionary<string, object?> SketchRectangle(double x1, double y1, double x2, double y2) => NotAvailable();
    public Dictionary<string, object?> SketchDimension(string mode, string entity1, string entity2 = "", double? value = null, string orientation = "aligned", double? positionX = null, double? positionY = null) => NotAvailable();
    public Dictionary<string, object?> SketchPoint(double x, double y) => NotAvailable();
    public Dictionary<string, object?> SketchSpline(string points, string fitMethod = "sweet") => NotAvailable();
    public Dictionary<string, object?> SketchEllipse(double cx, double cy, double majorRadius, double minorRadius, double majorAxisAngle = 0.0) => NotAvailable();
    public Dictionary<string, object?> SketchCircularPattern(string entities, string axis, int count, double angle = 360.0, bool fitted = true, bool symmetric = false) => NotAvailable();
    public Dictionary<string, object?> SketchRectangularPattern(string entities, string xAxis, int xCount, double xSpacing, string yAxis = "", int yCount = 1, double ySpacing = 0.0) => NotAvailable();
    public Dictionary<string, object?> SketchOffset(string entities, double offsetX, double offsetY, bool includeConnected = false) => NotAvailable();
    public Dictionary<string, object?> SketchMove(string entities, double dx, double dy, bool copy = false) => NotAvailable();
    public Dictionary<string, object?> SketchRotate(string entities, double cx, double cy, double angle, bool copy = false) => NotAvailable();
    public Dictionary<string, object?> SketchDelete() => NotAvailable();
    public Dictionary<string, object?> SketchConstraint(string mode, string entity1, string entity2 = "", string symLine = "", string axis = "major") => NotAvailable();
    public Dictionary<string, object?> SketchTrim(string entity, string cuttingEntity, string side = "end") => NotAvailable();
    public Dictionary<string, object?> SketchScale(string entities, double cx, double cy, double factor) => NotAvailable();
    public Dictionary<string, object?> SketchMirror(string entities, string mirrorEntity) => NotAvailable();
    public Dictionary<string, object?> SketchLineClose() => NotAvailable();
    public Dictionary<string, object?> SketchProfiles() => NotAvailable();
    public Dictionary<string, object?> Extrude(string profile, double distance, string direction = "positive", double taper = 0.0, string operation = "new_body") => NotAvailable();
    public Dictionary<string, object?> Revolve(string profile, string axis, double angle = 360.0, string direction = "positive", string operation = "join") => NotAvailable();
    public Dictionary<string, object?> Sweep(string profile, string path, string sweepType = "path", string operation = "new_body", double taper = 0, string pathSketch = "", string profileSketch = "") => NotAvailable();
    public Dictionary<string, object?> Fillet(string edges, double radius, string mode = "constant") => NotAvailable();
    public Dictionary<string, object?> Chamfer(string edges, double distance, string mode = "equal_distance") => NotAvailable();
    public Dictionary<string, object?> CircularPattern(string profile, string axis, int count, double angle = 360.0, bool fitWithinAngle = true, bool naturalDirection = true) => NotAvailable();
    public Dictionary<string, object?> MirrorFeature(string profile, string mirrorPlane) => NotAvailable();
    public Dictionary<string, object?> RectangularPattern(string profile, string xAxis, int xCount, double xSpacing, string yAxis = "", int yCount = 1, double ySpacing = 0.0) => NotAvailable();
    public Dictionary<string, object?> Loft(string profiles, string operation = "new_body") => NotAvailable();
    public Dictionary<string, object?> Coil(string profile, string axis, double pitch, double revolutions, string operation = "new_body") => NotAvailable();
    public Dictionary<string, object?> Rib(string profile, double thickness, string direction = "normal", string operation = "new_body") => NotAvailable();
    public Dictionary<string, object?> Emboss(string profile, double depth, string type = "emboss_from_face") => NotAvailable();
    public Dictionary<string, object?> Derive(string sourcePath) => NotAvailable();
    public Dictionary<string, object?> Hole(double x, double y, double diameter, double depth, string type = "drilled", string operation = "join") => NotAvailable();
    public Dictionary<string, object?> Thread(string face, string specification, string direction = "right") => NotAvailable();
    public Dictionary<string, object?> InspectEdges() => NotAvailable();
    public Dictionary<string, object?> Shell(string faces, double thickness, string direction = "inside", string operation = "new_body") => NotAvailable();
    public Dictionary<string, object?> Draft(string faces, double angle, string mode = "fixed_edge", string pullDirection = "z", string fixedEntity = "") => NotAvailable();
    public Dictionary<string, object?> Split(string splitTool, string removeSide = "positive", string targetBody = "") => NotAvailable();
    public Dictionary<string, object?> Combine(string baseBody, string toolBodies, string operation = "join", bool keepToolBodies = false) => NotAvailable();
    public Dictionary<string, object?> Thicken(string faces, double thickness, string direction = "positive", string operation = "new_body") => NotAvailable();
    public Dictionary<string, object?> ParamList(string? filterPattern = null) => NotAvailable();
    public Dictionary<string, object?> ParamGet(string name) => NotAvailable();
    public Dictionary<string, object?> ParamSet(string name, double value) => NotAvailable();
    public Dictionary<string, object?> ParamSetExpression(string name, string expression) => NotAvailable();
    public Dictionary<string, object?> IPropertyGet(string name, string propertySet = "Summary") => NotAvailable();
    public Dictionary<string, object?> IPropertySet(string name, string? value, string propertySet = "Summary") => NotAvailable();
    public Dictionary<string, object?> IPropertySummary() => NotAvailable();
    public Dictionary<string, object?> IPropertyCustomGet(string name) => NotAvailable();
    public Dictionary<string, object?> IPropertyCustomSet(string name, string? value) => NotAvailable();
    public Dictionary<string, object?> WorkPlane(string definition, string reference1, string reference2, double offset) => NotAvailable();
    public Dictionary<string, object?> WorkAxis(string definition, string reference1, string reference2) => NotAvailable();
    public Dictionary<string, object?> WorkPoint(string definition, string reference1, string reference2, string reference3) => NotAvailable();
    public Dictionary<string, object?> AsmListComponents() => NotAvailable();
    public Dictionary<string, object?> AsmListConstraints() => NotAvailable();
    public Dictionary<string, object?> AsmPlaceComponent(string path, double x = 0, double y = 0, double z = 0) => NotAvailable();
    public Dictionary<string, object?> AsmGroundComponent(string occurrence) => NotAvailable();
    public Dictionary<string, object?> AsmReplaceComponent(string occurrence, string newPath) => NotAvailable();
    public Dictionary<string, object?> AsmDeleteConstraint(string constraint) => NotAvailable();
    public Dictionary<string, object?> AsmConstraintMate(string entityOne, string entityTwo, double offset = 0) => NotAvailable();
    public Dictionary<string, object?> AsmConstraintFlush(string entityOne, string entityTwo, double offset = 0) => NotAvailable();
    public Dictionary<string, object?> AsmConstraintAngle(string entityOne, string entityTwo, double angle, string solution = "directed") => NotAvailable();
    public Dictionary<string, object?> AsmConstraintInsert(string entityOne, string entityTwo, double offset = 0) => NotAvailable();
    public Dictionary<string, object?> AsmConstraintTangent(string entityOne, string entityTwo, double offset = 0) => NotAvailable();
    public Dictionary<string, object?> AsmCircularPattern(string occurrence, string axis, int count, double angle = 360) => NotAvailable();
    public Dictionary<string, object?> AsmRectangularPattern(string occurrence, string xAxis, int xCount, double xSpacing, string? yAxis = null, int yCount = 1, double ySpacing = 0) => NotAvailable();
    public Dictionary<string, object?> AsmExtrudeCut(string profile, double distance, string direction = "positive") => NotAvailable();
    public Dictionary<string, object?> AsmHole(double x, double y, double diameter, double depth, string type = "drilled") => NotAvailable();
    public Dictionary<string, object?> AsmBom() => NotAvailable();
}
