using System.Collections.Immutable;
using System.Globalization;
using System.Text.Json;
using McpCad.Core.Models;
using McpCad.Onshape.Builders;

namespace McpCad.Tools.Onshape.Orchestration;

internal static class OnshapePartPlanParser
{
    private static readonly string[] AddressMembers = ["documentId", "workspaceOrVersionId", "microversionId", "elementId"];
    private static readonly string[] PlanMembers = ["schemaVersion", "stages"];
    private static readonly string[] StageMembers = ["id", "kind", "plane", "profile", "sketchId", "operation", "depth_cm", "direction", "afterStageId", "view"];
    private static readonly string[] ProfileMembers = ["type", "x1_cm", "y1_cm", "x2_cm", "y2_cm", "cx_cm", "cy_cm", "radius_cm", "points", "start", "segments"];

    public static (CadElementAddress Address, OnshapePartPlan Plan) Parse(string addressJson, string planJson)
    {
        using var addressDocument = Document(addressJson, "address");
        using var planDocument = Document(planJson, "plan");
        return (ParseAddress(addressDocument.RootElement), ParsePlan(planDocument.RootElement));
    }

    private static CadElementAddress ParseAddress(JsonElement element)
    {
        var fields = Fields(element, "address", AddressMembers, "INVALID_ADDRESS", "documentId", "workspaceOrVersionId", "elementId");
        return new CadElementAddress(
            String(fields["documentId"], "address.documentId", "INVALID_ADDRESS"),
            String(fields["workspaceOrVersionId"], "address.workspaceOrVersionId", "INVALID_ADDRESS"),
            fields.TryGetValue("microversionId", out var microversion) ? String(microversion, "address.microversionId", "INVALID_ADDRESS") : null,
            String(fields["elementId"], "address.elementId", "INVALID_ADDRESS"));
    }

    private static OnshapePartPlan ParsePlan(JsonElement element)
    {
        var fields = Fields(element, "plan", PlanMembers, "INVALID_PLAN_SCHEMA", "schemaVersion", "stages");
        var version = SchemaVersion(fields["schemaVersion"]);
        if (version != 1) Fail("UNSUPPORTED_SCHEMA_VERSION", "plan.schemaVersion", "must be integer 1");
        if (fields["stages"].ValueKind != JsonValueKind.Array) Fail("INVALID_PLAN_SCHEMA", "plan.stages", "must be an array");
        var stages = ImmutableArray.CreateBuilder<OnshapePlanStage>();
        var index = 0;
        foreach (var stage in fields["stages"].EnumerateArray()) stages.Add(ParseStage(stage, $"stages[{index++}]"));
        return new OnshapePartPlan(version, stages.ToImmutable());
    }

    private static OnshapePlanStage ParseStage(JsonElement element, string path)
    {
        var common = Fields(element, path, StageMembers, "INVALID_PLAN_SCHEMA", "id", "kind");
        var id = String(common["id"], $"{path}.id", "INVALID_PLAN_SCHEMA");
        var kind = String(common["kind"], $"{path}.kind", "INVALID_PLAN_SCHEMA");
        return kind switch
        {
            "sketch" => ParseSketch(element, path, id),
            "extrude" => ParseExtrude(element, path, id),
            "checkpoint" => ParseCheckpoint(element, path, id),
            _ => Fail<OnshapePlanStage>("INVALID_PLAN_SCHEMA", $"{path}.kind", "must be sketch, extrude, or checkpoint"),
        };
    }

    private static SketchPlanStage ParseSketch(JsonElement element, string path, string id)
    {
        var fields = Fields(element, path, ["id", "kind", "plane", "profile"], "INVALID_PLAN_SCHEMA", "id", "kind", "plane", "profile");
        return new SketchPlanStage(id, Plane(String(fields["plane"], $"{path}.plane", "INVALID_PLAN_SCHEMA"), $"{path}.plane"), ParseProfile(fields["profile"], $"{path}.profile"));
    }

    private static ExtrudePlanStage ParseExtrude(JsonElement element, string path, string id)
    {
        var fields = Fields(element, path, ["id", "kind", "sketchId", "operation", "depth_cm", "direction"], "INVALID_PLAN_SCHEMA", "id", "kind", "sketchId", "operation", "depth_cm", "direction");
        return new ExtrudePlanStage(id,
            String(fields["sketchId"], $"{path}.sketchId", "INVALID_PLAN_SCHEMA"),
            Operation(String(fields["operation"], $"{path}.operation", "INVALID_PLAN_SCHEMA"), $"{path}.operation"),
            Number(fields["depth_cm"], $"{path}.depth_cm"),
            Direction(String(fields["direction"], $"{path}.direction", "INVALID_PLAN_SCHEMA"), $"{path}.direction"));
    }

    private static CheckpointPlanStage ParseCheckpoint(JsonElement element, string path, string id)
    {
        var fields = Fields(element, path, ["id", "kind", "afterStageId", "view"], "INVALID_PLAN_SCHEMA", "id", "kind", "afterStageId", "view");
        return new CheckpointPlanStage(id,
            String(fields["afterStageId"], $"{path}.afterStageId", "INVALID_PLAN_SCHEMA"),
            View(String(fields["view"], $"{path}.view", "INVALID_PLAN_SCHEMA"), $"{path}.view"));
    }

    private static ClosedProfile ParseProfile(JsonElement element, string path)
    {
        var common = Fields(element, path, ProfileMembers, "INVALID_PLAN_SCHEMA", "type");
        var type = String(common["type"], $"{path}.type", "INVALID_PLAN_SCHEMA");
        return type switch
        {
            "rectangle" => Rectangle(element, path),
            "circle" => Circle(element, path),
            "polygon" => Polygon(element, path),
            "mixed" => Mixed(element, path),
            _ => Fail<ClosedProfile>("INVALID_PLAN_SCHEMA", $"{path}.type", "must be rectangle, circle, polygon, or mixed"),
        };
    }

    private static RectangleProfile Rectangle(JsonElement element, string path)
    {
        var f = Fields(element, path, ["type", "x1_cm", "y1_cm", "x2_cm", "y2_cm"], "INVALID_PLAN_SCHEMA", "type", "x1_cm", "y1_cm", "x2_cm", "y2_cm");
        return new RectangleProfile(Number(f["x1_cm"], $"{path}.x1_cm"), Number(f["y1_cm"], $"{path}.y1_cm"), Number(f["x2_cm"], $"{path}.x2_cm"), Number(f["y2_cm"], $"{path}.y2_cm"));
    }

    private static CircleProfile Circle(JsonElement element, string path)
    {
        var f = Fields(element, path, ["type", "cx_cm", "cy_cm", "radius_cm"], "INVALID_PLAN_SCHEMA", "type", "cx_cm", "cy_cm", "radius_cm");
        return new CircleProfile(Number(f["cx_cm"], $"{path}.cx_cm"), Number(f["cy_cm"], $"{path}.cy_cm"), Number(f["radius_cm"], $"{path}.radius_cm"));
    }

    private static PolygonProfile Polygon(JsonElement element, string path)
    {
        var f = Fields(element, path, ["type", "points"], "INVALID_PLAN_SCHEMA", "type", "points");
        if (f["points"].ValueKind != JsonValueKind.Array) Fail("INVALID_PLAN_SCHEMA", $"{path}.points", "must be an array");
        var points = ImmutableArray.CreateBuilder<SketchPoint>();
        var index = 0;
        foreach (var point in f["points"].EnumerateArray())
        {
            var pointPath = $"{path}.points[{index++}]";
            var p = Fields(point, pointPath, ["x_cm", "y_cm"], "INVALID_PLAN_SCHEMA", "x_cm", "y_cm");
            points.Add(new SketchPoint(Number(p["x_cm"], $"{pointPath}.x_cm"), Number(p["y_cm"], $"{pointPath}.y_cm")));
        }
        return new PolygonProfile(points.ToImmutable());
    }

    private static MixedProfile Mixed(JsonElement element, string path)
    {
        var f = Fields(element, path, ["type", "start", "segments"], "INVALID_PLAN_SCHEMA", "type", "start", "segments");
        var start = Point(f["start"], $"{path}.start");
        if (f["segments"].ValueKind != JsonValueKind.Array) Fail("INVALID_PLAN_SCHEMA", $"{path}.segments", "must be an array");
        var segments = ImmutableArray.CreateBuilder<MixedProfileSegmentInput>();
        var index = 0;
        foreach (var segment in f["segments"].EnumerateArray()) segments.Add(Segment(segment, $"{path}.segments[{index++}]"));
        return new MixedProfile(start, segments.ToImmutable());
    }

    private static MixedProfileSegmentInput Segment(JsonElement element, string path)
    {
        var common = Fields(element, path, ["type", "end", "through"], "INVALID_PLAN_SCHEMA", "type", "end");
        var type = String(common["type"], $"{path}.type", "INVALID_PLAN_SCHEMA");
        return type switch
        {
            "line" => new MixedProfileLineSegment(Point(Fields(element, path, ["type", "end"], "INVALID_PLAN_SCHEMA", "type", "end")["end"], $"{path}.end")),
            "arc" => Arc(element, path),
            _ => Fail<MixedProfileSegmentInput>("INVALID_PLAN_SCHEMA", $"{path}.type", "must be line or arc"),
        };
    }

    private static MixedProfileSegmentInput Arc(JsonElement element, string path)
    {
        var f = Fields(element, path, ["type", "through", "end"], "INVALID_PLAN_SCHEMA", "type", "through", "end");
        return new MixedProfileThreePointArcSegment(Point(f["through"], $"{path}.through"), Point(f["end"], $"{path}.end"));
    }

    private static MixedProfilePointCm Point(JsonElement element, string path)
    {
        var f = Fields(element, path, ["x_cm", "y_cm"], "INVALID_PLAN_SCHEMA", "x_cm", "y_cm");
        return new MixedProfilePointCm(Number(f["x_cm"], $"{path}.x_cm"), Number(f["y_cm"], $"{path}.y_cm"));
    }

    private static Dictionary<string, JsonElement> Fields(JsonElement element, string path, string[] allowed, string code, params string[] required)
    {
        if (element.ValueKind != JsonValueKind.Object) Fail(code, path, "must be an object");
        var fields = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        foreach (var property in element.EnumerateObject())
        {
            var memberPath = $"{path}.{property.Name}";
            if (!fields.TryAdd(property.Name, property.Value)) Fail(code, memberPath, "is duplicated");
            if (!allowed.Contains(property.Name, StringComparer.Ordinal)) Fail(code, memberPath, "is not allowed");
        }
        foreach (var name in required) if (!fields.ContainsKey(name)) Fail(code, $"{path}.{name}", "is required");
        return fields;
    }

    private static JsonDocument Document(string json, string path)
    {
        try { return JsonDocument.Parse(json); }
        catch (JsonException exception) { throw new ArgumentException($"INVALID_JSON at {path}: {exception.Message}", exception); }
    }

    private static int SchemaVersion(JsonElement element) =>
        element.ValueKind == JsonValueKind.Number && int.TryParse(element.GetRawText(), NumberStyles.None, CultureInfo.InvariantCulture, out var version)
            ? version : Fail<int>("UNSUPPORTED_SCHEMA_VERSION", "plan.schemaVersion", "must be an integer");

    private static string String(JsonElement element, string path, string code)
    {
        var value = element.ValueKind == JsonValueKind.String ? element.GetString() : null;
        return !string.IsNullOrWhiteSpace(value) ? value : Fail<string>(code, path, "must be a non-empty string");
    }

    private static double Number(JsonElement element, string path) =>
        element.ValueKind == JsonValueKind.Number && element.TryGetDouble(out var value) && double.IsFinite(value)
            ? value : Fail<double>("INVALID_PLAN_SCHEMA", path, "must be a finite number");

    private static CanonicalSketchPlane Plane(string value, string path) => value switch
    {
        "Top" => CanonicalSketchPlane.Top, "Front" => CanonicalSketchPlane.Front, "Right" => CanonicalSketchPlane.Right,
        _ => Fail<CanonicalSketchPlane>("INVALID_PLAN_SCHEMA", path, "must be Top, Front, or Right"),
    };
    private static ExtrudeOperation Operation(string value, string path) => value switch
    {
        "new" => ExtrudeOperation.New, "add" => ExtrudeOperation.Add, "remove" => ExtrudeOperation.Remove,
        _ => Fail<ExtrudeOperation>("INVALID_PLAN_SCHEMA", path, "must be new, add, or remove"),
    };
    private static ExtrudeDirection Direction(string value, string path) => value switch
    {
        "positive" => ExtrudeDirection.Positive, "negative" => ExtrudeDirection.Negative,
        _ => Fail<ExtrudeDirection>("INVALID_PLAN_SCHEMA", path, "must be positive or negative"),
    };
    private static CheckpointView View(string value, string path) => value switch
    {
        "iso" => CheckpointView.Iso, "top" => CheckpointView.Top,
        _ => Fail<CheckpointView>("INVALID_PLAN_SCHEMA", path, "must be iso or top"),
    };
    private static void Fail(string code, string path, string message) => throw new ArgumentException($"{code} at {path}: {message}");
    private static T Fail<T>(string code, string path, string message) { Fail(code, path, message); return default!; }
}
