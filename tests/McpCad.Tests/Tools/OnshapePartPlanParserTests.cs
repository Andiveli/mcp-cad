using System.Reflection;
using McpCad.Tools.Onshape;

namespace McpCad.Tests.Tools;

public class OnshapePartPlanParserTests
{
    private static readonly Assembly Tools = typeof(OnshapeSkillTools).Assembly;
    private const string Address = """{"documentId":"doc","workspaceOrVersionId":"ws","elementId":"el"}""";
    private const string Plan = """{"schemaVersion":1,"stages":[{"id":"sketch-1","kind":"sketch","plane":"Top","profile":{"type":"rectangle","x1_cm":0,"y1_cm":0,"x2_cm":2,"y2_cm":3}}]}""";

    [Fact]
    public void Parse_AcceptsExactTwoStringRootsAndProducesTypedAddressAndPlan()
    {
        var parsed = Parse(Address, Plan);

        Item(parsed, "Item1").GetType().Name.Should().Be("CadElementAddress");
        Item(parsed, "Item2").GetType().Name.Should().Be("OnshapePartPlan");
    }

    [Theory]
    [InlineData("{\"documentId\":\"doc\",\"workspaceOrVersionId\":\"ws\",\"elementId\":\"el\",\"extra\":1}", Plan, "address.extra")]
    [InlineData(Address, "{\"schemaVersion\":1,\"stages\":[],\"extra\":1}", "plan.extra")]
    [InlineData(Address, "{\"schemaVersion\":1,\"stages\":[{\"id\":\"s\",\"kind\":\"sketch\",\"plane\":\"Top\",\"profile\":{\"type\":\"circle\",\"cx_cm\":0,\"cy_cm\":0,\"radius_cm\":1,\"extra\":0}}]}", "stages[0].profile.extra")]
    [InlineData(Address, "{\"schemaVersion\":1,\"stages\":[{\"id\":\"s\",\"kind\":\"sketch\",\"plane\":\"Top\",\"profile\":{\"type\":\"polygon\",\"points\":[{\"x_cm\":0,\"y_cm\":0,\"z\":0}]}}]}", "stages[0].profile.points[0].z")]
    [InlineData(Address, "{\"schemaVersion\":1,\"stages\":[{\"id\":\"s\",\"kind\":\"sketch\",\"plane\":\"Top\",\"profile\":{\"type\":\"mixed\",\"start\":{\"x_cm\":0,\"y_cm\":0},\"segments\":[{\"type\":\"line\",\"end\":{\"x_cm\":1,\"y_cm\":0},\"start\":{\"x_cm\":0,\"y_cm\":0}}]}}]}", "stages[0].profile.segments[0].start")]
    public void Parse_RejectsUnknownMembersAtEveryObjectDepth(string address, string plan, string path) =>
        Reject(address, plan).Should().Contain(path);

    [Theory]
    [InlineData("{\"documentId\":\"doc\",\"documentId\":\"again\",\"workspaceOrVersionId\":\"ws\",\"elementId\":\"el\"}", Plan, "address.documentId")]
    [InlineData(Address, "{\"schemaVersion\":1,\"schemaVersion\":1,\"stages\":[]}", "plan.schemaVersion")]
    [InlineData(Address, "{\"schemaVersion\":1,\"stages\":[{\"id\":\"s\",\"id\":\"again\",\"kind\":\"sketch\",\"plane\":\"Top\",\"profile\":{\"type\":\"rectangle\",\"x1_cm\":0,\"y1_cm\":0,\"x2_cm\":1,\"y2_cm\":1}}]}", "stages[0].id")]
    [InlineData(Address, "{\"schemaVersion\":1,\"stages\":[{\"id\":\"s\",\"kind\":\"sketch\",\"plane\":\"Top\",\"profile\":{\"type\":\"polygon\",\"points\":[{\"x_cm\":0,\"x_cm\":1,\"y_cm\":0}]}}]}", "stages[0].profile.points[0].x_cm")]
    [InlineData(Address, "{\"schemaVersion\":1,\"stages\":[{\"id\":\"s\",\"kind\":\"sketch\",\"plane\":\"Top\",\"profile\":{\"type\":\"mixed\",\"start\":{\"x_cm\":0,\"y_cm\":0},\"segments\":[{\"type\":\"line\",\"end\":{\"x_cm\":1,\"y_cm\":0},\"end\":{\"x_cm\":0,\"y_cm\":0}}]}}]}", "stages[0].profile.segments[0].end")]
    public void Parse_RejectsDuplicatesAtEveryDepth(string address, string plan, string path) =>
        Reject(address, plan).Should().Contain(path);

    [Theory]
    [InlineData("{}", Plan, "address.documentId")]
    [InlineData(Address, "{}", "plan.schemaVersion")]
    [InlineData(Address, "{\"schemaVersion\":1,\"stages\":[{\"id\":\"s\",\"kind\":\"extrude\"}]}", "stages[0].sketchId")]
    [InlineData(Address, "{\"schemaVersion\":1,\"stages\":[{\"id\":\"s\",\"kind\":\"sketch\",\"plane\":\"Top\",\"profile\":{\"type\":\"circle\",\"cx_cm\":0,\"cy_cm\":0}}]}", "stages[0].profile.radius_cm")]
    public void Parse_RejectsMissingRequiredMembers(string address, string plan, string path) =>
        Reject(address, plan).Should().Contain(path);

    [Theory]
    [InlineData("{\"documentId\":null,\"workspaceOrVersionId\":\"ws\",\"elementId\":\"el\"}", Plan, "address.documentId")]
    [InlineData(Address, "{\"schemaVersion\":\"1\",\"stages\":[]}", "plan.schemaVersion")]
    [InlineData(Address, "{\"schemaVersion\":1.0,\"stages\":[]}", "plan.schemaVersion")]
    [InlineData(Address, "{\"schemaVersion\":1,\"stages\":{}}", "plan.stages")]
    [InlineData(Address, "{\"schemaVersion\":1,\"stages\":[{\"id\":\"s\",\"kind\":\"sketch\",\"plane\":\"top\",\"profile\":{\"type\":\"circle\",\"cx_cm\":\"0\",\"cy_cm\":0,\"radius_cm\":null}}]}", "stages[0].plane")]
    [InlineData(Address, "{\"schemaVersion\":1,\"stages\":[{\"id\":\"s\",\"kind\":\"Sketch\",\"plane\":\"Top\",\"profile\":{\"type\":\"circle\",\"cx_cm\":0,\"cy_cm\":0,\"radius_cm\":1}}]}", "stages[0].kind")]
    public void Parse_RejectsWrongKindsNumericCoercionNullsAndCaseAliases(string address, string plan, string path) =>
        Reject(address, plan).Should().Contain(path);

    [Fact]
    public void Parse_AcceptsOptionalMicroversionAndDerivedMixedSegmentsButRejectsLateMalformedInput()
    {
        const string mixed = """{"schemaVersion":1,"stages":[{"id":"s","kind":"sketch","plane":"Right","profile":{"type":"mixed","start":{"x_cm":0,"y_cm":0},"segments":[{"type":"line","end":{"x_cm":1,"y_cm":0}},{"type":"arc","through":{"x_cm":1,"y_cm":1},"end":{"x_cm":0,"y_cm":0}}]}},{"id":"bad","kind":"checkpoint","afterStageId":"s","view":"ISO"}]}""";
        var address = """{"documentId":"doc","workspaceOrVersionId":"ws","microversionId":"micro","elementId":"el"}""";

        Reject(address, mixed).Should().Contain("stages[1].view");
    }

    [Theory]
    [InlineData("{\"documentId\":\" \",\"workspaceOrVersionId\":\"ws\",\"elementId\":\"el\"}", Plan, "address.documentId")]
    [InlineData(Address, "{\"schemaVersion\":1,\"stages\":[{\"id\":\"\",\"kind\":\"checkpoint\",\"afterStageId\":\"s\",\"view\":\"iso\"}]}", "stages[0].id")]
    [InlineData(Address, "{\"schemaVersion\":1,", "plan")]
    public void Parse_RejectsWhitespaceAndMalformedJson(string address, string plan, string path) =>
        Reject(address, plan).Should().Contain(path);

    [Fact]
    public void Parse_PreservesRepeatedPolygonClosingPointForValidatorNormalization() =>
        Parse(Address, """{"schemaVersion":1,"stages":[{"id":"s","kind":"sketch","plane":"Top","profile":{"type":"polygon","points":[{"x_cm":0,"y_cm":0},{"x_cm":1,"y_cm":0},{"x_cm":0,"y_cm":1},{"x_cm":0,"y_cm":0}]}}]}""")
            .Should().NotBeNull();

    [Fact]
    public void Parse_AcceptsEveryClosedFormAndPreservesOrderedStages()
    {
        const string plan = """{"schemaVersion":1,"stages":[{"id":"rectangle","kind":"sketch","plane":"Top","profile":{"type":"rectangle","x1_cm":0,"y1_cm":0,"x2_cm":1,"y2_cm":1}},{"id":"circle","kind":"sketch","plane":"Front","profile":{"type":"circle","cx_cm":0,"cy_cm":0,"radius_cm":1}},{"id":"polygon","kind":"sketch","plane":"Right","profile":{"type":"polygon","points":[{"x_cm":0,"y_cm":0},{"x_cm":1,"y_cm":0},{"x_cm":0,"y_cm":1}]}},{"id":"mixed","kind":"sketch","plane":"Top","profile":{"type":"mixed","start":{"x_cm":0,"y_cm":0},"segments":[{"type":"line","end":{"x_cm":1,"y_cm":0}},{"type":"arc","through":{"x_cm":1,"y_cm":1},"end":{"x_cm":0,"y_cm":0}}]}},{"id":"extrude","kind":"extrude","sketchId":"rectangle","operation":"remove","depth_cm":2,"direction":"negative"},{"id":"checkpoint","kind":"checkpoint","afterStageId":"extrude","view":"top"}]}""";
        var parsedPlan = Item(Parse(Address, plan), "Item2");
        var stages = (System.Collections.IEnumerable)parsedPlan.GetType().GetProperty("Stages")!.GetValue(parsedPlan)!;

        stages.Cast<object>().Select(stage => stage.GetType().Name).Should().Equal("SketchPlanStage", "SketchPlanStage", "SketchPlanStage", "SketchPlanStage", "ExtrudePlanStage", "CheckpointPlanStage");
    }

    [Fact]
    public void Parse_RejectsAliasesAndNonFiniteJsonNumbers()
    {
        var cases = new[]
        {
            (Plan.Replace("\"Top\"", "\"top\""), "stages[0].plane"),
            (Plan.Replace("\"sketch\"", "\"Sketch\""), "stages[0].kind"),
            (Plan.Replace("\"rectangle\"", "\"Rectangle\""), "stages[0].profile.type"),
            (Extrude("join", "positive"), "stages[0].operation"),
            (Extrude("cut", "positive"), "stages[0].operation"),
            (Extrude("intersect", "positive"), "stages[0].operation"),
            (Extrude("new", "Positive"), "stages[0].direction"),
            (Checkpoint("ISO"), "stages[0].view"),
            (Plan.Replace("\"x1_cm\":0", "\"x1_cm\":\"0\""), "stages[0].profile.x1_cm"),
            (Plan.Replace("\"x1_cm\":0", "\"x1_cm\":null"), "stages[0].profile.x1_cm"),
            (Plan.Replace("\"x1_cm\":0", "\"x1_cm\":NaN"), "plan"),
            ("""{"schemaVersion":1,"stages":[{"id":"s","kind":"sketch","plane":"Top","profile":{"type":"mixed","start":{"x_cm":0,"y_cm":0},"segments":[{"type":"line","end":{"x_cm":1,"y_cm":0},"mid":{"x_cm":0,"y_cm":0}}]}}]}""", "stages[0].profile.segments[0].mid"),
        };

        foreach (var (plan, path) in cases) Reject(Address, plan).Should().Contain(path);
    }

    private static string Extrude(string operation, string direction) => $"{{\"schemaVersion\":1,\"stages\":[{{\"id\":\"e\",\"kind\":\"extrude\",\"sketchId\":\"s\",\"operation\":\"{operation}\",\"depth_cm\":1,\"direction\":\"{direction}\"}}]}}";
    private static string Checkpoint(string view) => $"{{\"schemaVersion\":1,\"stages\":[{{\"id\":\"c\",\"kind\":\"checkpoint\",\"afterStageId\":\"s\",\"view\":\"{view}\"}}]}}";

    private static object Parse(string address, string plan) => Method().Invoke(null, [address, plan])!;

    private static string Reject(string address, string plan)
    {
        var action = () => Parse(address, plan);
        return action.Should().Throw<TargetInvocationException>().Which.InnerException!.Message;
    }

    private static MethodInfo Method() => Tools.GetType("McpCad.Tools.Onshape.Orchestration.OnshapePartPlanParser", true)!
        .GetMethod("Parse", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)!;

    private static object Item(object tuple, string name) => tuple.GetType().GetField(name)!.GetValue(tuple)!;
}
