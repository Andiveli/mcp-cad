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

    public static JsonDocument CircularPatternFromFeature(
        string sourceFeatureId,
        string axisSketchFeatureId,
        int instanceCount,
        double totalAngleDeg,
        bool oppositeDirection = false,
        bool equalSpace = true)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceFeatureId);
        ArgumentException.ThrowIfNullOrWhiteSpace(axisSketchFeatureId);
        ArgumentOutOfRangeException.ThrowIfLessThan(instanceCount, 2);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(instanceCount, 100);
        if (!double.IsFinite(totalAngleDeg) || totalAngleDeg <= 0 || totalAngleDeg > 360)
        {
            throw new ArgumentOutOfRangeException(nameof(totalAngleDeg), "Total angle must be finite and between 0 and 360 degrees.");
        }

        return BuildFeatureEnvelope("circularPattern", "Circular pattern 1",
            ParamEnum("patternType", "PatternType", "FEATURE"),
            ParamEnum("operationType", "NewBodyOperationType", "NEW"),
            ParamEmptyQueryList("entities"),
            ParamEmptyQueryList("faces"),
            ParamFeatureList("instanceFunction", sourceFeatureId),
            ParamAxisSketch("axis", axisSketchFeatureId),
            ParamDegreeQuantity("angle", totalAngleDeg),
            ParamIntegerQuantity("instanceCount", instanceCount),
            ParamBoolean("oppositeDirection", oppositeDirection),
            ParamBoolean("equalSpace", equalSpace),
            ParamBoolean("isCentered", false),
            ParamBoolean("defaultScope", false),
            ParamEmptyQueryList("booleanScope"),
            ParamBoolean("fullFeaturePattern", false),
            ParamBoolean("skipInstances", false),
            ParamEmptyArray("skippedInstances"));
    }

    public static JsonDocument RectangularPatternFromFeature(
        string sourceFeatureId,
        string directionOneSketchFeatureId,
        double distanceMeters,
        int instanceCount,
        string? directionTwoSketchFeatureId = null,
        double? distanceTwoMeters = null,
        int? instanceCountTwo = null,
        bool oppositeDirection = false,
        bool isCentered = false,
        bool oppositeDirectionTwo = false,
        bool isCenteredTwo = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceFeatureId);
        ArgumentException.ThrowIfNullOrWhiteSpace(directionOneSketchFeatureId);
        EnsurePatternDistance(distanceMeters, nameof(distanceMeters));
        EnsurePatternInstanceCount(instanceCount, nameof(instanceCount));

        var hasSecondDirection = directionTwoSketchFeatureId is not null ||
                distanceTwoMeters.HasValue ||
                instanceCountTwo.HasValue ||
                oppositeDirectionTwo ||
                isCenteredTwo;
        if (hasSecondDirection &&
            (string.IsNullOrWhiteSpace(directionTwoSketchFeatureId) || !distanceTwoMeters.HasValue || !instanceCountTwo.HasValue))
        {
            throw new ArgumentException("Second direction, distance, and instance count must be supplied together.");
        }

        var secondDirection = hasSecondDirection ? directionTwoSketchFeatureId! : null;
        var secondDistance = hasSecondDirection ? distanceTwoMeters!.Value : 0.025;
        var secondCount = hasSecondDirection ? instanceCountTwo!.Value : 1;
        if (hasSecondDirection)
        {
            EnsurePatternDistance(secondDistance, nameof(distanceTwoMeters));
            EnsurePatternInstanceCount(secondCount, nameof(instanceCountTwo));
        }

        return BuildFeatureEnvelope("linearPattern", "Linear pattern 1",
            ParamEnum("patternType", "PatternType", "FEATURE"),
            ParamEnum("operationType", "NewBodyOperationType", "NEW"),
            ParamEmptyQueryList("entities"),
            ParamEmptyQueryList("faces"),
            ParamFeatureList("instanceFunction", sourceFeatureId),
            ParamAxisSketch("directionOne", directionOneSketchFeatureId),
            ParamMeterQuantity("distance", distanceMeters),
            ParamIntegerQuantity("instanceCount", instanceCount),
            ParamBoolean("oppositeDirection", oppositeDirection),
            ParamBoolean("isCentered", isCentered),
            ParamBoolean("hasSecondDir", hasSecondDirection),
            hasSecondDirection ? ParamAxisSketch("directionTwo", secondDirection!) : ParamEmptyQueryList("directionTwo"),
            hasSecondDirection
                    ? ParamMeterQuantity("distanceTwo", secondDistance)
                    : ParamDisabledMeterQuantity("distanceTwo", secondDistance),
            hasSecondDirection
                    ? ParamIntegerQuantity("instanceCountTwo", secondCount)
                    : ParamDisabledIntegerQuantity("instanceCountTwo", secondCount),
            ParamBoolean("oppositeDirectionTwo", hasSecondDirection && oppositeDirectionTwo),
            ParamBoolean("isCenteredTwo", hasSecondDirection && isCenteredTwo),
            ParamBoolean("defaultScope", false),
            ParamEmptyQueryList("booleanScope"),
            ParamBoolean("fullFeaturePattern", false),
            ParamBoolean("skipInstances", false),
            ParamEmptyArray("skippedInstances"));
    }

    public static JsonDocument MirrorFeature(string sourceFeatureId, string mirrorPlane)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceFeatureId);
        ArgumentException.ThrowIfNullOrWhiteSpace(mirrorPlane);

        return BuildFeatureEnvelope("mirror", "Mirror 1",
            ParamEnum("patternType", "MirrorType", "FEATURE"),
            ParamEnum("operationType", "NewBodyOperationType", "NEW"),
            ParamEmptyQueryList("entities"),
            ParamEmptyQueryList("faces"),
            ParamFeatureList("instanceFunction", sourceFeatureId),
            ParamCanonicalSketchPlane("mirrorPlane", mirrorPlane),
            ParamBoolean("defaultScope", false),
            ParamEmptyQueryList("booleanScope"),
            ParamBoolean("fullFeaturePattern", false));
    }

    public static JsonDocument OffsetPlane(
        string canonicalPlane,
        double offsetMeters,
        bool oppositeDirection = false)
    {
        if (!double.IsFinite(offsetMeters) || offsetMeters < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(offsetMeters), "Offset must be finite and nonnegative.");
        }

        return BuildFeatureEnvelope("cPlane", "Plane 1",
            ParamCanonicalSketchPlane("entities", canonicalPlane),
            ParamEnum("cplaneType", "CPlaneType", "OFFSET"),
            ParamMeterQuantity("offset", offsetMeters),
            ParamDegreeQuantity("angle", 0),
            ParamBoolean("oppositeDirection", oppositeDirection),
            ParamBoolean("flipAlignment", false),
            ParamBoolean("flipNormal", false),
            ParamMeterQuantity("width", 0.15),
            ParamMeterQuantity("height", 0.15));
    }

    public static JsonDocument SweepFromSketch(
        string profileSketchFeatureId,
        string pathSketchFeatureId,
        string booleanOp = "new") =>
        BuildFeatureEnvelope("sweep", "Sweep 1",
            ParamEnum("bodyType", "ExtendedToolBodyType", "SOLID"),
            ParamEnum("operationType", "NewBodyOperationType", MapBooleanOp(booleanOp)),
            ParamSketchProfile("profiles", profileSketchFeatureId),
            ParamAxisSketch("path", pathSketchFeatureId),
            ParamEnum("profileControl", "ProfileControlMode", "NONE"));

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

    public static JsonDocument Shell(
        IReadOnlyList<string> faceIds,
        double thicknessMeters,
        bool oppositeDirection = false)
    {
        if (!double.IsFinite(thicknessMeters) || thicknessMeters <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(thicknessMeters), "Shell thickness must be finite and positive.");
        }

        EnsureFaces(faceIds);
        return BuildFeatureEnvelope("shell", "Shell 1",
            ParamBoolean("isHollow", false),
            ParamEntities("entities", faceIds),
            ParamEmptyQueryList("parts"),
            ParamMeterQuantity("thickness", thicknessMeters),
            ParamBoolean("oppositeDirection", oppositeDirection));
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

    private static void EnsurePatternDistance(double distanceMeters, string parameterName)
    {
        if (!double.IsFinite(distanceMeters) || distanceMeters <= 0)
        {
            throw new ArgumentOutOfRangeException(parameterName, "Pattern distance must be finite and positive.");
        }
    }

    private static void EnsurePatternInstanceCount(int instanceCount, string parameterName)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(instanceCount, 2, parameterName);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(instanceCount, 100, parameterName);
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

    private static void EnsureFaces(IReadOnlyList<string> faceIds)
    {
        ArgumentNullException.ThrowIfNull(faceIds);
        ArgumentOutOfRangeException.ThrowIfEqual(faceIds.Count, 0);
        if (faceIds.Any(string.IsNullOrWhiteSpace))
        {
            throw new ArgumentException("Face IDs must be non-empty.", nameof(faceIds));
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

    private static Dictionary<string, object?> ParamSketchProfile(string parameterId, string sketchFeatureId) =>
        new()
        {
            ["btType"] = "BTMParameterQueryList-148",
            ["parameterId"] = parameterId,
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

    private static Dictionary<string, object?> ParamFeatureList(string parameterId, string featureId) =>
        new()
        {
            ["btType"] = "BTMParameterFeatureList-1749",
            ["parameterId"] = parameterId,
            ["featureIds"] = new[] { featureId },
        };

    private static Dictionary<string, object?> ParamEmptyQueryList(string parameterId) =>
        new()
        {
            ["btType"] = "BTMParameterQueryList-148",
            ["parameterId"] = parameterId,
            ["queries"] = Array.Empty<Dictionary<string, object?>>(),
        };

    private static Dictionary<string, object?> ParamEmptyArray(string parameterId) =>
        new()
        {
            ["btType"] = "BTMParameterArray-2025",
            ["parameterId"] = parameterId,
            ["items"] = Array.Empty<object>(),
        };

    private static Dictionary<string, object?> ParamMeterQuantity(string parameterId, double value) =>
        new()
        {
            ["btType"] = "BTMParameterQuantity-147",
            ["parameterId"] = parameterId,
            ["expression"] = $"{value.ToString("G9", CultureInfo.InvariantCulture)} m",
        };

    private static Dictionary<string, object?> ParamDisabledMeterQuantity(string parameterId, double value) =>
            new()
            {
                ["btType"] = "BTMParameterQuantity-147",
                ["parameterId"] = parameterId,
                ["expression"] = string.Empty,
                ["value"] = value,
                ["units"] = "meter",
            };

        private static Dictionary<string, object?> ParamDisabledIntegerQuantity(string parameterId, int value) =>
            new()
            {
                ["btType"] = "BTMParameterQuantity-147",
                ["parameterId"] = parameterId,
                ["expression"] = string.Empty,
                ["value"] = (double)value,
                ["isInteger"] = true,
            };

        private static Dictionary<string, object?> ParamDegreeQuantity(string parameterId, double value) =>
        new()
        {
            ["btType"] = "BTMParameterQuantity-147",
            ["parameterId"] = parameterId,
            ["expression"] = $"{value.ToString("G9", CultureInfo.InvariantCulture)} deg",
        };

    private static Dictionary<string, object?> ParamIntegerQuantity(string parameterId, int value) =>
        new()
        {
            ["btType"] = "BTMParameterQuantity-147",
            ["parameterId"] = parameterId,
            ["isInteger"] = true,
            ["expression"] = value.ToString(CultureInfo.InvariantCulture),
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

    private static Dictionary<string, object?> ParamCanonicalSketchPlane(string parameterId, string plane)
    {
        using var sketch = OnshapeSketchBuilder.BuildSketch(
            Array.Empty<Dictionary<string, object?>>(),
            Array.Empty<object>(),
            plane);
        var parameter = JsonSerializer.Deserialize<Dictionary<string, object?>>(
            sketch.RootElement.GetProperty("feature").GetProperty("parameters")[0].GetRawText())
            ?? throw new InvalidOperationException("Canonical sketch plane parameter could not be built.");
        parameter["parameterId"] = parameterId;
        return parameter;
    }

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
