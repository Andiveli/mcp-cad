using McpCad.Core.Modeling;

namespace McpCad.Tests.Modeling;

public class PartModelingValidationTests
{
    [Fact]
    public void ValidateSketch_NormalizesRectangleToMetersAndOppositeCorners()
    {
        var result = PartModelingValidation.ValidateSketch(new SketchDefinition(
            CanonicalPlane.Top,
            LengthUnit.Centimeter,
            new RectangleProfile(new Point2(3, 4), new Point2(1, 2))));

        result.IsValid.Should().BeTrue();
        result.Value!.Unit.Should().Be(LengthUnit.Meter);
        result.Value.Profile.Should().Be(new RectangleProfile(
            new Point2(0.01, 0.02), new Point2(0.03, 0.04)));
    }

    [Fact]
    public void ValidateSketch_NormalizesCircleToMeters()
    {
        var result = PartModelingValidation.ValidateSketch(new SketchDefinition(
            CanonicalPlane.Front,
            LengthUnit.Millimeter,
            new CircleProfile(new Point2(20, -10), 5)));

        result.IsValid.Should().BeTrue();
        result.Value!.Profile.Should().Be(new CircleProfile(new Point2(0.02, -0.01), 0.005));
    }

    [Fact]
    public void ValidateExtrusion_NormalizesDepthAndPreservesDirection()
    {
        var result = PartModelingValidation.ValidateExtrusion(new ExtrusionDefinition(
            2,
            LengthUnit.Meter,
            ExtrusionDirection.Negative));

        result.IsValid.Should().BeTrue();
        result.Value.Should().Be(new ExtrusionDefinition(2, LengthUnit.Meter, ExtrusionDirection.Negative));
    }

    [Theory]
    [InlineData(LengthUnit.Millimeter, 20d)]
    [InlineData(LengthUnit.Centimeter, 2d)]
    [InlineData(LengthUnit.Meter, 0.02d)]
    public void ValidateSketch_ProducesEquivalentMeterGeometryForEveryUnit(LengthUnit unit, double radius)
    {
        var result = PartModelingValidation.ValidateSketch(new SketchDefinition(
            CanonicalPlane.Right,
            unit,
            new CircleProfile(new Point2(radius, -radius), radius)));

        result.Value.Should().Be(new SketchDefinition(
            CanonicalPlane.Right,
            LengthUnit.Meter,
            new CircleProfile(new Point2(0.02, -0.02), 0.02)));
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void Validation_RejectsNonFiniteLengths(double value)
    {
        PartModelingValidation.ValidateSketch(new SketchDefinition(
            CanonicalPlane.Top,
            LengthUnit.Meter,
            new CircleProfile(new Point2(value, 1), 1))).Error!.Code.Should().Be(PartModelingErrorCode.NonFiniteValue);
        PartModelingValidation.ValidateExtrusion(new ExtrusionDefinition(
            value,
            LengthUnit.Meter,
            ExtrusionDirection.Positive)).Error!.Code.Should().Be(PartModelingErrorCode.NonFiniteValue);
    }

    [Theory]
    [InlineData(0d)]
    [InlineData(-1d)]
    public void Validation_RejectsNonPositiveRadiusAndDepth(double value)
    {
        PartModelingValidation.ValidateSketch(new SketchDefinition(
            CanonicalPlane.Top,
            LengthUnit.Meter,
            new CircleProfile(new Point2(1, 1), value))).Error!.Code.Should().Be(PartModelingErrorCode.NonPositiveValue);
        PartModelingValidation.ValidateExtrusion(new ExtrusionDefinition(
            value,
            LengthUnit.Meter,
            ExtrusionDirection.Positive)).Error!.Code.Should().Be(PartModelingErrorCode.NonPositiveValue);
    }

    [Fact]
    public void Validation_RejectsUndefinedEnumsDegenerateGeometryAndConversionCollapse()
    {
        PartModelingValidation.ValidateSketch(new SketchDefinition(
            (CanonicalPlane)99,
            LengthUnit.Meter,
            new CircleProfile(new Point2(1, 1), 1))).Error!.Code.Should().Be(PartModelingErrorCode.InvalidPlane);
        PartModelingValidation.ValidateExtrusion(new ExtrusionDefinition(
            1,
            LengthUnit.Meter,
            (ExtrusionDirection)99)).Error!.Code.Should().Be(PartModelingErrorCode.InvalidDirection);
        PartModelingValidation.ValidateSketch(new SketchDefinition(
            CanonicalPlane.Top,
            LengthUnit.Meter,
            new RectangleProfile(new Point2(1, 1), new Point2(1, 2)))).Error!.Code.Should().Be(PartModelingErrorCode.DegenerateGeometry);
        PartModelingValidation.ValidateSketch(new SketchDefinition(
            CanonicalPlane.Top,
            LengthUnit.Millimeter,
            new CircleProfile(new Point2(0, 0), double.Epsilon))).Error!.Code.Should().Be(PartModelingErrorCode.ConversionCollapsed);
    }

    public static IEnumerable<object[]> NullPointProfiles =>
    [
        [new RectangleProfile(null!, new Point2(1, 1))],
        [new RectangleProfile(new Point2(1, 1), null!)],
        [new CircleProfile(null!, 1)],
    ];

    [Theory]
    [MemberData(nameof(NullPointProfiles))]
    public void ValidateSketch_ReturnsTypedFailureForNullProfilePoint(SketchProfile profile)
    {
        ValidationResult<SketchDefinition>? result = null;
        Action validate = () => result = PartModelingValidation.ValidateSketch(new SketchDefinition(
            CanonicalPlane.Top,
            LengthUnit.Meter,
            profile));

        validate.Should().NotThrow();
        result!.IsValid.Should().BeFalse();
        result.Value.Should().BeNull();
        result.Error!.Code.Should().Be(PartModelingErrorCode.DegenerateGeometry);
    }

    [Fact]
    public void Validation_ReturnsTypedFailureForNullExtrusionDefinition()
    {
        var result = PartModelingValidation.ValidateExtrusion(null!);

        result.IsValid.Should().BeFalse();
        result.Error!.Code.Should().Be(PartModelingErrorCode.DegenerateGeometry);
    }
}
