using System.Globalization;
using System.Text.Json;

namespace McpCad.Onshape.Builders;

/// <summary>
/// Builds Onshape Part Studio feature POST envelopes ({ feature, rejectMicroversionSkew }).
/// Distances in meters at the template boundary.
/// </summary>
public static class OnshapeFeatureTemplates
{
    public static JsonDocument ExtrudeFromSketch(
        string sketchFeatureId,
        double depthMeters,
        string direction = "positive",
        string booleanOp = "new")
    {
        var depth = Math.Abs(depthMeters);
        var dir = NormalizeDirection(direction, depthMeters);
        return BuildFeatureEnvelope("extrude", "Extrude 1",
            ParamMeterQuantity("depth", depth),
            Param("direction", dir),
            ParamEnum("operationType", "NewBodyOperationType", MapBooleanOp(booleanOp)),
            ParamSketchEntities(sketchFeatureId));
    }

    public static JsonDocument RevolveFromSketch(
        string sketchFeatureId,
        string axisSketchFeatureId,
        double angleDeg,
        string direction = "positive",
        string booleanOp = "new") =>
        BuildFeatureEnvelope("revolve", "Revolve 1",
            ParamEnum("bodyType", "ExtendedToolBodyType", "SOLID"),
            ParamSketchEntities(sketchFeatureId),
            ParamAxisSketch("axis", axisSketchFeatureId),
            ParamDegreeQuantity("angle", Math.Abs(angleDeg)),
            ParamBoolean("oppositeDirection", NormalizeDirection(direction, angleDeg) == "negative"),
            ParamEnum("operationType", "NewBodyOperationType", MapBooleanOp(booleanOp)));

    public static JsonDocument Fillet(IReadOnlyList<string> edgeIds, double radiusMeters)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(radiusMeters, 0);
        EnsureEdges(edgeIds);
        return BuildFeatureEnvelope("fillet", "Fillet 1",
            ParamEntities("entities", edgeIds),
            ParamMeterQuantity("radius", radiusMeters));
    }

    public static JsonDocument Chamfer(IReadOnlyList<string> edgeIds, double distanceMeters)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(distanceMeters, 0);
        EnsureEdges(edgeIds);
        return BuildFeatureEnvelope("chamfer", "Chamfer 1",
            ParamEntities("entities", edgeIds),
            ParamEnum("chamferMethod", "ChamferMethod", "FACE_OFFSET"),
            ParamEnum("chamferType", "ChamferType", "EQUAL_OFFSETS"),
            ParamMeterQuantity("width", distanceMeters));
    }

    internal static Dictionary<string, object?> BuildMinimalFeatureEnvelope(
        string featureType,
        IReadOnlyList<Dictionary<string, object?>>? parameters)
    {
        var paramList = new List<object>();
        if (parameters is not null)
        {
            foreach (var p in parameters)
            {
                foreach (var kv in p)
                {
                    paramList.Add(Param(kv.Key, kv.Value));
                }
            }
        }

        return new Dictionary<string, object?>
        {
            ["feature"] = new Dictionary<string, object?>
            {
                ["featureType"] = featureType,
                ["suppressed"] = false,
                ["parameters"] = paramList,
            },
            ["rejectMicroversionSkew"] = true,
        };
    }

    private static string NormalizeDirection(string direction, double depthMeters)
    {
        if (depthMeters < 0)
        {
            return direction.Equals("positive", StringComparison.OrdinalIgnoreCase) ? "negative" : "positive";
        }

        return direction.Equals("negative", StringComparison.OrdinalIgnoreCase) ? "negative" : "positive";
    }

    private static string MapBooleanOp(string booleanOp) =>
        booleanOp.ToLowerInvariant() switch
        {
            "add" or "join" or "joined" => "ADD",
            "cut" or "remove" => "REMOVE",
            "intersect" => "INTERSECT",
            _ => "NEW",
        };

    private static JsonDocument BuildFeatureEnvelope(
        string featureType,
        string name,
        params Dictionary<string, object?>[] parameters)
    {
        var envelope = new Dictionary<string, object?>
        {
            ["feature"] = new Dictionary<string, object?>
            {
                ["featureType"] = featureType,
                ["name"] = name,
                ["suppressed"] = false,
                ["parameters"] = parameters,
            },
            ["rejectMicroversionSkew"] = true,
        };
        return JsonDocument.Parse(JsonSerializer.Serialize(envelope));
    }

    private static void EnsureEdges(IReadOnlyList<string> edgeIds)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(edgeIds.Count, 0);
        if (edgeIds.Any(string.IsNullOrWhiteSpace))
        {
            throw new ArgumentException("Edge IDs must be non-empty.", nameof(edgeIds));
        }
    }

    private static Dictionary<string, object?> ParamSketchEntities(string sketchFeatureId) =>
        new()
        {
            ["btType"] = "BTMParameterQueryList-148",
            ["parameterId"] = "entities",
            ["queries"] = new List<Dictionary<string, object?>>
            {
                new()
                {
                    ["btType"] = "BTMIndividualSketchRegionQuery-140",
                    ["featureId"] = sketchFeatureId,
                    ["filterInnerLoops"] = true,
                },
            },
        };

    private static Dictionary<string, object?> ParamEnum(string parameterId, string enumName, string value) =>
        new()
        {
            ["btType"] = "BTMParameterEnum-145",
            ["parameterId"] = parameterId,
            ["enumName"] = enumName,
            ["value"] = value,
        };

    private static Dictionary<string, object?> ParamMeterQuantity(string parameterId, double value) =>
        new()
        {
            ["btType"] = "BTMParameterQuantity-147",
            ["parameterId"] = parameterId,
            ["expression"] = $"{value.ToString("G9", CultureInfo.InvariantCulture)} m",
        };

    private static Dictionary<string, object?> ParamDegreeQuantity(string parameterId, double value) =>
        new()
        {
            ["btType"] = "BTMParameterQuantity-147",
            ["parameterId"] = parameterId,
            ["expression"] = $"{value.ToString("G9", CultureInfo.InvariantCulture)} deg",
        };

    private static Dictionary<string, object?> ParamBoolean(string parameterId, bool value) =>
        new()
        {
            ["btType"] = "BTMParameterBoolean-144",
            ["parameterId"] = parameterId,
            ["value"] = value,
        };

    private static Dictionary<string, object?> ParamEntities(string parameterId, IReadOnlyList<string> ids) =>
        new()
        {
            ["btType"] = "BTMParameterQueryList-148",
            ["parameterId"] = parameterId,
            ["queries"] = new List<Dictionary<string, object?>>
            {
                new()
                {
                    ["btType"] = "BTMIndividualQuery-138",
                    ["deterministicIds"] = ids,
                },
            },
        };

    private static Dictionary<string, object?> ParamAxisSketch(string parameterId, string featureId) =>
        new()
        {
            ["btType"] = "BTMParameterQueryList-148",
            ["parameterId"] = parameterId,
            ["queries"] = new List<Dictionary<string, object?>>
            {
                new()
                {
                    ["btType"] = "BTMIndividualQuery-138",
                    ["queryString"] = $"query = qCreatedBy(id + \"{featureId}\" + \"wireOp\", EntityType.EDGE);",
                    ["deterministicIds"] = Array.Empty<string>(),
                },
            },
        };

    private static Dictionary<string, object?> Param(string parameterId, object? value)
    {
        if (value is IReadOnlyList<string> strings)
        {
            return new Dictionary<string, object?>
            {
                ["btType"] = "BTMParameterString-149",
                ["parameterId"] = parameterId,
                ["value"] = string.Join(",", strings),
            };
        }

        if (value is string s)
        {
            return new Dictionary<string, object?>
            {
                ["btType"] = "BTMParameterString-149",
                ["parameterId"] = parameterId,
                ["value"] = s,
            };
        }

        return new Dictionary<string, object?>
        {
            ["btType"] = "BTMParameterQuantity-147",
            ["parameterId"] = parameterId,
            ["value"] = CoerceNumeric(value),
        };
    }

    private static object? CoerceNumeric(object? value)
    {
        if (value is double d)
        {
            return double.Parse(d.ToString("G9", CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);
        }

        return value;
    }
}
