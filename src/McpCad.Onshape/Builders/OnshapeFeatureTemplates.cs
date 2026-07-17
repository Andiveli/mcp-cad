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
        var envelope = new Dictionary<string, object?>
        {
            ["feature"] = new Dictionary<string, object?>
            {
                ["featureType"] = "extrude",
                ["name"] = "Extrude 1",
                ["suppressed"] = false,
                ["parameters"] = new object[]
                {
                    Param("depth", depth),
                    Param("direction", dir),
                    Param("operationType", MapBooleanOp(booleanOp)),
                    Param("entities", sketchFeatureId),
                },
            },
            ["rejectMicroversionSkew"] = true,
        };
        return JsonDocument.Parse(JsonSerializer.Serialize(envelope));
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
            "add" or "join" or "joined" => "JOIN",
            "cut" or "remove" => "REMOVE",
            "intersect" => "INTERSECT",
            _ => "NEW",
        };

    private static Dictionary<string, object?> Param(string parameterId, object? value)
    {
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