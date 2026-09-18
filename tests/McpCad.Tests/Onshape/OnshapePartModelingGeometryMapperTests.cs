using McpCad.Core.Modeling;
using McpCad.Onshape.Modeling;

namespace McpCad.Tests.Onshape;

public sealed class OnshapePartModelingGeometryMapperTests
{
    [Theory]
    [MemberData(nameof(MeasuredFrames))]
    public void Map_MapsEveryCanonicalFrameWithAllFourRectangleCorners(
        CanonicalPlane plane, double xx, double xy, double xz, double yx, double yy, double yz, double nx, double ny, double nz)
    {
        var definition = Rectangle(plane, new Point2(0.02, -0.01), new Point2(0.07, 0.03));
        var frame = Frame((xx, xy, xz), (yx, yy, yz), (nx, ny, nz));

        var mapped = OnshapePartModelingGeometryMapper.Map(definition, Extrusion(ExtrusionDirection.Positive), frame);

        var rectangle = mapped.Profile.Should().BeOfType<OnshapeMappedRectangle>().Subject;
        rectangle.CornersM.Should().HaveCount(4);
        AssertWorldCorners(rectangle.CornersM, plane, definition.Profile.Should().BeOfType<RectangleProfile>().Subject, frame);
        mapped.Extrusion.Should().Be(new OnshapeMappedExtrusion(0.08, ExtrusionDirection.Positive));
    }

    [Fact]
    public void Map_RecoversEveryWorldVertexFromAnArbitrarilyRotatedNativeFrame()
    {
        const double rootHalf = 0.7071067811865476;
        var definition = Rectangle(CanonicalPlane.Top, new Point2(-0.03, 0.01), new Point2(0.04, 0.06));
        var frame = Frame((rootHalf, rootHalf, 0), (-rootHalf, rootHalf, 0), (0, 0, 1));

        var mapped = OnshapePartModelingGeometryMapper.Map(definition, Extrusion(ExtrusionDirection.Positive), frame);

        var rectangle = mapped.Profile.Should().BeOfType<OnshapeMappedRectangle>().Subject;
        AssertWorldCorners(rectangle.CornersM, CanonicalPlane.Top, definition.Profile.Should().BeOfType<RectangleProfile>().Subject, frame);
        rectangle.CornersM.Select(point => point.X).Distinct().Should().HaveCountGreaterThan(2);
    }

    [Fact]
    public void Map_MapsOffOriginCircleCenterAndPreservesMeterRadius()
    {
        var definition = new SketchDefinition(
            CanonicalPlane.Front,
            LengthUnit.Meter,
            new CircleProfile(new Point2(0.06, -0.04), 0.015));
        var frame = Frame((0, 0, 1), (1, 0, 0), (0, 1, 0));

        var mapped = OnshapePartModelingGeometryMapper.Map(definition, Extrusion(ExtrusionDirection.Positive), frame);

        var circle = mapped.Profile.Should().BeOfType<OnshapeMappedCircle>().Subject;
        AssertWorldNear(NativeToWorld(circle.CenterM, frame), new WorldPoint(0.06, 0, -0.04));
        circle.RadiusM.Should().Be(0.015);
    }

    [Theory]
    [InlineData(ExtrusionDirection.Positive, ExtrusionDirection.Negative)]
    [InlineData(ExtrusionDirection.Negative, ExtrusionDirection.Positive)]
    public void Map_FlipsNativeDirectionOnlyWhenTheNativeNormalIsInverted(
        ExtrusionDirection requested, ExtrusionDirection expected)
    {
        var frame = Frame((1, 0, 0), (0, -1, 0), (0, 0, -1));

        var mapped = OnshapePartModelingGeometryMapper.Map(Rectangle(CanonicalPlane.Top, new Point2(0, 0), new Point2(0.02, 0.01)), Extrusion(requested), frame);

        mapped.Extrusion.Direction.Should().Be(expected);
    }

    [Theory]
    [InlineData("translated")]
    [InlineData("skew")]
    [InlineData("degenerate")]
    [InlineData("wrong-plane")]
    public void Map_RejectsUnsupportedFramesWithTypedFailure(string invalid)
    {
        var frame = invalid switch
        {
            "translated" => new OnshapePartModelingFrame(new OnshapeFrameVector(0.001, 0, 0), new OnshapeFrameVector(1, 0, 0), new OnshapeFrameVector(0, 1, 0), new OnshapeFrameVector(0, 0, 1)),
            "skew" => Frame((1, 0, 0), (0.1, 1, 0), (0, 0, 1)),
            "degenerate" => Frame((1, 0, 0), (0, 0, 0), (0, 0, 1)),
            "wrong-plane" => Frame((1, 0, 0), (0, 1, 0), (0, 0, 1)),
            _ => throw new ArgumentOutOfRangeException(nameof(invalid)),
        };

        var act = () => OnshapePartModelingGeometryMapper.Map(
            Rectangle(invalid == "wrong-plane" ? CanonicalPlane.Front : CanonicalPlane.Top, new Point2(0, 0), new Point2(0.02, 0.01)),
            Extrusion(ExtrusionDirection.Positive),
            frame);

        act.Should().Throw<OnshapePartModelingGeometryMapException>().Which.Failure.ToString()
            .Should().Be(nameof(OnshapePartModelingGeometryMapFailure.UnsupportedFrame));
    }

    [Fact]
    public void Map_RejectsMalformedNormalizedInputsWithoutGuessingDefaults()
    {
        var frame = Frame((1, 0, 0), (0, 1, 0), (0, 0, 1));
        var invalidUnit = new SketchDefinition(CanonicalPlane.Top, LengthUnit.Centimeter, new CircleProfile(new Point2(0, 0), 0.01));
        var invalidDirection = new ExtrusionDefinition(0.08, LengthUnit.Meter, (ExtrusionDirection)99);

        Action nullSketch = () => OnshapePartModelingGeometryMapper.Map(null!, Extrusion(ExtrusionDirection.Positive), frame);
        Action sketch = () => OnshapePartModelingGeometryMapper.Map(invalidUnit, Extrusion(ExtrusionDirection.Positive), frame);
        Action extrusion = () => OnshapePartModelingGeometryMapper.Map(Rectangle(CanonicalPlane.Top, new Point2(0, 0), new Point2(0.02, 0.01)), invalidDirection, frame);

        nullSketch.Should().Throw<OnshapePartModelingGeometryMapException>();
        sketch.Should().Throw<OnshapePartModelingGeometryMapException>();
        extrusion.Should().Throw<OnshapePartModelingGeometryMapException>();
    }

    [Fact]
    public void Map_RejectsFiniteCoordinatesThatOverflowDuringProjection()
    {
        var axis = Math.Sqrt(0.5);
        var frame = Frame((axis, axis, 0), (-axis, axis, 0), (0, 0, 1));
        var definition = new SketchDefinition(CanonicalPlane.Top, LengthUnit.Meter,
            new CircleProfile(new Point2(double.MaxValue, double.MaxValue), 1));
        Action act = () => OnshapePartModelingGeometryMapper.Map(definition, Extrusion(ExtrusionDirection.Positive), frame);
        act.Should().Throw<OnshapePartModelingGeometryMapException>().Which.Failure
            .Should().Be(OnshapePartModelingGeometryMapFailure.InvalidInput);
    }

    public static IEnumerable<object[]> MeasuredFrames()
    {
        yield return [CanonicalPlane.Top, 1d, 0d, 0d, 0d, 1d, 0d, 0d, 0d, 1d];
        yield return [CanonicalPlane.Front, 1d, 0d, 0d, 0d, 0d, 1d, 0d, -1d, 0d];
        yield return [CanonicalPlane.Right, 0d, 1d, 0d, 0d, 0d, 1d, 1d, 0d, 0d];
    }

    private static SketchDefinition Rectangle(CanonicalPlane plane, Point2 first, Point2 opposite) =>
        new(plane, LengthUnit.Meter, new RectangleProfile(first, opposite));

    private static ExtrusionDefinition Extrusion(ExtrusionDirection direction) => new(0.08, LengthUnit.Meter, direction);

    private static OnshapePartModelingFrame Frame(
        (double X, double Y, double Z) x, (double X, double Y, double Z) y, (double X, double Y, double Z) normal) =>
        new(new OnshapeFrameVector(0, 0, 0), new OnshapeFrameVector(x.X, x.Y, x.Z), new OnshapeFrameVector(y.X, y.Y, y.Z), new OnshapeFrameVector(normal.X, normal.Y, normal.Z));

    private static void AssertWorldCorners(
        IReadOnlyList<OnshapeMappedPoint> mapped, CanonicalPlane plane, RectangleProfile rectangle, OnshapePartModelingFrame frame)
    {
        var expected = new[]
        {
            World(plane, rectangle.FirstCorner.X, rectangle.FirstCorner.Y),
            World(plane, rectangle.OppositeCorner.X, rectangle.FirstCorner.Y),
            World(plane, rectangle.OppositeCorner.X, rectangle.OppositeCorner.Y),
            World(plane, rectangle.FirstCorner.X, rectangle.OppositeCorner.Y),
        };
        mapped.Should().HaveCount(expected.Length);
        for (var i = 0; i < expected.Length; i++)
            AssertWorldNear(NativeToWorld(mapped[i], frame), expected[i]);
    }

    private static WorldPoint World(CanonicalPlane plane, double u, double v) => plane switch
    {
        CanonicalPlane.Top => new WorldPoint(u, v, 0),
        CanonicalPlane.Front => new WorldPoint(u, 0, v),
        CanonicalPlane.Right => new WorldPoint(0, u, v),
        _ => throw new ArgumentOutOfRangeException(nameof(plane)),
    };

    private static WorldPoint NativeToWorld(OnshapeMappedPoint point, OnshapePartModelingFrame frame) => new(
        frame.Origin.X + frame.XAxis.X * point.X + frame.YAxis.X * point.Y,
        frame.Origin.Y + frame.XAxis.Y * point.X + frame.YAxis.Y * point.Y,
        frame.Origin.Z + frame.XAxis.Z * point.X + frame.YAxis.Z * point.Y);

    private static void AssertWorldNear(WorldPoint actual, WorldPoint expected)
    {
        actual.X.Should().BeApproximately(expected.X, 1e-9);
        actual.Y.Should().BeApproximately(expected.Y, 1e-9);
        actual.Z.Should().BeApproximately(expected.Z, 1e-9);
    }

    private sealed record WorldPoint(double X, double Y, double Z);
}
