using System.Reflection;
using McpCad.Core;
using McpCad.Core.Models;
using McpCad.Tools;
using ModelContextProtocol.Server;

namespace McpCad.Tests.Tools;

public class AtomicToolsRouterTests
{
    [Fact]
    public void AtomicTools_Constructor_Accepts_IProviderRouter()
    {
        var router = new FakeRouter("onshape", new Dictionary<string, ICadProvider> { ["onshape"] = new WebOnlyProvider() });
        var tools = new AtomicTools(router);
        Assert.NotNull(tools);
    }

    [Fact]
    public void Fillet_With_Onshape_CurrentProvider_Returns_NotSupported_Error()
    {
        var router = new FakeRouter("onshape", new Dictionary<string, ICadProvider> { ["onshape"] = new WebOnlyProvider() });
        var tools = new AtomicTools(router);

        var result = tools.fillet("1", 0.5);

        Assert.False((bool)result["success"]!);
        Assert.Equal("onshape does not support fillet operations", result["error"]?.ToString());
    }

    [Fact]
    public void Fillet_With_Inventor_CurrentProvider_Dispatches_To_Mechanical()
    {
        var mech = new RecordingMechanicalProvider();
        var router = new FakeRouter("inventor", new Dictionary<string, ICadProvider> { ["inventor"] = mech });
        var tools = new AtomicTools(router);

        var result = tools.fillet("edge-1", 2.5, "constant");

        Assert.True((bool)result["success"]!);
        Assert.True(mech.FilletCalled);
        Assert.Equal("edge-1", mech.LastFilletEdges);
    }

    [Fact]
    public void Same_Fillet_Routes_Differently_Per_PushProvider()
    {
        var mech = new RecordingMechanicalProvider();
        var web = new WebOnlyProvider();
        var router = new FakeRouter("inventor", new Dictionary<string, ICadProvider>
        {
            ["inventor"] = mech,
            ["onshape"] = web,
        });
        var tools = new AtomicTools(router);

        var ok = tools.fillet("e", 1.0);
        Assert.True((bool)ok["success"]!);

        using (router.PushProvider("onshape"))
        {
            var err = tools.fillet("e", 1.0);
            Assert.False((bool)err["success"]!);
        }

        var ok2 = tools.fillet("e", 1.0);
        Assert.True((bool)ok2["success"]!);
    }

    [Fact]
    public void Unified_Tool_Registration_Has_No_Duplicate_Mcp_Names()
    {
        var names = new List<string>();
        foreach (var t in new[] { typeof(AtomicTools), typeof(SkillTools), typeof(WebCadTools), typeof(SelectProviderTool) })
        {
            names.AddRange(ToolRegistrationValidator.GetToolMethodNames(t));
        }

        var dupes = names.GroupBy(n => n, StringComparer.Ordinal).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
        Assert.Empty(dupes);
    }

    [Fact]
    public void WebCadTools_ws_list_Available_On_Web_Provider()
    {
        var web = new WebOnlyProvider();
        var router = new FakeRouter("onshape", new Dictionary<string, ICadProvider> { ["onshape"] = web });
        var tools = new WebCadTools(router);

        var result = tools.ws_list("doc-1");

        Assert.True((bool)result["success"]!);
        Assert.True(web.WsListCalled);
    }

    [Fact]
    public void Connect_Unified_Name_Dispatches_To_Mechanical()
    {
        var mech = new RecordingMechanicalProvider();
        var router = new FakeRouter("inventor", new Dictionary<string, ICadProvider> { ["inventor"] = mech });
        var tools = new AtomicTools(router);

        var result = tools.connect();

        Assert.True((bool)result["success"]!);
        Assert.True(mech.ConnectCalled);
    }

    [Fact]
    public void AtomicTools_Has_No_ICadProvider_Or_Concrete_Provider_Fields()
    {
        var fields = typeof(AtomicTools).GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        foreach (var f in fields)
        {
            Assert.False(typeof(ICadProvider).IsAssignableFrom(f.FieldType));
            Assert.False(typeof(IMechanicalCadProvider).IsAssignableFrom(f.FieldType));
            Assert.False(typeof(ICadWebProvider).IsAssignableFrom(f.FieldType));
            Assert.Equal(typeof(IProviderRouter), f.FieldType);
        }

        var ctor = typeof(AtomicTools).GetConstructors().Single();
        var param = ctor.GetParameters().Single();
        Assert.Equal(typeof(IProviderRouter), param.ParameterType);
    }

    private sealed class FakeRouter : IProviderRouter
    {
        private readonly Dictionary<string, ICadProvider> _map;
        private string _current;

        public FakeRouter(string current, IReadOnlyDictionary<string, ICadProvider> map)
        {
            _current = current;
            _map = new Dictionary<string, ICadProvider>(map, StringComparer.OrdinalIgnoreCase);
        }

        public string CurrentProvider => _current;

        public IReadOnlyCollection<string> AvailableProviders => _map.Keys.ToList().AsReadOnly();

        public IDisposable PushProvider(string provider)
        {
            var prior = _current;
            _current = provider;
            return new Scope(() => _current = prior);
        }

        public ICadProvider Resolve(string provider)
        {
            if (_map.TryGetValue(provider, out var p))
                return p;
            throw new InvalidOperationException($"Unknown provider {provider}");
        }

        private sealed class Scope(Action onDispose) : IDisposable
        {
            public void Dispose() => onDispose();
        }
    }

    private sealed class WebOnlyProvider : ICadWebProvider
    {
        public bool WsListCalled { get; private set; }

        public Dictionary<string, object?> Connect() => Ok();
        public Dictionary<string, object?> Disconnect() => Ok();
        public Dictionary<string, object?> Health() => Ok();
        public Dictionary<string, object?> DocOpen(string path) => Ok();
        public Dictionary<string, object?> DocNewPart(string template = "") => Ok();
        public Dictionary<string, object?> DocNewAssembly(string template = "") => Ok();
        public Dictionary<string, object?> DocSave() => Ok();
        public Dictionary<string, object?> DocSaveAs(string path) => Ok();
        public Dictionary<string, object?> DocClose(bool save = true) => Ok();
        public Dictionary<string, object?> ExportStep(string path, Dictionary<string, object?>? options = null) => Ok();
        public Dictionary<string, object?> ExportStl(string path, Dictionary<string, object?>? options = null) => Ok();
        public Dictionary<string, object?> ExportPdf(string path, Dictionary<string, object?>? options = null) => Ok();
        public Dictionary<string, object?> ExportDxf(string path, Dictionary<string, object?>? options = null) => Ok();
        public Dictionary<string, object?> Authenticate(string accessKey, string secretKeyId, string secretKey) => Ok();
        public Dictionary<string, object?> WsList(string? documentId = null)
        {
            WsListCalled = true;
            return Ok();
        }

        public Dictionary<string, object?> WsCurrent(string documentId) => Ok();
        public Dictionary<string, object?> ElementList(string documentId, string wvm, string wvmId) => Ok();
        public Dictionary<string, object?> ElementGet(CadElementAddress address) => Ok();
        public Dictionary<string, object?> ElementCreate(CadElementAddress address, string name, string elementType) => Ok();
        public Dictionary<string, object?> ElementRename(CadElementAddress address, string name) => Ok();
        public Dictionary<string, object?> DocCopy(string documentId, string? name = null) => Ok();
        public Dictionary<string, object?> DocDelete(string documentId, bool confirm) => Ok();
        public Dictionary<string, object?> VersionCreate(string documentId, string? name = null) => Ok();
        public Dictionary<string, object?> MicroversionGet(CadElementAddress address) => Ok();
        public Dictionary<string, object?> ExportStep(CadElementAddress address, Dictionary<string, object?>? options = null) => Ok();
        public Dictionary<string, object?> ExportStl(CadElementAddress address, Dictionary<string, object?>? options = null) => Ok();
        public Dictionary<string, object?> SketchReplace(CadElementAddress address) => Ok();
        public Dictionary<string, object?> FeatureAdd(CadElementAddress partStudio, string featureType, IReadOnlyList<Dictionary<string, object?>>? parameters = null) => Ok();
        public Dictionary<string, object?> AssemblyListInstances(CadElementAddress assemblyElement) => Ok();
        public Dictionary<string, object?> AssemblyCreateInstance(CadElementAddress assemblyElement, string partDocumentId, string partElementId) => Ok();
        public Dictionary<string, object?> AssemblyDeleteInstance(CadElementAddress assemblyElement, string instanceId) => Ok();

        private static Dictionary<string, object?> Ok() => new() { ["success"] = true };
    }

    private sealed class RecordingMechanicalProvider : IMechanicalCadProvider
    {
        public bool FilletCalled { get; private set; }
        public string? LastFilletEdges { get; private set; }
        public bool ConnectCalled { get; private set; }

        public Dictionary<string, object?> Connect()
        {
            ConnectCalled = true;
            return new() { ["success"] = true };
        }

        public Dictionary<string, object?> Fillet(string edges, double radius, string mode = "constant")
        {
            FilletCalled = true;
            LastFilletEdges = edges;
            return new() { ["success"] = true, ["feature"] = "Fillet1" };
        }

        public Dictionary<string, object?> Disconnect() => new() { ["success"] = true };
        public Dictionary<string, object?> Health() => new() { ["success"] = true };
        public Dictionary<string, object?> DocOpen(string path) => new() { ["success"] = true };
        public Dictionary<string, object?> DocNewPart(string template = "") => new() { ["success"] = true };
        public Dictionary<string, object?> DocNewAssembly(string template = "") => new() { ["success"] = true };
        public Dictionary<string, object?> DocSave() => new() { ["success"] = true };
        public Dictionary<string, object?> DocSaveAs(string path) => new() { ["success"] = true };
        public Dictionary<string, object?> DocClose(bool save = true) => new() { ["success"] = true };
        public Dictionary<string, object?> ExportStep(string path, Dictionary<string, object?>? options = null) => new() { ["success"] = true };
        public Dictionary<string, object?> ExportStl(string path, Dictionary<string, object?>? options = null) => new() { ["success"] = true };
        public Dictionary<string, object?> ExportPdf(string path, Dictionary<string, object?>? options = null) => new() { ["success"] = true };
        public Dictionary<string, object?> ExportDxf(string path, Dictionary<string, object?>? options = null) => new() { ["success"] = true };
        public Dictionary<string, object?> SketchCreate(string plane = "XY") => new() { ["success"] = true };
        public Dictionary<string, object?> SketchLine(double x1, double y1, double x2, double y2, string? tag = null, bool connect = false) => new() { ["success"] = true };
        public Dictionary<string, object?> SketchLineClose() => new() { ["success"] = true };
        public Dictionary<string, object?> SketchCircle(double cx, double cy, double radius, string? tag = null) => new() { ["success"] = true };
        public Dictionary<string, object?> SketchArc(double cx, double cy, double radius, double startAngle, double endAngle) => new() { ["success"] = true };
        public Dictionary<string, object?> SketchRectangle(double x1, double y1, double x2, double y2) => new() { ["success"] = true };
        public Dictionary<string, object?> SketchDimension(string mode, string entity1, string entity2 = "", double? value = null, string orientation = "aligned", double? positionX = null, double? positionY = null) => new() { ["success"] = true };
        public Dictionary<string, object?> SketchPoint(double x, double y) => new() { ["success"] = true };
        public Dictionary<string, object?> SketchSpline(string points, string fitMethod = "sweet") => new() { ["success"] = true };
        public Dictionary<string, object?> SketchEllipse(double cx, double cy, double majorRadius, double minorRadius, double majorAxisAngle = 0.0) => new() { ["success"] = true };
        public Dictionary<string, object?> SketchCircularPattern(string entities, string axis, int count, double angle = 360.0, bool fitted = true, bool symmetric = false) => new() { ["success"] = true };
        public Dictionary<string, object?> SketchRectangularPattern(string entities, string xAxis, int xCount, double xSpacing, string yAxis = "", int yCount = 1, double ySpacing = 0.0) => new() { ["success"] = true };
        public Dictionary<string, object?> SketchOffset(string entities, double offsetX, double offsetY, bool includeConnected = false) => new() { ["success"] = true };
        public Dictionary<string, object?> SketchMove(string entities, double dx, double dy, bool copy = false) => new() { ["success"] = true };
        public Dictionary<string, object?> SketchRotate(string entities, double cx, double cy, double angle, bool copy = false) => new() { ["success"] = true };
        public Dictionary<string, object?> SketchDelete() => new() { ["success"] = true };
        public Dictionary<string, object?> SketchConstraint(string mode, string entity1, string entity2 = "", string symLine = "", string axis = "") => new() { ["success"] = true };
        public Dictionary<string, object?> SketchTrim(string entity, string cuttingEntity, string side = "keep") => new() { ["success"] = true };
        public Dictionary<string, object?> SketchScale(string entities, double cx, double cy, double factor) => new() { ["success"] = true };
        public Dictionary<string, object?> SketchMirror(string entities, string mirrorEntity) => new() { ["success"] = true };
        public Dictionary<string, object?> SketchProfiles() => new() { ["success"] = true };
        public Dictionary<string, object?> Extrude(string profile, double distance, string direction = "positive", double taper = 0.0, string operation = "join") => new() { ["success"] = true };
        public Dictionary<string, object?> Revolve(string profile, string axis, double angle = 360.0, string direction = "positive", string operation = "join") => new() { ["success"] = true };
        public Dictionary<string, object?> Sweep(string profile, string path, string sweepType = "path", string operation = "join", double taper = 0.0, string pathSketch = "last", string profileSketch = "last") => new() { ["success"] = true };
        public Dictionary<string, object?> Chamfer(string edges, double distance, string mode = "equal") => new() { ["success"] = true };
        public Dictionary<string, object?> CircularPattern(string profile, string axis, int count, double angle = 360.0, bool fitWithinAngle = true, bool naturalDirection = true) => new() { ["success"] = true };
        public Dictionary<string, object?> MirrorFeature(string profile, string mirrorPlane) => new() { ["success"] = true };
        public Dictionary<string, object?> RectangularPattern(string profile, string xAxis, int xCount, double xSpacing, string yAxis = "", int yCount = 1, double ySpacing = 0.0) => new() { ["success"] = true };
        public Dictionary<string, object?> Loft(string profiles, string operation = "join") => new() { ["success"] = true };
        public Dictionary<string, object?> Coil(string profile, string axis, double pitch, double revolutions, string operation = "join") => new() { ["success"] = true };
        public Dictionary<string, object?> Rib(string profile, double thickness, string direction = "normal", string operation = "join") => new() { ["success"] = true };
        public Dictionary<string, object?> Emboss(string profile, double depth, string type = "emboss") => new() { ["success"] = true };
        public Dictionary<string, object?> Derive(string sourcePath) => new() { ["success"] = true };
        public Dictionary<string, object?> Hole(double x, double y, double diameter, double depth, string type = "drilled", string operation = "cut") => new() { ["success"] = true };
        public Dictionary<string, object?> Thread(string face, string specification, string direction = "right") => new() { ["success"] = true };
        public Dictionary<string, object?> InspectEdges() => new() { ["success"] = true };
        public Dictionary<string, object?> Shell(string faces, double thickness, string direction = "inside", string operation = "cut") => new() { ["success"] = true };
        public Dictionary<string, object?> Thicken(string faces, double thickness, string direction = "inside", string operation = "join") => new() { ["success"] = true };
        public Dictionary<string, object?> Combine(string baseBody, string toolBodies, string operation = "cut", bool keepToolBodies = false) => new() { ["success"] = true };
        public Dictionary<string, object?> Split(string splitTool, string removeSide = "none", string targetBody = "auto") => new() { ["success"] = true };
        public Dictionary<string, object?> Draft(string faces, double angle, string mode = "fixed_plane", string pullDirection = "normal", string fixedEntity = "") => new() { ["success"] = true };
        public Dictionary<string, object?> ParamList(string filterPattern = "") => new() { ["success"] = true };
        public Dictionary<string, object?> ParamGet(string name) => new() { ["success"] = true };
        public Dictionary<string, object?> ParamSet(string name, double value) => new() { ["success"] = true };
        public Dictionary<string, object?> ParamSetExpression(string name, string expression) => new() { ["success"] = true };
        public Dictionary<string, object?> IPropertyGet(string name, string propertySet = "Inventor Summary Information") => new() { ["success"] = true };
        public Dictionary<string, object?> IPropertySet(string name, string value, string propertySet = "Inventor Summary Information") => new() { ["success"] = true };
        public Dictionary<string, object?> IPropertySummary() => new() { ["success"] = true };
        public Dictionary<string, object?> IPropertyCustomGet(string name) => new() { ["success"] = true };
        public Dictionary<string, object?> IPropertyCustomSet(string name, string value) => new() { ["success"] = true };
        public Dictionary<string, object?> WorkPlane(string definition, string reference1 = "", string reference2 = "", double offset = 0.0) => new() { ["success"] = true };
        public Dictionary<string, object?> WorkAxis(string definition, string reference1 = "", string reference2 = "") => new() { ["success"] = true };
        public Dictionary<string, object?> WorkPoint(string definition, string reference1 = "", string reference2 = "", string reference3 = "") => new() { ["success"] = true };
        public Dictionary<string, object?> AsmListComponents() => new() { ["success"] = true };
        public Dictionary<string, object?> AsmListConstraints() => new() { ["success"] = true };
        public Dictionary<string, object?> AsmPlaceComponent(string path, double x = 0, double y = 0, double z = 0) => new() { ["success"] = true };
        public Dictionary<string, object?> AsmGroundComponent(string occurrence) => new() { ["success"] = true };
        public Dictionary<string, object?> AsmReplaceComponent(string occurrence, string newPath) => new() { ["success"] = true };
        public Dictionary<string, object?> AsmDeleteConstraint(string constraint) => new() { ["success"] = true };
        public Dictionary<string, object?> AsmConstraintMate(string entityOne, string entityTwo, double offset = 0) => new() { ["success"] = true };
        public Dictionary<string, object?> AsmConstraintFlush(string entityOne, string entityTwo, double offset = 0) => new() { ["success"] = true };
        public Dictionary<string, object?> AsmConstraintAngle(string entityOne, string entityTwo, double angle, string solution = "auto") => new() { ["success"] = true };
        public Dictionary<string, object?> AsmConstraintInsert(string entityOne, string entityTwo, double offset = 0) => new() { ["success"] = true };
        public Dictionary<string, object?> AsmConstraintTangent(string entityOne, string entityTwo, double offset = 0) => new() { ["success"] = true };
        public Dictionary<string, object?> AsmCircularPattern(string occurrence, string axis, int count, double angle = 360.0) => new() { ["success"] = true };
        public Dictionary<string, object?> AsmRectangularPattern(string occurrence, string xAxis, int xCount, double xSpacing, string yAxis = "", int yCount = 1, double ySpacing = 0.0) => new() { ["success"] = true };
        public Dictionary<string, object?> AsmExtrudeCut(string profile, double distance, string direction = "positive") => new() { ["success"] = true };
        public Dictionary<string, object?> AsmHole(double x, double y, double diameter, double depth, string type = "drilled") => new() { ["success"] = true };
        public Dictionary<string, object?> AsmBom() => new() { ["success"] = true };
    }
}