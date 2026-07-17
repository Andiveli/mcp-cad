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
            Param("depth", depth),
            Param("direction", dir),
            Param("operationType", MapBooleanOp(booleanOp)),
            Param("entities", sketchFeatureId));
    }

    public static JsonDocument RevolveFromSketch(
        string sketchFeatureId,
        double angleDeg,
        string direction = "positive",
        string booleanOp = "new") =>
        BuildFeatureEnvelope("revolve", "Revolve 1",
            Param("entities", sketchFeatureId),
            Param("angle", angleDeg),
            Param("direction", NormalizeDirection(direction, angleDeg)),
            Param("operationType", MapBooleanOp(booleanOp)));

    public static JsonDocument Fillet(IReadOnlyList<string> edgeIds, double radiusMeters)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(radiusMeters, 0);
        EnsureEdges(edgeIds);
        return BuildFeatureEnvelope("fillet", "Fillet 1",
            Param("entities", edgeIds),
            Param("radius", radiusMeters));
    }

    public static JsonDocument Chamfer(IReadOnlyList<string> edgeIds, double distanceMeters)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(distanceMeters, 0);
        EnsureEdges(edgeIds);
        return BuildFeatureEnvelope("chamfer", "Chamfer 1",
            Param("entities", edgeIds),
            Param("distance", distanceMeters),
            Param("chamferType", "equalDistance"));
    }

    public static JsonDocument Hole(
        string centerPoint,
        double diameterMeters,
        double depthMeters,
        string holeType = "simple")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(centerPoint);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(diameterMeters, 0);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(depthMeters, 0);
        return BuildFeatureEnvelope("hole", "Hole 1",
            Param("centerPoint", centerPoint),
            Param("diameter", diameterMeters),
            Param("depth", depthMeters),
            Param("holeType", holeType));
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
