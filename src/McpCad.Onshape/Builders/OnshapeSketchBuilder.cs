using System.Globalization;
using System.Text.Json;

namespace McpCad.Onshape.Builders;

public static class OnshapeSketchBuilder
{
    private const string CurveSegmentType = "BTMSketchCurveSegment-155";
    private const string CurveType = "BTMSketchCurve-4";
    private const string LineGeometryType = "BTCurveGeometryLine-117";
    private const string CircleGeometryType = "BTCurveGeometryCircle-115";
    public static JsonDocument BuildRectangle(double x1M, double y1M, double x2M, double y2M, string? plane = null)
    {
        var minX = Math.Min(x1M, x2M);
        var maxX = Math.Max(x1M, x2M);
        var minY = Math.Min(y1M, y2M);
        var maxY = Math.Max(y1M, y2M);

        var entities = new List<Dictionary<string, object?>>();
        entities.Add(CurveSegment("line-0", "point-0", "point-1", minX, minY, maxX, minY));
        entities.Add(CurveSegment("line-1", "point-1", "point-2", maxX, minY, maxX, maxY));
        entities.Add(CurveSegment("line-2", "point-2", "point-3", maxX, maxY, minX, maxY));
        entities.Add(CurveSegment("line-3", "point-3", "point-0", minX, maxY, minX, minY));

        return BuildSketch(entities, Array.Empty<object>(), plane);
    }

    public static JsonDocument BuildLine(double x1M, double y1M, double x2M, double y2M)
    {
        var entities = new List<Dictionary<string, object?>>();
        entities.Add(CurveSegment("line-0", "point-0", "point-1", x1M, y1M, x2M, y2M));
        return BuildSketch(entities, Array.Empty<object>());
    }

    public static JsonDocument BuildCircle(double cxM, double cyM, double radiusM)
    {
        var entities = new List<Dictionary<string, object?>>
        {
            new()
            {
                ["btType"] = CurveType,
                ["entityId"] = "circle-0",
                ["geometry"] = new Dictionary<string, object?>
                {
                    ["btType"] = CircleGeometryType,
                    ["xCenter"] = N9(cxM),
                    ["yCenter"] = N9(cyM),
                    ["radius"] = N9(radiusM),
                    ["clockwise"] = false,
                    ["xDir"] = 1.0,
                    ["yDir"] = 0.0,
                },
                ["isConstruction"] = false,
                ["startParam"] = 0.0,
                ["endParam"] = 2.0 * Math.PI,
            },
        };

        return BuildSketch(entities, Array.Empty<object>());
    }

    public static JsonDocument BuildSketch(
        IReadOnlyList<Dictionary<string, object?>> entities,
        IReadOnlyList<object> constraints,
        string? plane = null)
    {
        var (operationId, deterministicId) = (plane ?? "Top").Trim().ToLowerInvariant() switch
        {
            "top" => ("TopplaneOp", "JDC"),
            "front" => ("FrontplaneOp", "JCC"),
            "right" => ("RightplaneOp", "JBC"),
            _ => throw new ArgumentException("Plane must be Top, Front, or Right.", nameof(plane)),
        };

        var envelope = new Dictionary<string, object?>
        {
            ["feature"] = new Dictionary<string, object?>
            {
                ["btType"] = "BTMSketch-151",
                ["parameters"] = new object[]
                {
                    new Dictionary<string, object?>
                    {
                        ["btType"] = "BTMParameterQueryList-148",
                        ["libraryRelationType"] = "DEFAULT",
                        ["queries"] = new object[]
                        {
                            new Dictionary<string, object?>
                            {
                                ["btType"] = "BTMIndividualQuery-138",
                                ["queryStatement"] = null,
                                ["queryString"] = $"query=qCompressed(1.0,\"%B5$QueryM4Sa$entityTypeBa$EntityTypeS4$FACESb$historyTypeS8$CREATIONSb$operationIdB2$IdA1S{operationId.Length - 7}.7${operationId}S9$queryTypeS5$DUMMY\",id);",
                                ["deterministicIds"] = new[] { deterministicId },
                            },
                        },
                        ["filter"] = SketchPlaneFilter(),
                        ["parameterId"] = "sketchPlane",
                        ["parameterName"] = "",
                    },
                },
                ["entities"] = entities,
                ["constraints"] = constraints,
            },
            ["rejectMicroversionSkew"] = true,
        };
        return JsonDocument.Parse(JsonSerializer.Serialize(envelope));
    }

    private static Dictionary<string, object?> SketchPlaneFilter() => new()
    {
        ["btType"] = "BTOrFilter-167",
        ["operand1"] = new Dictionary<string, object?>
        {
            ["btType"] = "BTOrFilter-167",
            ["operand1"] = new Dictionary<string, object?>
            {
                ["btType"] = "BTAndFilter-110",
                ["operand1"] = new Dictionary<string, object?>
                {
                    ["btType"] = "BTGeometryFilter-130",
                    ["geometryType"] = "PLANE",
                },
                ["operand2"] = new Dictionary<string, object?>
                {
                    ["btType"] = "BTFlatSheetMetalFilter-3018",
                    ["allows"] = "MODEL_ONLY",
                },
            },
            ["operand2"] = new Dictionary<string, object?>
            {
                ["btType"] = "BTAndFilter-110",
                ["operand1"] = new Dictionary<string, object?>
                {
                    ["btType"] = "BTAndFilter-110",
                    ["operand1"] = new Dictionary<string, object?>
                    {
                        ["btType"] = "BTSMDefinitionEntityTypeFilter-1651",
                        ["smDefinitionEntityType"] = "FACE",
                    },
                    ["operand2"] = new Dictionary<string, object?>
                    {
                        ["btType"] = "BTFlatSheetMetalFilter-3018",
                        ["allows"] = "MODEL_AND_FLATTENED",
                    },
                },
                ["operand2"] = new Dictionary<string, object?>
                {
                    ["btType"] = "BTGeometryFilter-130",
                    ["geometryType"] = "PLANE",
                },
            },
        },
        ["operand2"] = new Dictionary<string, object?>
        {
            ["btType"] = "BTBodyTypeFilter-112",
            ["bodyType"] = "MATE_CONNECTOR",
        },
    };

    private static Dictionary<string, object?> CurveSegment(
        string entityId,
        string startPointId,
        string endPointId,
        double x1, double y1, double x2, double y2)
    {
        var dx = x2 - x1;
        var dy = y2 - y1;
        var length = Math.Sqrt(dx * dx + dy * dy);

        var midX = (x1 + x2) / 2.0;
        var midY = (y1 + y2) / 2.0;

        var dirX = length > 0 ? dx / length : 1.0;
        var dirY = length > 0 ? dy / length : 0.0;

        var halfLength = length / 2.0;

        return new()
        {
            ["btType"] = CurveSegmentType,
            ["entityId"] = entityId,
            ["startPointId"] = startPointId,
            ["endPointId"] = endPointId,
            ["geometry"] = new Dictionary<string, object?>
            {
                ["btType"] = LineGeometryType,
                ["pntX"] = N9(midX),
                ["pntY"] = N9(midY),
                ["dirX"] = N9(dirX),
                ["dirY"] = N9(dirY),
            },
            ["startParam"] = N9(-halfLength),
            ["endParam"] = N9(halfLength),
            ["isConstruction"] = false,
        };
    }

    private static double N9(double value) =>
        double.Parse(value.ToString("G9", CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);
}
