using System.Globalization;
using System.Text.Json;

namespace McpCad.Onshape.Builders;

public readonly record struct SketchPoint(double X, double Y);

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

    public static JsonDocument BuildLine(
        double x1M,
        double y1M,
        double x2M,
        double y2M,
        string? plane = null,
        double? lengthM = null)
    {
        var entities = new List<Dictionary<string, object?>>();
        entities.Add(CurveSegment("line-0", "point-0", "point-1", x1M, y1M, x2M, y2M));
        var constraints = lengthM is double value
            ? new object[] { DimensionConstraint("constraint-length-0", "LENGTH", "line-0", value, true) }
            : Array.Empty<object>();
        return BuildSketch(entities, constraints, plane);
    }

    /// <summary>
    /// Builds a sketch-backed construction axis: a centered 40 mm line on a canonical sketch plane.
    /// The returned line is usable as an axis edge by revolve and pattern features; it is not a native Onshape work-axis feature.
    /// </summary>
    public static JsonDocument BuildWorkAxis(string canonicalPlane, string direction = "x")
    {
        var normalizedDirection = direction?.Trim().ToLowerInvariant() switch
        {
            "x" => "x",
            "y" => "y",
            _ => throw new ArgumentException("Direction must be x or y.", nameof(direction)),
        };
        const double halfLengthM = 0.02;
        var axis = normalizedDirection == "x"
            ? CurveSegment("work-axis-0", "work-axis-0.start", "work-axis-0.end", -halfLengthM, 0, halfLengthM, 0)
            : CurveSegment("work-axis-0", "work-axis-0.start", "work-axis-0.end", 0, -halfLengthM, 0, halfLengthM);
        axis["isConstruction"] = true;

        return BuildSketch([axis], Array.Empty<object>(), canonicalPlane, disableImprinting: true);
    }

    public static JsonDocument BuildPolyline(
        IReadOnlyList<SketchPoint> points,
        bool closed = false,
        string? plane = null)
    {
        ArgumentNullException.ThrowIfNull(points);
        var minimumPoints = closed ? 3 : 2;
        if (points.Count < minimumPoints)
        {
            throw new ArgumentException($"A {(closed ? "closed" : "open")} polyline requires at least {minimumPoints} points.", nameof(points));
        }

        if (points.Any(point => !double.IsFinite(point.X) || !double.IsFinite(point.Y)))
        {
            throw new ArgumentException("Polyline points must have finite coordinates.", nameof(points));
        }

        var segmentCount = closed ? points.Count : points.Count - 1;
        var entities = new List<Dictionary<string, object?>>(segmentCount);
        var constraints = new List<object>(segmentCount * 2);

        for (var index = 0; index < segmentCount; index++)
        {
            var start = points[index];
            var end = points[(index + 1) % points.Count];
            if (start == end)
            {
                throw new ArgumentException("Polyline segments cannot have zero length.", nameof(points));
            }

            if (start.Y != end.Y && start.X != end.X)
            {
                throw new ArgumentException("Polyline segments must be horizontal or vertical.", nameof(points));
            }

            entities.Add(CurveSegment(
                $"line-{index}",
                $"point-{index}-start",
                $"point-{index}-end",
                start.X,
                start.Y,
                end.X,
                end.Y));
        }

        for (var index = 0; index < segmentCount - 1; index++)
        {
            constraints.Add(Constraint(
                $"constraint-coincident-{index}",
                "COINCIDENT",
                $"line-{index}.end",
                $"line-{index + 1}.start"));
        }

        if (closed)
        {
            constraints.Add(Constraint(
                $"constraint-coincident-{segmentCount - 1}",
                "COINCIDENT",
                $"line-{segmentCount - 1}.end",
                "line-0.start"));
        }

        for (var index = 0; index < segmentCount; index++)
        {
            var start = points[index];
            var end = points[(index + 1) % points.Count];
            constraints.Add(Constraint(
                $"constraint-{(start.Y == end.Y ? "horizontal" : "vertical")}-{index}",
                start.Y == end.Y ? "HORIZONTAL" : "VERTICAL",
                $"line-{index}"));
        }

        return BuildSketch(entities, constraints, plane);
    }

    public static JsonDocument BuildCircle(
        double cxM,
        double cyM,
        double radiusM,
        string? plane = null,
        double? diameterM = null)
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

        var constraints = diameterM is double value
            ? new object[] { DimensionConstraint("constraint-diameter-0", "DIAMETER", "circle-0", value, false) }
            : Array.Empty<object>();
        return BuildSketch(entities, constraints, plane);
    }

    public static JsonDocument BuildLinearPatternCircle(
        double cxM,
        double cyM,
        double radiusM,
        int columns,
        double spacingM,
        string? plane = null)
    {
        ValidateFinite(cxM, nameof(cxM));
        ValidateFinite(cyM, nameof(cyM));
        ValidateFinite(radiusM, nameof(radiusM));
        ValidateFinite(spacingM, nameof(spacingM));
        if (radiusM <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(radiusM), "Radius must be greater than zero.");
        }
        if (spacingM <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(spacingM), "Spacing must be greater than zero.");
        }
        if (columns is < 2 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(columns), "Columns must be between 2 and 100.");
        }

        var entities = new List<Dictionary<string, object?>>(columns + 1)
        {
            PatternCircle("pattern-circle", "pattern-circle.center", cxM, cyM, radiusM),
        };
        for (var column = 1; column < columns; column++)
        {
            var id = $"pattern-op.0.{column}.0";
            entities.Add(PatternCircle(id, $"{id}.center", cxM + column * spacingM, cyM, radiusM));
        }
        entities.Add(PatternDirectionLine(cxM, cyM, cxM + spacingM, cyM));

        var constraints = new object[]
        {
            PatternLengthConstraint(spacingM),
            LinearPatternConstraint(columns),
        };
        return BuildSketch(entities, constraints, plane);
    }

    public static JsonDocument BuildCircularPatternLine(
        double centerXM,
        double centerYM,
        double orbitRadiusM,
        double lineLengthM,
        int instances,
        string? plane = null)
    {
        ValidateFinite(centerXM, nameof(centerXM));
        ValidateFinite(centerYM, nameof(centerYM));
        ValidateFinite(orbitRadiusM, nameof(orbitRadiusM));
        ValidateFinite(lineLengthM, nameof(lineLengthM));
        if (orbitRadiusM <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(orbitRadiusM), "Orbit radius must be greater than zero.");
        }
        if (lineLengthM <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(lineLengthM), "Line length must be greater than zero.");
        }
        if (instances is < 2 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(instances), "Instances must be between 2 and 100.");
        }

        var entities = new List<Dictionary<string, object?>>(instances + 1)
        {
            PatternCircle("circular-op.pivotCircle", "circular-op.pivotCircle.center", centerXM, centerYM, orbitRadiusM, construction: true),
        };
        for (var instance = 0; instance < instances; instance++)
        {
            var angle = 2.0 * Math.PI * instance / instances;
            var midpointX = centerXM + orbitRadiusM * Math.Cos(angle);
            var midpointY = centerYM + orbitRadiusM * Math.Sin(angle);
            var dirX = -Math.Sin(angle);
            var dirY = Math.Cos(angle);
            var id = instance == 0 ? "circular-op.seed" : $"circular-op.pattern.0.{instance}.0";
            var halfLength = lineLengthM / 2.0;
            entities.Add(CurveSegment(
                id,
                $"{id}.start",
                $"{id}.end",
                midpointX - dirX * halfLength,
                midpointY - dirY * halfLength,
                midpointX + dirX * halfLength,
                midpointY + dirY * halfLength));
        }

        return BuildSketch(entities, [CircularPatternConstraint(instances)], plane);
    }

    public static JsonDocument BuildEllipse(
        double cxM,
        double cyM,
        double majorRadiusM,
        double minorRadiusM,
        double rotationDeg = 0,
        string? plane = null,
        double? majorDiameterM = null,
        double? minorDiameterM = null)
    {
        ValidateFinite(cxM, nameof(cxM));
        ValidateFinite(cyM, nameof(cyM));
        ValidateFinite(majorRadiusM, nameof(majorRadiusM));
        ValidateFinite(minorRadiusM, nameof(minorRadiusM));
        ValidateFinite(rotationDeg, nameof(rotationDeg));
        if (majorRadiusM <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(majorRadiusM), "Major radius must be greater than zero.");
        }
        if (minorRadiusM <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(minorRadiusM), "Minor radius must be greater than zero.");
        }
        if (majorRadiusM < minorRadiusM)
        {
            throw new ArgumentOutOfRangeException(nameof(majorRadiusM), "Major radius must be greater than or equal to minor radius.");
        }
        ValidateOptionalDimension(majorDiameterM, nameof(majorDiameterM));
        ValidateOptionalDimension(minorDiameterM, nameof(minorDiameterM));

        var rotationRad = (rotationDeg % 360.0) * Math.PI / 180.0;
        var entity = new Dictionary<string, object?>
        {
            ["btType"] = CurveType,
            ["entityId"] = "ellipse-0",
            ["centerId"] = "ellipse-0.center",
            ["geometry"] = new Dictionary<string, object?>
            {
                ["btType"] = "BTCurveGeometryEllipse-1189",
                ["xCenter"] = N9(cxM),
                ["yCenter"] = N9(cyM),
                ["radius"] = N9(majorRadiusM),
                ["minorRadius"] = N9(minorRadiusM),
                ["clockwise"] = false,
                ["xDir"] = N9(Math.Cos(rotationRad)),
                ["yDir"] = N9(Math.Sin(rotationRad)),
            },
            ["isConstruction"] = false,
        };
        var constraints = new List<object>(2);
        if (majorDiameterM is double majorDiameter)
        {
            constraints.Add(DimensionConstraint("constraint-major-diameter-0", "MAJOR_DIAMETER", "ellipse-0", majorDiameter, false));
        }
        if (minorDiameterM is double minorDiameter)
        {
            constraints.Add(DimensionConstraint("constraint-minor-diameter-0", "MINOR_DIAMETER", "ellipse-0", minorDiameter, false));
        }

        return BuildSketch([entity], constraints, plane);
    }

    public static JsonDocument BuildArc(
        double cxM,
        double cyM,
        double radiusM,
        double startAngleDeg,
        double endAngleDeg,
        string? plane = null)
    {
        var startAngleRad = startAngleDeg * Math.PI / 180.0;
        var endAngleRad = endAngleDeg * Math.PI / 180.0;
        var entity = new Dictionary<string, object?>
        {
            ["btType"] = CurveSegmentType,
            ["entityId"] = "arc-0",
            ["startPointId"] = "point-0",
            ["endPointId"] = "point-1",
            ["centerId"] = "point-center",
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
            ["startParam"] = N9(startAngleRad),
            ["endParam"] = N9(endAngleRad),
            ["isConstruction"] = false,
        };

        return BuildSketch([entity], Array.Empty<object>(), plane);
    }

    public static JsonDocument BuildPoint(
        double xM,
        double yM,
        string? plane = null,
        bool construction = false)
    {
        if (!double.IsFinite(xM))
        {
            throw new ArgumentOutOfRangeException(nameof(xM), "Point coordinate must be finite.");
        }

        if (!double.IsFinite(yM))
        {
            throw new ArgumentOutOfRangeException(nameof(yM), "Point coordinate must be finite.");
        }

        var entity = new Dictionary<string, object?>
        {
            ["btType"] = "BTMSketchPoint-158",
            ["entityId"] = "point-0",
            ["isUserPoint"] = true,
            ["isConstruction"] = construction,
            ["x"] = N9(xM),
            ["y"] = N9(yM),
        };

        return BuildSketch([entity], Array.Empty<object>(), plane);
    }

    public static JsonDocument BuildSpline(
        IReadOnlyList<SketchPoint> points,
        string? plane = null,
        bool construction = false)
    {
        ArgumentNullException.ThrowIfNull(points);
        if (points.Count < 3)
        {
            throw new ArgumentException("A spline requires at least three points.", nameof(points));
        }

        if (points.Any(point => !double.IsFinite(point.X) || !double.IsFinite(point.Y)))
        {
            throw new ArgumentException("Spline points must have finite coordinates.", nameof(points));
        }

        for (var index = 1; index < points.Count; index++)
        {
            if (points[index - 1] == points[index])
            {
                throw new ArgumentException("Spline points cannot be consecutive duplicates.", nameof(points));
            }
        }

        var start = points[0];
        var second = points[1];
        var penultimate = points[^2];
        var end = points[^1];
        var startDerivative = new SketchPoint(second.X - start.X, second.Y - start.Y);
        var endDerivative = new SketchPoint(end.X - penultimate.X, end.Y - penultimate.Y);
            var parameters = new List<Dictionary<string, object?>>
            {
                SplineBooleanParameter("geometryIsPeriodic", false),
                SplineBooleanParameter(".hasHandlesInSketch", true),
            };
            parameters.AddRange(points.Select((_, index) =>
                SplineBooleanParameter($".{index}.hasInternalHandle", index == 0 || index == points.Count - 1)));
            parameters.Add(SplineQuantityParameter("splinePointParamCount", "0.0"));
            parameters.Add(SplineQuantityParameter("splinePointCount", $"{points.Count}.0"));
        var entity = new Dictionary<string, object?>
        {
            ["btType"] = CurveSegmentType,
            ["entityId"] = "spline-0",
            ["startPointId"] = "spline-0.start",
            ["endPointId"] = "spline-0.end",
            ["internalIds"] = points
                .Select((_, index) => $"spline-0.{index}.internal")
                .Append("spline-0.startHandle")
                .Append("spline-0.endHandle")
                .ToArray(),
            ["geometry"] = new Dictionary<string, object?>
            {
                ["btType"] = "BTCurveGeometryInterpolatedSpline-116",
                ["isPeriodic"] = false,
                ["interpolationPoints"] = points
                    .SelectMany(point => new[] { N9(point.X), N9(point.Y) })
                    .ToArray(),
                ["startDerivativeX"] = N9(startDerivative.X),
                ["startDerivativeY"] = N9(startDerivative.Y),
                ["endDerivativeX"] = N9(endDerivative.X),
                ["endDerivativeY"] = N9(endDerivative.Y),
                ["startHandleX"] = N9(start.X + startDerivative.X / 3.0),
                ["startHandleY"] = N9(start.Y + startDerivative.Y / 3.0),
                ["endHandleX"] = N9(end.X - endDerivative.X / 3.0),
                ["endHandleY"] = N9(end.Y - endDerivative.Y / 3.0),
            },
            ["startParam"] = 0.0,
            ["endParam"] = 1.0,
                ["parameters"] = parameters,
            ["isConstruction"] = construction,
        };

        return BuildSketch([entity], Array.Empty<object>(), plane);
    }

    public static JsonDocument BuildSketch(
        IReadOnlyList<Dictionary<string, object?>> entities,
        IReadOnlyList<object> constraints,
        string? plane = null,
        bool disableImprinting = false)
    {
        var (operationId, deterministicId) = (plane ?? "Top").Trim().ToLowerInvariant() switch
        {
            "top" => ("TopplaneOp", "JDC"),
            "front" => ("FrontplaneOp", "JCC"),
            "right" => ("RightplaneOp", "JEC"),
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
        if (disableImprinting)
        {
            var feature = (Dictionary<string, object?>)envelope["feature"]!;
            var parameters = (object[])feature["parameters"]!;
            feature["parameters"] = parameters.Append(new Dictionary<string, object?>
            {
                ["btType"] = "BTMParameterBoolean-144",
                ["libraryRelationType"] = "DEFAULT",
                ["parameterId"] = "disableImprinting",
                ["parameterName"] = "",
                ["value"] = true,
            }).ToArray();
        }

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

    private static Dictionary<string, object?> PatternCircle(
        string entityId,
        string centerId,
        double cxM,
        double cyM,
        double radiusM,
        bool construction = false) => new()
    {
        ["btType"] = CurveType,
        ["entityId"] = entityId,
        ["centerId"] = centerId,
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
        ["isConstruction"] = construction,
        ["startParam"] = 0.0,
        ["endParam"] = 2.0 * Math.PI,
    };

    private static Dictionary<string, object?> CircularPatternConstraint(int instances)
    {
        var parameters = new List<Dictionary<string, object?>>
        {
            PatternQuantityParameter("patterng", "3.0", true),
            PatternQuantityParameter("patternc1", $"{instances}.0", true),
            PatternQuantityParameter("previouspatternc1", $"{instances}.0", true),
            PatternQuantityParameter("maximumpatterng", "3.0", true),
            PatternBooleanParameter("openPattern", false),
        };
        for (var instance = 0; instance < instances; instance++)
        {
            var id = instance == 0 ? "circular-op.seed" : $"circular-op.pattern.0.{instance}.0";
            parameters.Add(PatternStringParameter($"localInstance0,{instance}", id));
            parameters.Add(PatternStringParameter($"localInstance1,{instance}", $"{id}.start"));
            parameters.Add(PatternStringParameter($"localInstance2,{instance}", $"{id}.end"));
        }
        parameters.Add(PatternStringParameter("localPivot", "circular-op.pivotCircle.center"));
        parameters.Add(PatternQuantityParameter("labelDistance", "0.01*m", false));
        parameters.Add(PatternQuantityParameter("labelAngle", "0.0*rad", false));
        return new()
        {
            ["btType"] = "BTMSketchConstraint-2",
            ["entityId"] = "circular-op.pattern.pattern",
            ["constraintType"] = "CIRCULAR_PATTERN",
            ["parameters"] = parameters,
        };
    }

    private static Dictionary<string, object?> PatternDirectionLine(double x1M, double y1M, double x2M, double y2M)
    {
        var line = CurveSegment("pattern-op.direction1", "pattern-circle.center", "pattern-op.0.1.0.center", x1M, y1M, x2M, y2M);
        line["isConstruction"] = true;
        return line;
    }

    private static Dictionary<string, object?> PatternLengthConstraint(double spacingM) => new()
    {
        ["btType"] = "BTMSketchConstraint-2",
        ["entityId"] = "pattern-op.len1.length",
        ["constraintType"] = "LENGTH",
        ["parameters"] = new object[]
        {
            PatternStringParameter("localFirst", "pattern-op.direction1"),
            PatternEnumParameter("direction", "DimensionDirection", "MINIMUM"),
            PatternQuantityParameter("length", $"{spacingM.ToString("G9", CultureInfo.InvariantCulture)}*m", false),
            PatternEnumParameter("alignment", "DimensionAlignment", "ALIGNED"),
            PatternQuantityParameter("labelRatio", "0.5", false),
            PatternQuantityParameter("labelDistance", "0.01*m", false),
            PatternEnumParameter("sketchToolType", "SketchToolType", "PATTERN"),
        },
    };

    private static Dictionary<string, object?> LinearPatternConstraint(int columns)
    {
        var parameters = new List<Dictionary<string, object?>>
        {
            PatternQuantityParameter("patterng", "2.0", true),
            PatternQuantityParameter("patternc1", $"{columns}.0", true),
            PatternQuantityParameter("previouspatternc1", $"{columns}.0", true),
            PatternQuantityParameter("maximumpatterng", "2.0", true),
            PatternQuantityParameter("patternc2", "1.0", true),
            PatternQuantityParameter("previouspatternc2", "1.0", true),
        };
        for (var column = 0; column < columns; column++)
        {
            var entityId = column == 0 ? "pattern-circle" : $"pattern-op.0.{column}.0";
            var centerId = column == 0 ? "pattern-circle.center" : $"{entityId}.center";
            parameters.Add(PatternStringParameter($"localInstance0,{column},0", entityId));
            parameters.Add(PatternStringParameter($"localInstance1,{column},0", centerId));
        }
        parameters.Add(PatternStringParameter("localDirection1", "pattern-op.direction1"));
        parameters.Add(PatternQuantityParameter("labelDistance", "0.01*m", false));
        parameters.Add(PatternQuantityParameter("labelAngle", "-0.7853981633974483*rad", false));
        parameters.Add(PatternEnumParameter("sketchToolType", "SketchToolType", "PATTERN"));
        return new()
        {
            ["btType"] = "BTMSketchConstraint-2",
            ["entityId"] = "pattern-op.pattern",
            ["constraintType"] = "LINEAR_PATTERN",
            ["parameters"] = parameters,
        };
    }

    private static Dictionary<string, object?> PatternStringParameter(string parameterId, string value) => new()
    {
        ["btType"] = "BTMParameterString-149",
        ["parameterId"] = parameterId,
        ["value"] = value,
        ["libraryRelationType"] = "DEFAULT",
        ["parameterName"] = string.Empty,
    };

    private static Dictionary<string, object?> PatternBooleanParameter(string parameterId, bool value) => new()
    {
        ["btType"] = "BTMParameterBoolean-144",
        ["parameterId"] = parameterId,
        ["value"] = value,
        ["libraryRelationType"] = "DEFAULT",
        ["parameterName"] = string.Empty,
    };

    private static Dictionary<string, object?> PatternQuantityParameter(string parameterId, string expression, bool isInteger) => new()
    {
        ["btType"] = "BTMParameterQuantity-147",
        ["parameterId"] = parameterId,
        ["expression"] = expression,
        ["isInteger"] = isInteger,
        ["libraryRelationType"] = "DEFAULT",
        ["parameterName"] = string.Empty,
    };

    private static Dictionary<string, object?> PatternEnumParameter(string parameterId, string enumName, string value) => new()
    {
        ["btType"] = "BTMParameterEnum-145",
        ["parameterId"] = parameterId,
        ["enumName"] = enumName,
        ["value"] = value,
        ["libraryRelationType"] = "DEFAULT",
        ["parameterName"] = string.Empty,
    };

    private static Dictionary<string, object?> PointDictionary(SketchPoint point) => new()
    {
        ["x"] = N9(point.X),
        ["y"] = N9(point.Y),
    };

    private static void ValidateFinite(double value, string parameterName)
    {
        if (!double.IsFinite(value))
        {
            throw new ArgumentOutOfRangeException(parameterName, "Value must be finite.");
        }
    }

    private static void ValidateOptionalDimension(double? dimensionM, string parameterName)
    {
        if (dimensionM is double value && (!double.IsFinite(value) || value <= 0))
        {
            throw new ArgumentOutOfRangeException(parameterName, "Dimension must be finite and greater than zero.");
        }
    }

    private static Dictionary<string, object?> Constraint(
        string entityId,
        string constraintType,
        string localFirst,
        string? localSecond = null)
    {
        var parameters = new List<Dictionary<string, object?>>
        {
            StringParameter("localFirst", localFirst),
        };
        if (localSecond is not null)
        {
            parameters.Add(StringParameter("localSecond", localSecond));
        }

        return new()
        {
            ["btType"] = "BTMSketchConstraint-2",
            ["entityId"] = entityId,
            ["constraintType"] = constraintType,
            ["parameters"] = parameters,
        };
    }

    private static Dictionary<string, object?> StringParameter(string parameterId, string value) => new()
    {
        ["btType"] = "BTMParameterString-149",
        ["parameterId"] = parameterId,
        ["value"] = value,
    };

    private static Dictionary<string, object?> DimensionConstraint(
        string entityId,
        string constraintType,
        string localFirst,
        double dimensionM,
        bool includesDirectionAndAlignment)
    {
        var parameters = new List<Dictionary<string, object?>>
        {
            StringParameter("localFirst", localFirst),
            QuantityParameter("length", $"{dimensionM.ToString("G9", CultureInfo.InvariantCulture)}*m"),
        };
        if (includesDirectionAndAlignment)
        {
            parameters.Add(EnumParameter("direction", "DimensionDirection", "MINIMUM"));
            parameters.Add(EnumParameter("alignment", "DimensionAlignment", "ALIGNED"));
        }

        parameters.Add(QuantityParameter("labelRatio", "0.5"));
        parameters.Add(QuantityParameter("labelDistance", "0.01*m"));
        return new()
        {
            ["btType"] = "BTMSketchConstraint-2",
            ["entityId"] = entityId,
            ["constraintType"] = constraintType,
            ["parameters"] = parameters,
        };
    }

    private static Dictionary<string, object?> SplineBooleanParameter(string parameterId, bool value) => new()
        {
            ["btType"] = "BTMParameterBoolean-144",
            ["parameterId"] = parameterId,
            ["value"] = value,
            ["libraryRelationType"] = "DEFAULT",
            ["parameterName"] = string.Empty,
        };

        private static Dictionary<string, object?> SplineQuantityParameter(string parameterId, string expression) => new()
        {
            ["btType"] = "BTMParameterQuantity-147",
            ["parameterId"] = parameterId,
            ["expression"] = expression,
            ["isInteger"] = false,
            ["libraryRelationType"] = "DEFAULT",
            ["parameterName"] = string.Empty,
        };

        private static Dictionary<string, object?> QuantityParameter(string parameterId, string expression) => new()
    {
        ["btType"] = "BTMParameterQuantity-147",
        ["parameterId"] = parameterId,
        ["expression"] = expression,
        ["isInteger"] = false,
    };

    private static Dictionary<string, object?> EnumParameter(string parameterId, string enumName, string value) => new()
    {
        ["btType"] = "BTMParameterEnum-145",
        ["parameterId"] = parameterId,
        ["enumName"] = enumName,
        ["value"] = value,
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
