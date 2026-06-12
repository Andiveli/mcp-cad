using McpCad.Core;
using McpCad.Core.Models;
using McpCad.SolidWorks.Helpers;
using McpCad.SolidWorks.Managers;

namespace McpCad.SolidWorks;

/// <summary>
/// Implements IMechanicalCadProvider (and ICadProvider) by delegating to SolidWorksDriver + minimal managers.
/// Phase 3 wiring complete + Phase 4+5 complete this batch: Document + Sketch + Feature (extrude min) + Inspection + SwTagStore/SelectionHelper.
/// Helpers integrated for profile resolve (index priority MVP + @tag in-mem) in Feature/Inspection.
/// Other methods beyond basic loop return clear ErrorResult.Create("Not yet implemented for SolidWorks provider. See roadmap.") per spec/design/tasks 4.x-5.x.
/// 
/// Ctor instantiates helpers (in-mem) + 4 managers (Document/Sketch/Feature/Inspection). Mirrors InventorProvider thin-delegator but scoped.
/// Uses generalized Cad* exceptions (in driver/managers). SelectByID2 / InsertExtrude / FirstFeature documented in managers.
/// </summary>
public class SolidWorksProvider : IMechanicalCadProvider
{
    private readonly SolidWorksDriver _driver;
    private readonly DocumentManager _document;
    private readonly SketchManager _sketch;
    private readonly FeatureManager _feature;
    private readonly InspectionManager _inspection;
    private readonly SwTagStore _tagStore;
    private readonly SelectionHelper _selectionHelper;

    public SolidWorksProvider(SolidWorksDriver driver)
    {
        _driver = driver ?? throw new ArgumentNullException(nameof(driver));
        _tagStore = new SwTagStore();
        _selectionHelper = new SelectionHelper();
        _document = new DocumentManager(driver);
        _sketch = new SketchManager(driver, _tagStore);
        _feature = new FeatureManager(driver, _tagStore, _selectionHelper);
        _inspection = new InspectionManager(driver, _tagStore, _selectionHelper); // full wiring for helpers (MVP + future capture/tag)
        // Ctor wiring meets contract tests + tagging/selection for basic loop (index/tag -> selectable profile in extrude)
    }

    // ── Connection (delegate to driver; basic MVP for skeleton) ────────────────────────────────

    public Dictionary<string, object?> Connect() => _driver.Connect();
    public Dictionary<string, object?> Disconnect() => _driver.Disconnect();
    public Dictionary<string, object?> Health() => _driver.Health();

    // ── Documents (full for basic loop via DocumentManager 4.1) ────────────────────────────────

    public Dictionary<string, object?> DocOpen(string path) => _document.DocOpen(path);

    public Dictionary<string, object?> DocNewPart(string template = "") => _document.DocNewPart(template);

    public Dictionary<string, object?> DocNewAssembly(string template = "") => _document.DocNewAssembly(template);

    public Dictionary<string, object?> DocSave() => _document.DocSave();

    public Dictionary<string, object?> DocSaveAs(string path) => _document.DocSaveAs(path);

    public Dictionary<string, object?> DocClose(bool save = true) => _document.DocClose(save);

    // ── Export (ICadProvider) — stubs ──────────────────────────────────────────────────────────

    public Dictionary<string, object?> ExportStep(string path, Dictionary<string, object?>? options = null)
        => ErrorResult.Create("Not yet implemented for SolidWorks provider. See roadmap.");

    public Dictionary<string, object?> ExportStl(string path, Dictionary<string, object?>? options = null)
        => ErrorResult.Create("Not yet implemented for SolidWorks provider. See roadmap.");

    public Dictionary<string, object?> ExportPdf(string path, Dictionary<string, object?>? options = null)
        => ErrorResult.Create("Not yet implemented for SolidWorks provider. See roadmap.");

    public Dictionary<string, object?> ExportDxf(string path, Dictionary<string, object?>? options = null)
        => ErrorResult.Create("Not yet implemented for SolidWorks provider. See roadmap.");

    // ── Sketch (basic loop MVP via SketchManager 4.2: create + line/circle + profiles; others Error) ─

    public Dictionary<string, object?> SketchCreate(string plane = "XY") => _sketch.SketchCreate(plane);

    public Dictionary<string, object?> SketchLine(double x1, double y1, double x2, double y2, string? tag = null, bool connect = false)
        => _sketch.SketchLine(x1, y1, x2, y2, tag, connect);

    public Dictionary<string, object?> SketchCircle(double cx, double cy, double radius, string? tag = null)
        => _sketch.SketchCircle(cx, cy, radius, tag);

    public Dictionary<string, object?> SketchArc(double cx, double cy, double radius, double startAngle, double endAngle)
        => ErrorResult.Create("Not yet implemented for SolidWorks provider. See roadmap.");

    public Dictionary<string, object?> SketchRectangle(double x1, double y1, double x2, double y2)
        => ErrorResult.Create("Not yet implemented for SolidWorks provider. See roadmap.");

    public Dictionary<string, object?> SketchDimension(string mode, string entity1, string entity2 = "", double? value = null, string orientation = "aligned", double? positionX = null, double? positionY = null)
        => ErrorResult.Create("Not yet implemented for SolidWorks provider. See roadmap.");

    public Dictionary<string, object?> SketchPoint(double x, double y)
        => ErrorResult.Create("Not yet implemented for SolidWorks provider. See roadmap.");

    public Dictionary<string, object?> SketchSpline(string points, string fitMethod = "sweet")
        => ErrorResult.Create("Not yet implemented for SolidWorks provider. See roadmap.");

    public Dictionary<string, object?> SketchEllipse(double cx, double cy, double majorRadius, double minorRadius, double majorAxisAngle = 0.0)
        => ErrorResult.Create("Not yet implemented for SolidWorks provider. See roadmap.");

    public Dictionary<string, object?> SketchCircularPattern(string entities, string axis, int count, double angle = 360.0, bool fitted = true, bool symmetric = false)
        => ErrorResult.Create("Not yet implemented for SolidWorks provider. See roadmap.");

    public Dictionary<string, object?> SketchRectangularPattern(string entities, string xAxis, int xCount, double xSpacing, string yAxis = "", int yCount = 1, double ySpacing = 0.0)
        => ErrorResult.Create("Not yet implemented for SolidWorks provider. See roadmap.");

    public Dictionary<string, object?> SketchOffset(string entities, double offsetX, double offsetY, bool includeConnected = false)
        => ErrorResult.Create("Not yet implemented for SolidWorks provider. See roadmap.");

    public Dictionary<string, object?> SketchMove(string entities, double dx, double dy, bool copy = false)
        => ErrorResult.Create("Not yet implemented for SolidWorks provider. See roadmap.");

    public Dictionary<string, object?> SketchRotate(string entities, double cx, double cy, double angle, bool copy = false)
        => ErrorResult.Create("Not yet implemented for SolidWorks provider. See roadmap.");

    public Dictionary<string, object?> SketchDelete()
        => ErrorResult.Create("Not yet implemented for SolidWorks provider. See roadmap.");

    public Dictionary<string, object?> SketchConstraint(string mode, string entity1, string entity2 = "", string symLine = "", string axis = "major")
        => ErrorResult.Create("Not yet implemented for SolidWorks provider. See roadmap.");

    public Dictionary<string, object?> SketchTrim(string entity, string cuttingEntity, string side = "end")
        => ErrorResult.Create("Not yet implemented for SolidWorks provider. See roadmap.");

    public Dictionary<string, object?> SketchScale(string entities, double cx, double cy, double factor)
        => ErrorResult.Create("Not yet implemented for SolidWorks provider. See roadmap.");

    public Dictionary<string, object?> SketchMirror(string entities, string mirrorEntity)
        => ErrorResult.Create("Not yet implemented for SolidWorks provider. See roadmap.");

    public Dictionary<string, object?> SketchLineClose() => _sketch.SketchLineClose();

    public Dictionary<string, object?> SketchProfiles() => _sketch.SketchProfiles();

    // ── Features (MVP extrude delegated; others clear not-impl per Phase 4.3 narrow) ────────────

    public Dictionary<string, object?> Extrude(string profile, double distance, string direction = "positive", double taper = 0.0, string operation = "new_body")
        => _feature.Extrude(profile, distance, direction, taper, operation);

    public Dictionary<string, object?> Revolve(string profile, string axis, double angle = 360.0, string direction = "positive", string operation = "join")
        => ErrorResult.Create("Not yet implemented for SolidWorks provider. See roadmap.");

    public Dictionary<string, object?> Sweep(string profile, string path, string sweepType = "path", string operation = "new_body", double taper = 0, string pathSketch = "", string profileSketch = "")
        => ErrorResult.Create("Not yet implemented for SolidWorks provider. See roadmap.");

    public Dictionary<string, object?> Fillet(string edges, double radius, string mode = "constant")
        => ErrorResult.Create("Not yet implemented for SolidWorks provider. See roadmap.");

    public Dictionary<string, object?> Chamfer(string edges, double distance, string mode = "equal_distance")
        => ErrorResult.Create("Not yet implemented for SolidWorks provider. See roadmap.");

    public Dictionary<string, object?> CircularPattern(string profile, string axis, int count, double angle = 360.0, bool fitWithinAngle = true, bool naturalDirection = true)
        => ErrorResult.Create("Not yet implemented for SolidWorks provider. See roadmap.");

    public Dictionary<string, object?> MirrorFeature(string profile, string mirrorPlane)
        => ErrorResult.Create("Not yet implemented for SolidWorks provider. See roadmap.");

    public Dictionary<string, object?> RectangularPattern(string profile, string xAxis, int xCount, double xSpacing, string yAxis = "", int yCount = 1, double ySpacing = 0.0)
        => ErrorResult.Create("Not yet implemented for SolidWorks provider. See roadmap.");

    public Dictionary<string, object?> Loft(string profiles, string operation = "new_body")
        => ErrorResult.Create("Not yet implemented for SolidWorks provider. See roadmap.");

    public Dictionary<string, object?> Coil(string profile, string axis, double pitch, double revolutions, string operation = "new_body")
        => ErrorResult.Create("Not yet implemented for SolidWorks provider. See roadmap.");

    public Dictionary<string, object?> Rib(string profile, double thickness, string direction = "normal", string operation = "new_body")
        => ErrorResult.Create("Not yet implemented for SolidWorks provider. See roadmap.");

    public Dictionary<string, object?> Emboss(string profile, double depth, string type = "emboss_from_face")
        => ErrorResult.Create("Not yet implemented for SolidWorks provider. See roadmap.");

    public Dictionary<string, object?> Derive(string sourcePath)
        => ErrorResult.Create("Not yet implemented for SolidWorks provider. See roadmap.");

    // Exact 6-param match to IMechanicalCadProvider (no plane; no ReadSketchData/TagFacesFromSketch pollution per CRITICAL 5 contract stability)
    // Signature exactly matches interface: Hole(double x, double y, double diameter, double depth, string type, string operation)
    public Dictionary<string, object?> Hole(double x, double y, double diameter, double depth, string type = "drilled", string operation = "join")
        => ErrorResult.Create("Not yet implemented for SolidWorks provider. See roadmap.");

    public Dictionary<string, object?> Thread(string face, string specification, string direction = "right")
        => ErrorResult.Create("Not yet implemented for SolidWorks provider. See roadmap.");

    public Dictionary<string, object?> InspectEdges()
        => ErrorResult.Create("Not yet implemented for SolidWorks provider. See roadmap.");

    public Dictionary<string, object?> Shell(string faces, double thickness, string direction = "inside", string operation = "new_body")
        => ErrorResult.Create("Not yet implemented for SolidWorks provider. See roadmap.");

    public Dictionary<string, object?> Draft(string faces, double angle, string mode = "fixed_edge", string pullDirection = "z", string fixedEntity = "")
        => ErrorResult.Create("Not yet implemented for SolidWorks provider. See roadmap.");

    public Dictionary<string, object?> Split(string splitTool, string removeSide = "positive", string targetBody = "")
        => ErrorResult.Create("Not yet implemented for SolidWorks provider. See roadmap.");

    public Dictionary<string, object?> Combine(string baseBody, string toolBodies, string operation = "join", bool keepToolBodies = false)
        => ErrorResult.Create("Not yet implemented for SolidWorks provider. See roadmap.");

    public Dictionary<string, object?> Thicken(string faces, double thickness, string direction = "positive", string operation = "new_body")
        => ErrorResult.Create("Not yet implemented for SolidWorks provider. See roadmap.");

    // ── Parameters — stubs ─────────────────────────────────────────────────────────────────────

    public Dictionary<string, object?> ParamList(string? filterPattern = null)
        => ErrorResult.Create("Not yet implemented for SolidWorks provider. See roadmap.");

    public Dictionary<string, object?> ParamGet(string name)
        => ErrorResult.Create("Not yet implemented for SolidWorks provider. See roadmap.");

    public Dictionary<string, object?> ParamSet(string name, double value)
        => ErrorResult.Create("Not yet implemented for SolidWorks provider. See roadmap.");

    public Dictionary<string, object?> ParamSetExpression(string name, string expression)
        => ErrorResult.Create("Not yet implemented for SolidWorks provider. See roadmap.");

    // ── Properties — stubs ─────────────────────────────────────────────────────────────────────

    public Dictionary<string, object?> IPropertyGet(string name, string propertySet = "Summary")
        => ErrorResult.Create("Not yet implemented for SolidWorks provider. See roadmap.");

    public Dictionary<string, object?> IPropertySet(string name, string? value, string propertySet = "Summary")
        => ErrorResult.Create("Not yet implemented for SolidWorks provider. See roadmap.");

    public Dictionary<string, object?> IPropertySummary()
        => ErrorResult.Create("Not yet implemented for SolidWorks provider. See roadmap.");

    public Dictionary<string, object?> IPropertyCustomGet(string name)
        => ErrorResult.Create("Not yet implemented for SolidWorks provider. See roadmap.");

    public Dictionary<string, object?> IPropertyCustomSet(string name, string? value)
        => ErrorResult.Create("Not yet implemented for SolidWorks provider. See roadmap.");

    // ── WorkFeatures — stubs ───────────────────────────────────────────────────────────────────

    public Dictionary<string, object?> WorkPlane(string definition, string reference1, string reference2, double offset)
        => ErrorResult.Create("Not yet implemented for SolidWorks provider. See roadmap.");

    public Dictionary<string, object?> WorkAxis(string definition, string reference1, string reference2)
        => ErrorResult.Create("Not yet implemented for SolidWorks provider. See roadmap.");

    public Dictionary<string, object?> WorkPoint(string definition, string reference1, string reference2, string reference3)
        => ErrorResult.Create("Not yet implemented for SolidWorks provider. See roadmap.");

    // ── Assembly — stubs ───────────────────────────────────────────────────────────────────────

    public Dictionary<string, object?> AsmListComponents()
        => ErrorResult.Create("Not yet implemented for SolidWorks provider. See roadmap.");

    public Dictionary<string, object?> AsmListConstraints()
        => ErrorResult.Create("Not yet implemented for SolidWorks provider. See roadmap.");

    public Dictionary<string, object?> AsmPlaceComponent(string path, double x = 0, double y = 0, double z = 0)
        => ErrorResult.Create("Not yet implemented for SolidWorks provider. See roadmap.");

    public Dictionary<string, object?> AsmGroundComponent(string occurrence)
        => ErrorResult.Create("Not yet implemented for SolidWorks provider. See roadmap.");

    public Dictionary<string, object?> AsmReplaceComponent(string occurrence, string newPath)
        => ErrorResult.Create("Not yet implemented for SolidWorks provider. See roadmap.");

    public Dictionary<string, object?> AsmDeleteConstraint(string constraint)
        => ErrorResult.Create("Not yet implemented for SolidWorks provider. See roadmap.");

    public Dictionary<string, object?> AsmConstraintMate(string entityOne, string entityTwo, double offset = 0)
        => ErrorResult.Create("Not yet implemented for SolidWorks provider. See roadmap.");

    public Dictionary<string, object?> AsmConstraintFlush(string entityOne, string entityTwo, double offset = 0)
        => ErrorResult.Create("Not yet implemented for SolidWorks provider. See roadmap.");

    public Dictionary<string, object?> AsmConstraintAngle(string entityOne, string entityTwo, double angle, string solution = "directed")
        => ErrorResult.Create("Not yet implemented for SolidWorks provider. See roadmap.");

    public Dictionary<string, object?> AsmConstraintInsert(string entityOne, string entityTwo, double offset = 0)
        => ErrorResult.Create("Not yet implemented for SolidWorks provider. See roadmap.");

    public Dictionary<string, object?> AsmConstraintTangent(string entityOne, string entityTwo, double offset = 0)
        => ErrorResult.Create("Not yet implemented for SolidWorks provider. See roadmap.");

    public Dictionary<string, object?> AsmCircularPattern(string occurrence, string axis, int count, double angle = 360)
        => ErrorResult.Create("Not yet implemented for SolidWorks provider. See roadmap.");

    public Dictionary<string, object?> AsmRectangularPattern(string occurrence, string xAxis, int xCount, double xSpacing, string? yAxis = null, int yCount = 1, double ySpacing = 0)
        => ErrorResult.Create("Not yet implemented for SolidWorks provider. See roadmap.");

    public Dictionary<string, object?> AsmExtrudeCut(string profile, double distance, string direction = "positive")
        => ErrorResult.Create("Not yet implemented for SolidWorks provider. See roadmap.");

    public Dictionary<string, object?> AsmHole(double x, double y, double diameter, double depth, string type = "drilled")
        => ErrorResult.Create("Not yet implemented for SolidWorks provider. See roadmap.");

    public Dictionary<string, object?> AsmBom()
        => ErrorResult.Create("Not yet implemented for SolidWorks provider. See roadmap.");

    // ── Inspection (Phase 4.4 delegated to manager; basic loop viability) ──────────────────────

    public Dictionary<string, object?> CaptureViewportImage(string view = "Iso", int width = 1024, int height = 768, string format = "png")
        => _inspection.CaptureViewportImage(view, width, height, format);

    public Dictionary<string, object?> GetFeatureTree()
        => _inspection.GetFeatureTree();

    public Dictionary<string, object?> GetBoundingBox(string target = "")
        => _inspection.GetBoundingBox(target);
}
