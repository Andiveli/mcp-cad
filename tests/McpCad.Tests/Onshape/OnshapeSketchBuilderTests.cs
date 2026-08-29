using System.Text.Json;
using McpCad.Onshape.Builders;

namespace McpCad.Tests.Onshape;

public class OnshapeSketchBuilderTests
{
    [Fact]
    public void BuildRectangle_ReturnsFourLineEntities()
    {
        using var doc = OnshapeSketchBuilder.BuildRectangle(0, 0, 0.1, 0.05);
        var feature = doc.RootElement.GetProperty("feature");
        feature.GetProperty("btType").GetString().Should().Be("BTMSketch-151");
        doc.RootElement.GetProperty("rejectMicroversionSkew").GetBoolean().Should().BeTrue();
        AssertSketchPlane(feature, "TopplaneOp", "JDC");

        var entities = feature.GetProperty("entities");
        entities.GetArrayLength().Should().Be(4);

        var expectedEndpoints = new[]
        {
            (Start: "point-0", End: "point-1"),
            (Start: "point-1", End: "point-2"),
            (Start: "point-2", End: "point-3"),
            (Start: "point-3", End: "point-0"),
        };

        for (var index = 0; index < entities.GetArrayLength(); index++)
        {
            var entity = entities[index];
            entity.GetProperty("btType").GetString().Should().Be("BTMSketchCurveSegment-155");
            entity.TryGetProperty("entityId", out _).Should().BeTrue();
            entity.GetProperty("startPointId").GetString().Should().Be(expectedEndpoints[index].Start);
            entity.GetProperty("endPointId").GetString().Should().Be(expectedEndpoints[index].End);
            entity.TryGetProperty("geometry", out _).Should().BeTrue();
            entity.GetProperty("geometry").GetProperty("btType").GetString().Should().Be("BTCurveGeometryLine-117");
            entity.TryGetProperty("startParam", out _).Should().BeTrue();
            entity.TryGetProperty("endParam", out _).Should().BeTrue();
            entity.GetProperty("isConstruction").GetBoolean().Should().BeFalse();
        }

        feature.GetProperty("constraints").GetArrayLength().Should().Be(0);
        entities.EnumerateArray().Should().NotContain(entity =>
            entity.GetProperty("btType").GetString() == "BTMPoint-210");
    }

    [Fact]
    public void BuildLine_ReturnsSingleLineEntityAndUsesRequestedPlane()
    {
        using var doc = OnshapeSketchBuilder.BuildLine(0, 0, 1, 0, "front");
        var feature = doc.RootElement.GetProperty("feature");
        var entities = feature.GetProperty("entities");
        entities.GetArrayLength().Should().Be(1);
        entities[0].GetProperty("btType").GetString().Should().Be("BTMSketchCurveSegment-155");
        AssertSketchPlane(feature, "FrontplaneOp", "JCC");
    }

    [Theory]
    [InlineData("Top", "x", "TopplaneOp", "JDC", 1, 0)]
    [InlineData("Front", "y", "FrontplaneOp", "JCC", 0, 1)]
    [InlineData("Right", "x", "RightplaneOp", "JEC", 1, 0)]
    public void BuildWorkAxis_ReturnsCenteredConstructionLineOnCanonicalPlane(
        string plane, string direction, string operationId, string deterministicId, double expectedXDirection, double expectedYDirection)
    {
        using var doc = OnshapeSketchBuilder.BuildWorkAxis(plane, direction);
        var feature = doc.RootElement.GetProperty("feature");
        var entities = feature.GetProperty("entities");

        feature.GetProperty("btType").GetString().Should().Be("BTMSketch-151");
        AssertSketchPlane(feature, operationId, deterministicId);
        feature.GetProperty("parameters")[1].GetProperty("parameterId").GetString().Should().Be("disableImprinting");
        feature.GetProperty("parameters")[1].GetProperty("value").GetBoolean().Should().BeTrue();
        entities.GetArrayLength().Should().Be(1);
        var axis = entities[0];
        axis.GetProperty("entityId").GetString().Should().Be("work-axis-0");
        axis.GetProperty("startPointId").GetString().Should().Be("work-axis-0.start");
        axis.GetProperty("endPointId").GetString().Should().Be("work-axis-0.end");
        axis.GetProperty("isConstruction").GetBoolean().Should().BeTrue();
        axis.GetProperty("geometry").GetProperty("pntX").GetDouble().Should().Be(0);
        axis.GetProperty("geometry").GetProperty("pntY").GetDouble().Should().Be(0);
        axis.GetProperty("geometry").GetProperty("dirX").GetDouble().Should().Be(expectedXDirection);
        axis.GetProperty("geometry").GetProperty("dirY").GetDouble().Should().Be(expectedYDirection);
        axis.GetProperty("startParam").GetDouble().Should().Be(-0.02);
        axis.GetProperty("endParam").GetDouble().Should().Be(0.02);
        feature.GetProperty("constraints").GetArrayLength().Should().Be(0);
    }

    [Fact]
    public void BuildLine_WithoutDimension_PreservesExistingPayload()
    {
        using var implicitNull = OnshapeSketchBuilder.BuildLine(0, 0, 0.1, 0, "front");
        using var explicitNull = OnshapeSketchBuilder.BuildLine(0, 0, 0.1, 0, "front", null);

        implicitNull.RootElement.GetRawText().Should().Be(explicitNull.RootElement.GetRawText());
        implicitNull.RootElement.GetProperty("feature").GetProperty("constraints").GetArrayLength().Should().Be(0);
    }

    [Fact]
    public void BuildLine_WithLength_EmitsLiveDerivedLengthConstraint()
    {
        using var doc = OnshapeSketchBuilder.BuildLine(0, 0, 0.1, 0, "front", 0.1);
        var feature = doc.RootElement.GetProperty("feature");

        AssertSketchPlane(feature, "FrontplaneOp", "JCC");
        var constraints = feature.GetProperty("constraints");
        constraints.GetArrayLength().Should().Be(1);
        AssertDimensionConstraint(constraints[0], "constraint-length-0", "LENGTH", "line-0", "0.1*m", true);
    }

    [Fact]
    public void BuildDimensions_UseInvariantMeterExpressions()
    {
        using var line = OnshapeSketchBuilder.BuildLine(0, 0, 0.0125, 0, lengthM: 0.0125);
        using var circle = OnshapeSketchBuilder.BuildCircle(0, 0, 0.00625, diameterM: 0.0125);

        line.RootElement.GetProperty("feature").GetProperty("constraints")[0]
            .GetProperty("parameters")[1].GetProperty("expression").GetString().Should().Be("0.0125*m");
        circle.RootElement.GetProperty("feature").GetProperty("constraints")[0]
            .GetProperty("parameters")[1].GetProperty("expression").GetString().Should().Be("0.0125*m");
    }

    [Fact]
    public void BuildCircle_ReturnsCircleEntityAndUsesRequestedPlane()
    {
        using var doc = OnshapeSketchBuilder.BuildCircle(0.05, 0.05, 0.02, "right");
        var feature = doc.RootElement.GetProperty("feature");
        var entities = feature.GetProperty("entities");
        entities.GetArrayLength().Should().Be(1);
        entities[0].GetProperty("btType").GetString().Should().Be("BTMSketchCurve-4");
        entities[0].GetProperty("geometry").GetProperty("btType").GetString().Should().Be("BTCurveGeometryCircle-115");
        entities[0].GetProperty("isConstruction").GetBoolean().Should().BeFalse();
        AssertSketchPlane(feature, "RightplaneOp", "JEC");
    }

    [Fact]
    public void BuildCircle_WithoutDimension_PreservesExistingPayload()
    {
        using var implicitNull = OnshapeSketchBuilder.BuildCircle(0.05, 0.05, 0.02, "right");
        using var explicitNull = OnshapeSketchBuilder.BuildCircle(0.05, 0.05, 0.02, "right", null);

        implicitNull.RootElement.GetRawText().Should().Be(explicitNull.RootElement.GetRawText());
        implicitNull.RootElement.GetProperty("feature").GetProperty("constraints").GetArrayLength().Should().Be(0);
    }

    [Fact]
    public void BuildCircle_WithDiameter_EmitsLiveDerivedDiameterConstraint()
    {
        using var doc = OnshapeSketchBuilder.BuildCircle(0.05, 0.05, 0.02, "right", 0.04);
        var feature = doc.RootElement.GetProperty("feature");

        AssertSketchPlane(feature, "RightplaneOp", "JEC");
        var constraints = feature.GetProperty("constraints");
        constraints.GetArrayLength().Should().Be(1);
        AssertDimensionConstraint(constraints[0], "constraint-diameter-0", "DIAMETER", "circle-0", "0.04*m", false);
    }

    [Fact]
    public void BuildEllipse_WithoutDimensions_EmitsReferenceEntityAndRequestedPlane()
    {
        using var doc = OnshapeSketchBuilder.BuildEllipse(0.05, -0.025, 0.04, 0.02, 0, "right");
        var feature = doc.RootElement.GetProperty("feature");
        var entities = feature.GetProperty("entities");
        entities.GetArrayLength().Should().Be(1);
        var entity = entities[0];
        var geometry = entity.GetProperty("geometry");

        entity.GetProperty("btType").GetString().Should().Be("BTMSketchCurve-4");
        entity.GetProperty("entityId").GetString().Should().Be("ellipse-0");
        entity.GetProperty("centerId").GetString().Should().Be("ellipse-0.center");
        entity.GetProperty("isConstruction").GetBoolean().Should().BeFalse();
        geometry.GetProperty("btType").GetString().Should().Be("BTCurveGeometryEllipse-1189");
        geometry.GetProperty("xCenter").GetDouble().Should().BeApproximately(0.05, 1e-9);
        geometry.GetProperty("yCenter").GetDouble().Should().BeApproximately(-0.025, 1e-9);
        geometry.GetProperty("radius").GetDouble().Should().BeApproximately(0.04, 1e-9);
        geometry.GetProperty("minorRadius").GetDouble().Should().BeApproximately(0.02, 1e-9);
        geometry.GetProperty("clockwise").GetBoolean().Should().BeFalse();
        feature.GetProperty("constraints").GetArrayLength().Should().Be(0);
        AssertSketchPlane(feature, "RightplaneOp", "JEC");
    }

    [Theory]
    [InlineData(0, 1, 0)]
    [InlineData(90, 0, 1)]
    [InlineData(30, 0.8660254037844386, 0.5)]
    [InlineData(450, 0, 1)]
    public void BuildEllipse_UsesRotationDegreesForUnitDirection(double rotationDeg, double expectedXDir, double expectedYDir)
    {
        using var doc = OnshapeSketchBuilder.BuildEllipse(0, 0, 0.04, 0.02, rotationDeg);
        var geometry = doc.RootElement.GetProperty("feature").GetProperty("entities")[0].GetProperty("geometry");

        geometry.GetProperty("xDir").GetDouble().Should().BeApproximately(expectedXDir, 1e-9);
        geometry.GetProperty("yDir").GetDouble().Should().BeApproximately(expectedYDir, 1e-9);
    }

    [Fact]
    public void BuildEllipse_AcceptsVeryLargeFiniteRotation()
    {
        using var doc = OnshapeSketchBuilder.BuildEllipse(0, 0, 0.04, 0.02, double.MaxValue);
        var geometry = doc.RootElement.GetProperty("feature").GetProperty("entities")[0].GetProperty("geometry");

        double.IsFinite(geometry.GetProperty("xDir").GetDouble()).Should().BeTrue();
        double.IsFinite(geometry.GetProperty("yDir").GetDouble()).Should().BeTrue();
    }

    [Fact]
    public void BuildEllipse_WithBothDiameterDimensions_EmitsOrderedDimensionConstraints()
    {
        using var doc = OnshapeSketchBuilder.BuildEllipse(0, 0, 0.04, 0.02, 0, majorDiameterM: 0.08, minorDiameterM: 0.04);
        var constraints = doc.RootElement.GetProperty("feature").GetProperty("constraints");

        constraints.GetArrayLength().Should().Be(2);
        AssertDimensionConstraint(constraints[0], "constraint-major-diameter-0", "MAJOR_DIAMETER", "ellipse-0", "0.08*m", false);
        AssertDimensionConstraint(constraints[1], "constraint-minor-diameter-0", "MINOR_DIAMETER", "ellipse-0", "0.04*m", false);
        constraints.EnumerateArray().Select(constraint => constraint.GetProperty("entityId").GetString()).Should().OnlyHaveUniqueItems();
    }

    [Theory]
    [InlineData(double.NaN, 0, 0.04, 0.02, 0)]
    [InlineData(0, double.PositiveInfinity, 0.04, 0.02, 0)]
    [InlineData(0, 0, 0, 0.02, 0)]
    [InlineData(0, 0, 0.01, 0.02, 0)]
    [InlineData(0, 0, 0.04, 0.02, double.NegativeInfinity)]
    public void BuildEllipse_RejectsInvalidGeometry(double cxM, double cyM, double majorRadiusM, double minorRadiusM, double rotationDeg)
    {
        var act = () => OnshapeSketchBuilder.BuildEllipse(cxM, cyM, majorRadiusM, minorRadiusM, rotationDeg);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-0.01)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void BuildEllipse_RejectsInvalidOptionalDimensions(double dimensionM)
    {
        var major = () => OnshapeSketchBuilder.BuildEllipse(0, 0, 0.04, 0.02, majorDiameterM: dimensionM);
        var minor = () => OnshapeSketchBuilder.BuildEllipse(0, 0, 0.04, 0.02, minorDiameterM: dimensionM);

        major.Should().Throw<ArgumentOutOfRangeException>();
        minor.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void BuildArc_UsesCircleGeometryWithCenterTopologyAndRadians()
    {
        using var doc = OnshapeSketchBuilder.BuildArc(0.05, 0.05, 0.02, 0, 90);
        var feature = doc.RootElement.GetProperty("feature");
        var entity = feature.GetProperty("entities")[0];

        entity.GetProperty("btType").GetString().Should().Be("BTMSketchCurveSegment-155");
        entity.GetProperty("startPointId").GetString().Should().Be("point-0");
        entity.GetProperty("endPointId").GetString().Should().Be("point-1");
        entity.GetProperty("centerId").GetString().Should().Be("point-center");
        entity.GetProperty("geometry").GetProperty("btType").GetString().Should().Be("BTCurveGeometryCircle-115");
        entity.GetProperty("startParam").GetDouble().Should().BeApproximately(0, 1e-9);
        entity.GetProperty("endParam").GetDouble().Should().BeApproximately(Math.PI / 2, 1e-8);
    }

    [Fact]
    public void BuildRectangle_NegativeCoordinates_NormalizesCorners()
    {
        using var doc = OnshapeSketchBuilder.BuildRectangle(0.1, 0.1, -0.1, -0.1);
        doc.RootElement.GetProperty("feature").GetProperty("entities").GetArrayLength().Should().Be(4);
    }

    [Fact]
    public void BuildRectangle_ZeroSize_StillEmitsFourEntities()
    {
        using var doc = OnshapeSketchBuilder.BuildRectangle(1, 2, 1, 2);
        doc.RootElement.GetProperty("feature").GetProperty("entities").GetArrayLength().Should().Be(4);
    }

    [Fact]
    public void Builder_Envelope_ContainsRejectMicroversionSkew()
    {
        using var doc = OnshapeSketchBuilder.BuildRectangle(0, 0, 1, 1);
        doc.RootElement.TryGetProperty("rejectMicroversionSkew", out var rms).Should().BeTrue();
        rms.GetBoolean().Should().BeTrue();
    }

    [Theory]
    [InlineData("Top", "TopplaneOp", "JDC")]
    [InlineData("front", "FrontplaneOp", "JCC")]
    [InlineData("RIGHT", "RightplaneOp", "JEC")]
    public void BuildRectangle_MapsNamedPlaneToSketchPlaneQuery(
        string plane,
        string operationId,
        string deterministicId)
    {
        using var doc = OnshapeSketchBuilder.BuildRectangle(0, 0, 1, 1, plane);

        AssertSketchPlane(doc.RootElement.GetProperty("feature"), operationId, deterministicId);
        doc.RootElement.TryGetProperty("plane", out _).Should().BeFalse();
    }

    [Fact]
    public void BuildRectangle_InvalidPlane_Throws()
    {
        var act = () => OnshapeSketchBuilder.BuildRectangle(0, 0, 1, 1, "XY");

        act.Should().Throw<ArgumentException>().WithMessage("*Top, Front, or Right*");
    }

    [Fact]
    public void BuildPolyline_Open_EmitsConnectedAxisConstrainedSegments()
    {
        using var doc = OnshapeSketchBuilder.BuildPolyline(
            [new(0, 0), new(0.1, 0), new(0.1, 0.05)], closed: false, plane: "front");
        var feature = doc.RootElement.GetProperty("feature");
        var entities = feature.GetProperty("entities");
        var constraints = feature.GetProperty("constraints");

        entities.GetArrayLength().Should().Be(2);
        entities[0].GetProperty("entityId").GetString().Should().Be("line-0");
        entities[0].GetProperty("startPointId").GetString().Should().Be("point-0-start");
        entities[0].GetProperty("endPointId").GetString().Should().Be("point-0-end");
        entities[1].GetProperty("entityId").GetString().Should().Be("line-1");
        constraints.GetArrayLength().Should().Be(3);
        AssertConstraint(constraints[0], "COINCIDENT", "line-0.end", "line-1.start");
        AssertConstraint(constraints[1], "HORIZONTAL", "line-0", null);
        AssertConstraint(constraints[2], "VERTICAL", "line-1", null);
        AssertSketchPlane(feature, "FrontplaneOp", "JCC");
    }

    [Fact]
    public void BuildPolyline_Closed_AddsFinalCoincidentConstraint()
    {
        using var doc = OnshapeSketchBuilder.BuildPolyline(
            [new(0, 0), new(0.1, 0), new(0.1, 0.05), new(0, 0.05)], closed: true);
        var constraints = doc.RootElement.GetProperty("feature").GetProperty("constraints");

        constraints.GetArrayLength().Should().Be(8);
        AssertConstraint(constraints[3], "COINCIDENT", "line-3.end", "line-0.start");
        constraints.EnumerateArray().Select(c => c.GetProperty("entityId").GetString()).Should().OnlyHaveUniqueItems();
    }

    [Theory]
    [InlineData(1, false)]
    [InlineData(2, true)]
    public void BuildPolyline_RejectsTooFewPoints(int pointCount, bool closed)
    {
        var points = Enumerable.Range(0, pointCount).Select(index => new SketchPoint(index, 0)).ToArray();

        var act = () => OnshapeSketchBuilder.BuildPolyline(points, closed);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void BuildPolyline_RejectsZeroLengthDiagonalAndNonFiniteSegments()
    {
        var zeroLength = () => OnshapeSketchBuilder.BuildPolyline([new(0, 0), new(0, 0)]);
        var diagonal = () => OnshapeSketchBuilder.BuildPolyline([new(0, 0), new(1, 1)]);
        var nonFinite = () => OnshapeSketchBuilder.BuildPolyline([new(0, 0), new(double.NaN, 0)]);

        zeroLength.Should().Throw<ArgumentException>();
        diagonal.Should().Throw<ArgumentException>();
        nonFinite.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void BuildPoint_EmitsStandalonePointEntityWithRequestedPlaneAndConstructionFlag(bool construction)
    {
        using var doc = OnshapeSketchBuilder.BuildPoint(0.0125, -0.025, "right", construction);
        var feature = doc.RootElement.GetProperty("feature");
        var entities = feature.GetProperty("entities");

        entities.GetArrayLength().Should().Be(1);
        var point = entities[0];
        point.GetProperty("btType").GetString().Should().Be("BTMSketchPoint-158");
        point.GetProperty("entityId").GetString().Should().Be("point-0");
        point.GetProperty("entityId").GetString().Should().NotBeNullOrWhiteSpace();
        point.GetProperty("isUserPoint").GetBoolean().Should().BeTrue();
        point.GetProperty("isConstruction").GetBoolean().Should().Be(construction);
        point.GetProperty("x").GetDouble().Should().BeApproximately(0.0125, 1e-9);
        point.GetProperty("y").GetDouble().Should().BeApproximately(-0.025, 1e-9);
        point.TryGetProperty("geometry", out _).Should().BeFalse();
        point.TryGetProperty("startPointId", out _).Should().BeFalse();
        point.TryGetProperty("endPointId", out _).Should().BeFalse();
        feature.GetProperty("constraints").GetArrayLength().Should().Be(0);
        AssertSketchPlane(feature, "RightplaneOp", "JEC");
    }

    [Theory]
    [InlineData(double.NaN, 0)]
    [InlineData(0, double.PositiveInfinity)]
    public void BuildPoint_RejectsNonFiniteCoordinates(double xM, double yM)
    {
        var act = () => OnshapeSketchBuilder.BuildPoint(xM, yM);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void BuildSpline_EmitsNonPeriodicInterpolatedSplineWithEndpointHandles()
    {
        using var doc = OnshapeSketchBuilder.BuildSpline(
            [new(0, 0), new(0.03, 0.02), new(0.06, 0)],
            "front",
            construction: true);
        var feature = doc.RootElement.GetProperty("feature");
        var entity = feature.GetProperty("entities")[0];
        var geometry = entity.GetProperty("geometry");

        entity.GetProperty("btType").GetString().Should().Be("BTMSketchCurveSegment-155");
        entity.GetProperty("entityId").GetString().Should().Be("spline-0");
        entity.GetProperty("startPointId").GetString().Should().Be("spline-0.start");
        entity.GetProperty("endPointId").GetString().Should().Be("spline-0.end");
        entity.GetProperty("startParam").GetDouble().Should().Be(0);
        entity.GetProperty("endParam").GetDouble().Should().Be(1);
        entity.GetProperty("isConstruction").GetBoolean().Should().BeTrue();
            var parameters = entity.GetProperty("parameters");
            parameters.GetArrayLength().Should().Be(7);
            AssertSplineBooleanParameter(parameters[0], "geometryIsPeriodic", false);
            AssertSplineBooleanParameter(parameters[1], ".hasHandlesInSketch", true);
            AssertSplineBooleanParameter(parameters[2], ".0.hasInternalHandle", true);
            AssertSplineBooleanParameter(parameters[3], ".1.hasInternalHandle", false);
            AssertSplineBooleanParameter(parameters[4], ".2.hasInternalHandle", true);
            AssertSplineQuantityParameter(parameters[5], "splinePointParamCount", "0.0");
            AssertSplineQuantityParameter(parameters[6], "splinePointCount", "3.0");
        geometry.GetProperty("btType").GetString().Should().Be("BTCurveGeometryInterpolatedSpline-116");
        geometry.GetProperty("isPeriodic").GetBoolean().Should().BeFalse();
        geometry.GetProperty("interpolationPoints").EnumerateArray()
            .Select(value => value.GetDouble())
            .Should().Equal(0, 0, 0.03, 0.02, 0.06, 0);
        geometry.GetProperty("startDerivativeX").GetDouble().Should().BeApproximately(0.03, 1e-9);
        geometry.GetProperty("startDerivativeY").GetDouble().Should().BeApproximately(0.02, 1e-9);
        geometry.GetProperty("endDerivativeX").GetDouble().Should().BeApproximately(0.03, 1e-9);
        geometry.GetProperty("endDerivativeY").GetDouble().Should().BeApproximately(-0.02, 1e-9);
        geometry.GetProperty("startHandleX").GetDouble().Should().BeApproximately(0.01, 1e-9);
        geometry.GetProperty("startHandleY").GetDouble().Should().BeApproximately(0.006666666666666667, 1e-9);
        geometry.GetProperty("endHandleX").GetDouble().Should().BeApproximately(0.05, 1e-9);
        geometry.GetProperty("endHandleY").GetDouble().Should().BeApproximately(0.006666666666666667, 1e-9);
        entity.GetProperty("internalIds")[0].GetString().Should().Be("spline-0.0.internal");
        entity.GetProperty("internalIds")[1].GetString().Should().Be("spline-0.1.internal");
        entity.GetProperty("internalIds")[2].GetString().Should().Be("spline-0.2.internal");
        entity.GetProperty("internalIds")[3].GetString().Should().Be("spline-0.startHandle");
        entity.GetProperty("internalIds")[4].GetString().Should().Be("spline-0.endHandle");
        foreach (var oldProperty in new[] { "periodic", "points", "startDerivative", "endDerivative", "startHandle", "endHandle", "internalIds" })
        {
            geometry.TryGetProperty(oldProperty, out _).Should().BeFalse();
        }
        geometry.TryGetProperty("splinePointCount", out _).Should().BeFalse();
        AssertSketchPlane(feature, "FrontplaneOp", "JCC");
    }

        [Fact]
        public void BuildSpline_EmitsInternalHandlesOnlyForEndpoints()
        {
            using var doc = OnshapeSketchBuilder.BuildSpline(
                [new(0, 0), new(0.01, 0.01), new(0.02, 0.01), new(0.03, 0)]);
            var parameters = doc.RootElement.GetProperty("feature").GetProperty("entities")[0].GetProperty("parameters");

            parameters.GetArrayLength().Should().Be(8);
            AssertSplineBooleanParameter(parameters[2], ".0.hasInternalHandle", true);
            AssertSplineBooleanParameter(parameters[3], ".1.hasInternalHandle", false);
            AssertSplineBooleanParameter(parameters[4], ".2.hasInternalHandle", false);
            AssertSplineBooleanParameter(parameters[5], ".3.hasInternalHandle", true);
            AssertSplineQuantityParameter(parameters[6], "splinePointParamCount", "0.0");
            AssertSplineQuantityParameter(parameters[7], "splinePointCount", "4.0");
        }

        [Theory]
        [InlineData(2)]
        [InlineData(3)]
        public void BuildSpline_RejectsTooFewOrConsecutiveDuplicatePoints(int pointCount)
    {
        var points = Enumerable.Range(0, pointCount)
            .Select(index => new SketchPoint(index, 0))
            .ToArray();
        if (pointCount == 3)
        {
            points[2] = points[1];
        }

        var act = () => OnshapeSketchBuilder.BuildSpline(points);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void BuildSpline_RejectsNonFinitePoints()
    {
        var act = () => OnshapeSketchBuilder.BuildSpline(
            [new(0, 0), new(double.NaN, 0), new(1, 0)]);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void BuildLinearPatternCircle_EmitsOneRowLiveReferenceTopologyAndConstraints()
    {
        using var doc = OnshapeSketchBuilder.BuildLinearPatternCircle(0.02, -0.01, 0.003, 3, 0.015, "front");
        var feature = doc.RootElement.GetProperty("feature");
        var entities = feature.GetProperty("entities");
        var constraints = feature.GetProperty("constraints");

        entities.GetArrayLength().Should().Be(4);
        AssertPatternCircle(entities[0], "pattern-circle", "pattern-circle.center", 0.02);
        AssertPatternCircle(entities[1], "pattern-op.0.1.0", "pattern-op.0.1.0.center", 0.035);
        AssertPatternCircle(entities[2], "pattern-op.0.2.0", "pattern-op.0.2.0.center", 0.05);
        entities[3].GetProperty("entityId").GetString().Should().Be("pattern-op.direction1");
        entities[3].GetProperty("btType").GetString().Should().Be("BTMSketchCurveSegment-155");
        entities[3].GetProperty("startPointId").GetString().Should().Be("pattern-circle.center");
        entities[3].GetProperty("endPointId").GetString().Should().Be("pattern-op.0.1.0.center");
        entities[3].GetProperty("isConstruction").GetBoolean().Should().BeTrue();

        constraints.GetArrayLength().Should().Be(2);
        AssertPatternLengthConstraint(constraints[0]);
        AssertLinearPatternConstraint(constraints[1], 3);
        AssertSketchPlane(feature, "FrontplaneOp", "JCC");
    }

    [Theory]
    [InlineData(double.NaN, 0, 0.003, 2, 0.015)]
    [InlineData(0, 0, 0, 2, 0.015)]
    [InlineData(0, 0, 0.003, 1, 0.015)]
    [InlineData(0, 0, 0.003, 101, 0.015)]
    [InlineData(0, 0, 0.003, 2, double.PositiveInfinity)]
    public void BuildLinearPatternCircle_RejectsInvalidInput(double cxM, double cyM, double radiusM, int columns, double spacingM)
    {
        var act = () => OnshapeSketchBuilder.BuildLinearPatternCircle(cxM, cyM, radiusM, columns, spacingM);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    private static void AssertPatternCircle(JsonElement entity, string id, string centerId, double xCenter, double yCenter = -0.01, double radius = 0.003)
    {
        entity.GetProperty("btType").GetString().Should().Be("BTMSketchCurve-4");
        entity.GetProperty("entityId").GetString().Should().Be(id);
        entity.GetProperty("centerId").GetString().Should().Be(centerId);
        entity.GetProperty("geometry").GetProperty("btType").GetString().Should().Be("BTCurveGeometryCircle-115");
        entity.GetProperty("geometry").GetProperty("xCenter").GetDouble().Should().BeApproximately(xCenter, 1e-9);
        entity.GetProperty("geometry").GetProperty("yCenter").GetDouble().Should().BeApproximately(yCenter, 1e-9);
        entity.GetProperty("geometry").GetProperty("radius").GetDouble().Should().BeApproximately(radius, 1e-9);
    }

    private static void AssertPatternLengthConstraint(JsonElement constraint)
    {
        constraint.GetProperty("entityId").GetString().Should().Be("pattern-op.len1.length");
        constraint.GetProperty("constraintType").GetString().Should().Be("LENGTH");
        var parameters = constraint.GetProperty("parameters");
        parameters.EnumerateArray().Select(parameter => parameter.GetProperty("parameterId").GetString()).Should()
            .Equal("localFirst", "direction", "length", "alignment", "labelRatio", "labelDistance", "sketchToolType");
        parameters[0].GetProperty("value").GetString().Should().Be("pattern-op.direction1");
        parameters[1].GetProperty("value").GetString().Should().Be("MINIMUM");
        parameters[2].GetProperty("expression").GetString().Should().Be("0.015*m");
        parameters[3].GetProperty("value").GetString().Should().Be("ALIGNED");
        parameters[6].GetProperty("value").GetString().Should().Be("PATTERN");
        AssertPatternParameterMetadata(parameters);
    }

    private static void AssertLinearPatternConstraint(JsonElement constraint, int columns)
    {
        constraint.GetProperty("entityId").GetString().Should().Be("pattern-op.pattern");
        constraint.GetProperty("constraintType").GetString().Should().Be("LINEAR_PATTERN");
        var parameters = constraint.GetProperty("parameters");
        parameters.EnumerateArray().Select(parameter => parameter.GetProperty("parameterId").GetString()).Should().Equal(
            "patterng", "patternc1", "previouspatternc1", "maximumpatterng", "patternc2", "previouspatternc2",
            "localInstance0,0,0", "localInstance1,0,0", "localInstance0,1,0", "localInstance1,1,0", "localInstance0,2,0", "localInstance1,2,0",
            "localDirection1", "labelDistance", "labelAngle", "sketchToolType");
        parameters.EnumerateArray().Take(6).Select(parameter => parameter.GetProperty("expression").GetString()).Should()
            .Equal("2.0", $"{columns}.0", $"{columns}.0", "2.0", "1.0", "1.0");
        parameters.EnumerateArray().Take(6).Should().OnlyContain(parameter => parameter.GetProperty("isInteger").GetBoolean());
        parameters[6].GetProperty("value").GetString().Should().Be("pattern-circle");
        parameters[7].GetProperty("value").GetString().Should().Be("pattern-circle.center");
        parameters[10].GetProperty("value").GetString().Should().Be("pattern-op.0.2.0");
        parameters[11].GetProperty("value").GetString().Should().Be("pattern-op.0.2.0.center");
        parameters[12].GetProperty("value").GetString().Should().Be("pattern-op.direction1");
        parameters[13].GetProperty("expression").GetString().Should().Be("0.01*m");
        parameters[14].GetProperty("expression").GetString().Should().Be("-0.7853981633974483*rad");
        parameters[15].GetProperty("value").GetString().Should().Be("PATTERN");
        AssertPatternParameterMetadata(parameters);
    }

    private static void AssertPatternParameterMetadata(JsonElement parameters)
    {
        parameters.EnumerateArray().Should().OnlyContain(parameter =>
            parameter.GetProperty("libraryRelationType").GetString() == "DEFAULT" &&
            parameter.GetProperty("parameterName").GetString() == string.Empty);
    }

    [Fact]
    public void BuildCircularPatternLine_EmitsReferenceTopologyRotationAndParameterOrder()
    {
        using var doc = OnshapeSketchBuilder.BuildCircularPatternLine(0.02, -0.01, 0.03, 0.008, 3, "front");
        var feature = doc.RootElement.GetProperty("feature");
        var entities = feature.GetProperty("entities");
        var constraints = feature.GetProperty("constraints");

        entities.GetArrayLength().Should().Be(4);
        constraints.GetArrayLength().Should().Be(1);
        var constraint = constraints[0];
        AssertPatternCircle(entities[0], "circular-op.pivotCircle", "circular-op.pivotCircle.center", 0.02, -0.01, 0.03);
        entities[0].GetProperty("isConstruction").GetBoolean().Should().BeTrue();
        AssertCircularPatternLine(entities[1], "circular-op.seed", 0.05, -0.01, 0, 1);
        AssertCircularPatternLine(entities[2], "circular-op.pattern.0.1.0", 0.005, 0.015980762, -0.866025404, -0.5);
        AssertCircularPatternLine(entities[3], "circular-op.pattern.0.2.0", 0.005, -0.035980762, 0.866025404, -0.5);
        AssertCircularPatternConstraint(constraint, 3);
        AssertSketchPlane(feature, "FrontplaneOp", "JCC");
    }

    [Fact]
    public void BuildCircularPatternLine_UsesRequestedInstanceCountForClosedDistribution()
    {
        using var doc = OnshapeSketchBuilder.BuildCircularPatternLine(0, 0, 0.02, 0.01, 4);
        var feature = doc.RootElement.GetProperty("feature");
        var entities = feature.GetProperty("entities");
        var parameters = feature.GetProperty("constraints")[0].GetProperty("parameters");

        entities.GetArrayLength().Should().Be(5);
        AssertCircularPatternLine(entities[4], "circular-op.pattern.0.3.0", 0, -0.02, 1, 0, 0.005);
        parameters.GetArrayLength().Should().Be(20);
        parameters[14].GetProperty("value").GetString().Should().Be("circular-op.pattern.0.3.0");
    }

    [Theory]
    [InlineData(double.NaN, 0, 0.03, 0.008, 2)]
    [InlineData(0, 0, 0, 0.008, 2)]
    [InlineData(0, 0, 0.03, 0, 2)]
    [InlineData(0, 0, 0.03, 0.008, 1)]
    [InlineData(0, double.PositiveInfinity, 0.03, 0.008, 2)]
    [InlineData(0, 0, double.PositiveInfinity, 0.008, 2)]
    [InlineData(0, 0, 0.03, 0.008, 101)]
    public void BuildCircularPatternLine_RejectsInvalidInput(double centerXM, double centerYM, double orbitRadiusM, double lineLengthM, int instances)
    {
        var act = () => OnshapeSketchBuilder.BuildCircularPatternLine(centerXM, centerYM, orbitRadiusM, lineLengthM, instances);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    private static void AssertCircularPatternLine(JsonElement entity, string id, double x, double y, double dirX, double dirY, double halfLength = 0.004)
    {
        entity.GetProperty("entityId").GetString().Should().Be(id);
        entity.GetProperty("startPointId").GetString().Should().Be($"{id}.start");
        entity.GetProperty("endPointId").GetString().Should().Be($"{id}.end");
        var geometry = entity.GetProperty("geometry");
        geometry.GetProperty("pntX").GetDouble().Should().BeApproximately(x, 1e-9);
        geometry.GetProperty("pntY").GetDouble().Should().BeApproximately(y, 1e-9);
        geometry.GetProperty("dirX").GetDouble().Should().BeApproximately(dirX, 1e-9);
        geometry.GetProperty("dirY").GetDouble().Should().BeApproximately(dirY, 1e-9);
        entity.GetProperty("startParam").GetDouble().Should().BeApproximately(-halfLength, 1e-9);
        entity.GetProperty("endParam").GetDouble().Should().BeApproximately(halfLength, 1e-9);
        entity.GetProperty("isConstruction").GetBoolean().Should().BeFalse();
    }

    private static void AssertCircularPatternConstraint(JsonElement constraint, int instances)
    {
        constraint.GetProperty("entityId").GetString().Should().Be("circular-op.pattern.pattern");
        constraint.GetProperty("constraintType").GetString().Should().Be("CIRCULAR_PATTERN");
        var parameters = constraint.GetProperty("parameters");
        parameters.EnumerateArray().Select(parameter => parameter.GetProperty("parameterId").GetString()).Should().Equal(
            "patterng", "patternc1", "previouspatternc1", "maximumpatterng", "openPattern",
            "localInstance0,0", "localInstance1,0", "localInstance2,0",
            "localInstance0,1", "localInstance1,1", "localInstance2,1",
            "localInstance0,2", "localInstance1,2", "localInstance2,2",
            "localPivot", "labelDistance", "labelAngle");
        parameters.EnumerateArray().Take(4).Select(parameter => parameter.GetProperty("expression").GetString()).Should().Equal("3.0", $"{instances}.0", $"{instances}.0", "3.0");
        parameters.EnumerateArray().Take(4).Should().OnlyContain(parameter => parameter.GetProperty("isInteger").GetBoolean());
        parameters[4].GetProperty("value").GetBoolean().Should().BeFalse();
        parameters[5].GetProperty("value").GetString().Should().Be("circular-op.seed");
        parameters[8].GetProperty("value").GetString().Should().Be("circular-op.pattern.0.1.0");
        parameters[14].GetProperty("value").GetString().Should().Be("circular-op.pivotCircle.center");
        parameters[15].GetProperty("expression").GetString().Should().Be("0.01*m");
        parameters[16].GetProperty("expression").GetString().Should().Be("0.0*rad");
        parameters.EnumerateArray().Should().OnlyContain(parameter =>
            parameter.GetProperty("libraryRelationType").GetString() == "DEFAULT" &&
            parameter.GetProperty("parameterName").GetString() == string.Empty);
        foreach (var parameter in parameters.EnumerateArray())
        {
            parameter.TryGetProperty("nodeId", out _).Should().BeFalse();
            parameter.TryGetProperty("units", out _).Should().BeFalse();
        }
        parameters.EnumerateArray().Should().NotContain(parameter => parameter.GetProperty("parameterId").GetString() == "sketchToolType");
    }

    private static void AssertDimensionConstraint(
        JsonElement constraint,
        string entityId,
        string type,
        string localFirst,
        string expression,
        bool expectsDirectionAndAlignment)
    {
        constraint.GetProperty("btType").GetString().Should().Be("BTMSketchConstraint-2");
        constraint.GetProperty("entityId").GetString().Should().Be(entityId);
        constraint.GetProperty("entityId").GetString().Should().NotBeNullOrWhiteSpace();
        constraint.GetProperty("constraintType").GetString().Should().Be(type);
        var parameters = constraint.GetProperty("parameters");
        parameters[0].GetProperty("btType").GetString().Should().Be("BTMParameterString-149");
        parameters[0].GetProperty("parameterId").GetString().Should().Be("localFirst");
        parameters[0].GetProperty("value").GetString().Should().Be(localFirst);
        parameters[1].GetProperty("btType").GetString().Should().Be("BTMParameterQuantity-147");
        parameters[1].GetProperty("parameterId").GetString().Should().Be("length");
        parameters[1].GetProperty("expression").GetString().Should().Be(expression);
        parameters[1].GetProperty("isInteger").GetBoolean().Should().BeFalse();

        var expectedParameterCount = expectsDirectionAndAlignment ? 6 : 4;
        parameters.GetArrayLength().Should().Be(expectedParameterCount);
        var labelRatioIndex = expectsDirectionAndAlignment ? 3 : 2;
        if (expectsDirectionAndAlignment)
        {
            parameters[2].GetProperty("btType").GetString().Should().Be("BTMParameterEnum-145");
            parameters[2].GetProperty("parameterId").GetString().Should().Be("direction");
            parameters[2].GetProperty("enumName").GetString().Should().Be("DimensionDirection");
            parameters[2].GetProperty("value").GetString().Should().Be("MINIMUM");
            parameters[3].GetProperty("btType").GetString().Should().Be("BTMParameterEnum-145");
            parameters[3].GetProperty("parameterId").GetString().Should().Be("alignment");
            parameters[3].GetProperty("enumName").GetString().Should().Be("DimensionAlignment");
            parameters[3].GetProperty("value").GetString().Should().Be("ALIGNED");
            labelRatioIndex = 4;
        }

        parameters[labelRatioIndex].GetProperty("btType").GetString().Should().Be("BTMParameterQuantity-147");
        parameters[labelRatioIndex].GetProperty("parameterId").GetString().Should().Be("labelRatio");
        parameters[labelRatioIndex].GetProperty("expression").GetString().Should().Be("0.5");
        parameters[labelRatioIndex + 1].GetProperty("btType").GetString().Should().Be("BTMParameterQuantity-147");
        parameters[labelRatioIndex + 1].GetProperty("parameterId").GetString().Should().Be("labelDistance");
        parameters[labelRatioIndex + 1].GetProperty("expression").GetString().Should().Be("0.01*m");
    }

    private static void AssertSplineBooleanParameter(JsonElement parameter, string parameterId, bool value)
        {
            parameter.GetProperty("btType").GetString().Should().Be("BTMParameterBoolean-144");
            parameter.GetProperty("parameterId").GetString().Should().Be(parameterId);
            parameter.GetProperty("value").GetBoolean().Should().Be(value);
            parameter.GetProperty("libraryRelationType").GetString().Should().Be("DEFAULT");
            parameter.GetProperty("parameterName").GetString().Should().BeEmpty();
        }

        private static void AssertSplineQuantityParameter(JsonElement parameter, string parameterId, string expression)
        {
            parameter.GetProperty("btType").GetString().Should().Be("BTMParameterQuantity-147");
            parameter.GetProperty("parameterId").GetString().Should().Be(parameterId);
            parameter.GetProperty("expression").GetString().Should().Be(expression);
            parameter.GetProperty("isInteger").GetBoolean().Should().BeFalse();
            parameter.GetProperty("libraryRelationType").GetString().Should().Be("DEFAULT");
            parameter.GetProperty("parameterName").GetString().Should().BeEmpty();
        }

        private static void AssertConstraint(JsonElement constraint, string type, string first, string? second)
    {
        constraint.GetProperty("btType").GetString().Should().Be("BTMSketchConstraint-2");
        constraint.GetProperty("entityId").GetString().Should().NotBeNullOrWhiteSpace();
        constraint.GetProperty("constraintType").GetString().Should().Be(type);
        var parameters = constraint.GetProperty("parameters");
        parameters[0].GetProperty("btType").GetString().Should().Be("BTMParameterString-149");
        parameters[0].GetProperty("parameterId").GetString().Should().Be("localFirst");
        parameters[0].GetProperty("value").GetString().Should().Be(first);
        if (second is not null)
        {
            parameters[1].GetProperty("parameterId").GetString().Should().Be("localSecond");
            parameters[1].GetProperty("value").GetString().Should().Be(second);
        }
    }

    private static void AssertSketchPlane(JsonElement feature, string operationId, string deterministicId)
    {
        var parameters = feature.GetProperty("parameters");
        parameters.GetArrayLength().Should().BeGreaterThan(0);
        var plane = parameters[0];
        plane.GetProperty("btType").GetString().Should().Be("BTMParameterQueryList-148");
        plane.GetProperty("parameterId").GetString().Should().Be("sketchPlane");
        plane.GetProperty("filter").GetProperty("btType").GetString().Should().Be("BTOrFilter-167");

        var query = plane.GetProperty("queries")[0];
        query.GetProperty("btType").GetString().Should().Be("BTMIndividualQuery-138");
        query.GetProperty("queryString").GetString().Should().Contain($"${operationId}");
        query.GetProperty("deterministicIds")[0].GetString().Should().Be(deterministicId);
    }
}
