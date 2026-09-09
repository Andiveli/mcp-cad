using System.Collections;
using System.Reflection;
using System.Text.Json;
using McpCad.Tools.Onshape;

namespace McpCad.Tests.Tools;

public class OnshapePartPlanValidatorTests
{
    private static readonly Assembly Tools = typeof(OnshapeSkillTools).Assembly;
    private const string Address = """{"documentId":"doc","workspaceOrVersionId":"ws","elementId":"el"}""";

    [Fact]
    public void Validate_CompilesEverySupportedProfileOnCanonicalPlanesWithOneCentimeterConversion()
    {
        var plan = Plan(
            Sketch("rectangle", "Top", "{\"type\":\"rectangle\",\"x1_cm\":0,\"y1_cm\":0,\"x2_cm\":2,\"y2_cm\":3}"),
            Sketch("circle", "Front", "{\"type\":\"circle\",\"cx_cm\":1,\"cy_cm\":2,\"radius_cm\":3}"),
            Sketch("polygon", "Right", "{\"type\":\"polygon\",\"points\":[{\"x_cm\":0,\"y_cm\":0},{\"x_cm\":2,\"y_cm\":0},{\"x_cm\":0,\"y_cm\":2},{\"x_cm\":0,\"y_cm\":0}]}"),
            Sketch("mixed", "Top", Mixed(1)));

        var validated = Validate(plan);
        var stages = (IEnumerable)validated.GetType().GetProperty("Stages")!.GetValue(validated)!;
        var payloads = stages.Cast<object>().Select(stage => (string)stage.GetType().GetProperty("SketchRequestJson")!.GetValue(stage)!).ToArray();

        payloads.Should().HaveCount(4);
        using var rectangle = JsonDocument.Parse(payloads[0]);
        rectangle.RootElement.GetProperty("feature").GetProperty("entities")[0].GetProperty("geometry").GetProperty("pntX").GetDouble().Should().Be(0.01);
        using var circle = JsonDocument.Parse(payloads[1]);
        circle.RootElement.GetProperty("feature").GetProperty("entities")[0].GetProperty("geometry").GetProperty("radius").GetDouble().Should().Be(0.03);
    }

    [Theory]
    [MemberData(nameof(InvalidPlans))]
    public void Validate_RejectsStaticLimitsReferencesAndGeometryBeforeExecution(string plan, string code) =>
        Reject(plan).Should().Contain(code);

    [Fact]
    public void Validate_RejectsUnsupportedSchemaVersionEvenForAnOtherwiseParsedPlan()
    {
        var parsed = Parser().Invoke(null, [Address, Plan(Sketch("s", "Top", "{\"type\":\"circle\",\"cx_cm\":0,\"cy_cm\":0,\"radius_cm\":1}"))])!;
        var address = Item(parsed, "Item1");
        var plan = Item(parsed, "Item2");
        var unsupported = Activator.CreateInstance(plan.GetType(), 2, plan.GetType().GetProperty("Stages")!.GetValue(plan))!;
        var action = () => Validator().GetMethod("Validate", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)!.Invoke(null, [address, unsupported]);

        action.Should().Throw<TargetInvocationException>().Which.InnerException!.Message.Should().Contain("UNSUPPORTED_SCHEMA_VERSION");
    }

    [Fact]
    public void Validate_AcceptsOneAndEightArcMixedProfilesButRejectsNine()
    {
        Validate(Plan(Sketch("one", "Top", Mixed(1)))).Should().NotBeNull();
        Validate(Plan(Sketch("eight", "Top", Mixed(8)))).Should().NotBeNull();

        Reject(Plan(Sketch("nine", "Top", Mixed(9)))).Should().Contain("INVALID_GEOMETRY");
    }

    [Fact]
    public void Validate_PreservesCentimeterDepthForTheSingleExecutorConversionAndExposesSingleBodyAssumption()
    {
        var validated = Validate(Plan(
            Sketch("s", "Top", "{\"type\":\"circle\",\"cx_cm\":0,\"cy_cm\":0,\"radius_cm\":1}"),
            "{\"id\":\"e\",\"kind\":\"extrude\",\"sketchId\":\"s\",\"operation\":\"add\",\"depth_cm\":2,\"direction\":\"positive\"}"));
        var stages = ((IEnumerable)validated.GetType().GetProperty("Stages")!.GetValue(validated)!).Cast<object>().ToArray();

        ((double)stages[1].GetType().GetProperty("DepthCm")!.GetValue(stages[1])!).Should().Be(2d);
        var assumption = (string)Validator().GetField("SingleBodyResultAssumption", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)!.GetRawConstantValue()!;
        assumption.Should().Contain("single-body-result assumption");
    }

    public static IEnumerable<object[]> InvalidPlans()
    {
        yield return [Plan(), "PLAN_LIMIT_EXCEEDED"];
        yield return [Plan(Enumerable.Range(0, 17).Select(index => Sketch($"s{index}", "Top", "{\"type\":\"circle\",\"cx_cm\":0,\"cy_cm\":0,\"radius_cm\":1}")).ToArray()), "PLAN_LIMIT_EXCEEDED"];
        yield return [Plan(Sketch("s", "Top", "{\"type\":\"circle\",\"cx_cm\":0,\"cy_cm\":0,\"radius_cm\":1}"), Checkpoint("c1", "s"), Checkpoint("c2", "s"), Checkpoint("c3", "s"), Checkpoint("c4", "s")), "PLAN_LIMIT_EXCEEDED"];
        yield return [Plan(Sketch("s", "Top", "{\"type\":\"circle\",\"cx_cm\":0,\"cy_cm\":0,\"radius_cm\":1}"), Sketch("s", "Front", "{\"type\":\"circle\",\"cx_cm\":0,\"cy_cm\":0,\"radius_cm\":1}")), "INVALID_REFERENCE"];
        yield return [Plan("{\"id\":\"e\",\"kind\":\"extrude\",\"sketchId\":\"s\",\"operation\":\"new\",\"depth_cm\":1,\"direction\":\"positive\"}", Sketch("s", "Top", "{\"type\":\"circle\",\"cx_cm\":0,\"cy_cm\":0,\"radius_cm\":1}")), "INVALID_REFERENCE"];
        yield return [Plan(Sketch("s", "Top", "{\"type\":\"circle\",\"cx_cm\":0,\"cy_cm\":0,\"radius_cm\":1}"), "{\"id\":\"e\",\"kind\":\"extrude\",\"sketchId\":\"s\",\"operation\":\"new\",\"depth_cm\":1,\"direction\":\"positive\"}", "{\"id\":\"e2\",\"kind\":\"extrude\",\"sketchId\":\"e\",\"operation\":\"new\",\"depth_cm\":1,\"direction\":\"positive\"}"), "INVALID_REFERENCE"];
        yield return [Plan(Sketch("s", "Top", "{\"type\":\"circle\",\"cx_cm\":0,\"cy_cm\":0,\"radius_cm\":1}"), "{\"id\":\"e\",\"kind\":\"extrude\",\"sketchId\":\"s\",\"operation\":\"new\",\"depth_cm\":1,\"direction\":\"positive\"}", Checkpoint("c", "s")), "INVALID_REFERENCE"];
        yield return [Plan(Sketch("r", "Top", "{\"type\":\"rectangle\",\"x1_cm\":0,\"y1_cm\":0,\"x2_cm\":0,\"y2_cm\":1}")), "INVALID_GEOMETRY"];
        yield return [Plan(Sketch("c", "Top", "{\"type\":\"circle\",\"cx_cm\":0,\"cy_cm\":0,\"radius_cm\":0}")), "INVALID_GEOMETRY"];
        yield return [Plan(Sketch("p", "Top", "{\"type\":\"polygon\",\"points\":[{\"x_cm\":0,\"y_cm\":0},{\"x_cm\":1,\"y_cm\":0}]}")), "INVALID_GEOMETRY"];
        yield return [Plan(Sketch("e", "Top", "{\"type\":\"circle\",\"cx_cm\":0,\"cy_cm\":0,\"radius_cm\":1}"), "{\"id\":\"x\",\"kind\":\"extrude\",\"sketchId\":\"e\",\"operation\":\"new\",\"depth_cm\":0,\"direction\":\"positive\"}"), "INVALID_GEOMETRY"];
    }

    private static object Validate(string plan)
    {
        var parsed = Parser().Invoke(null, [Address, plan])!;
        return Validator().GetMethod("Validate", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)!
            .Invoke(null, [Item(parsed, "Item1"), Item(parsed, "Item2")])!;
    }

    private static string Reject(string plan)
    {
        var action = () => Validate(plan);
        return action.Should().Throw<TargetInvocationException>().Which.InnerException!.Message;
    }

    private static string Plan(params string[] stages) => "{\"schemaVersion\":1,\"stages\":[" + string.Join(',', stages) + "]}";
    private static string Sketch(string id, string plane, string profile) => "{\"id\":\"" + id + "\",\"kind\":\"sketch\",\"plane\":\"" + plane + "\",\"profile\":" + profile + "}";
    private static string Checkpoint(string id, string target) => "{\"id\":\"" + id + "\",\"kind\":\"checkpoint\",\"afterStageId\":\"" + target + "\",\"view\":\"iso\"}";

    private static string Mixed(int arcs)
    {
        if (arcs == 8) return EightArcRoundedRectangle;
        var segments = new List<string> { "{\"type\":\"line\",\"end\":{\"x_cm\":10,\"y_cm\":0}}" };
        for (var index = 1; index <= arcs; index++)
        {
            var through = Math.PI * (index - 0.5) / arcs;
            var end = Math.PI * index / arcs;
            segments.Add(FormattableString.Invariant($"{{\"type\":\"arc\",\"through\":{{\"x_cm\":{5 + 5 * Math.Cos(through):F9},\"y_cm\":{5 * Math.Sin(through):F9}}},\"end\":{{\"x_cm\":{5 + 5 * Math.Cos(end):F9},\"y_cm\":{5 * Math.Sin(end):F9}}}}}"));
        }
        return "{\"type\":\"mixed\",\"start\":{\"x_cm\":0,\"y_cm\":0},\"segments\":[" + string.Join(',', segments) + "]}";
    }

    private const string EightArcRoundedRectangle = """{"type":"mixed","start":{"x_cm":4.5,"y_cm":0},"segments":[{"type":"line","end":{"x_cm":5.5,"y_cm":0}},{"type":"arc","through":{"x_cm":5.85,"y_cm":0.15},"end":{"x_cm":6.146447,"y_cm":0.146447}},{"type":"line","end":{"x_cm":9.853553,"y_cm":3.853553}},{"type":"arc","through":{"x_cm":9.95,"y_cm":4.05},"end":{"x_cm":10,"y_cm":4.5}},{"type":"line","end":{"x_cm":10,"y_cm":5.5}},{"type":"arc","through":{"x_cm":9.95,"y_cm":5.95},"end":{"x_cm":9.853553,"y_cm":6.146447}},{"type":"line","end":{"x_cm":6.146447,"y_cm":9.853553}},{"type":"arc","through":{"x_cm":5.85,"y_cm":9.95},"end":{"x_cm":5.5,"y_cm":10}},{"type":"line","end":{"x_cm":4.5,"y_cm":10}},{"type":"arc","through":{"x_cm":4.15,"y_cm":9.95},"end":{"x_cm":3.853553,"y_cm":9.853553}},{"type":"line","end":{"x_cm":0.146447,"y_cm":6.146447}},{"type":"arc","through":{"x_cm":0.05,"y_cm":5.95},"end":{"x_cm":0,"y_cm":5.5}},{"type":"line","end":{"x_cm":0,"y_cm":4.5}},{"type":"arc","through":{"x_cm":0.05,"y_cm":4.05},"end":{"x_cm":0.146447,"y_cm":3.853553}},{"type":"line","end":{"x_cm":3.853553,"y_cm":0.146447}},{"type":"arc","through":{"x_cm":4.15,"y_cm":0.05},"end":{"x_cm":4.5,"y_cm":0}}]}""";

    private static Type Validator() => Tools.GetType("McpCad.Tools.Onshape.Orchestration.OnshapePartPlanValidator", true)!;
    private static MethodInfo Parser() => Tools.GetType("McpCad.Tools.Onshape.Orchestration.OnshapePartPlanParser", true)!
        .GetMethod("Parse", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)!;
    private static object Item(object tuple, string name) => tuple.GetType().GetField(name)!.GetValue(tuple)!;
}
