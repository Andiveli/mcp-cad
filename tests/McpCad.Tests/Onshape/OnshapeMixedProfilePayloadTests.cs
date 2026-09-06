using McpCad.Onshape.Builders;

namespace McpCad.Tests.Onshape;

public class OnshapeMixedProfilePayloadTests
{
    [Fact]
    public void BuildSingleArcClosedProfile_EmitsDeterministicClosedDProfile()
    {
        var input = D();

        using var doc = OnshapeSketchBuilder.BuildSingleArcClosedProfile(input, "front");
        var feature = doc.RootElement.GetProperty("feature");
        var entities = feature.GetProperty("entities");
        var constraints = feature.GetProperty("constraints");

        doc.RootElement.GetProperty("rejectMicroversionSkew").GetBoolean().Should().BeTrue();
        entities.GetArrayLength().Should().Be(4);
        constraints.GetArrayLength().Should().Be(4);
        entities.EnumerateArray().Select(entity => entity.GetProperty("entityId").GetString()).Should()
            .Equal("mixed-profile-segment-0", "mixed-profile-segment-1", "mixed-profile-segment-2", "mixed-profile-segment-3");
        entities.EnumerateArray().Should().OnlyContain(entity =>
            entity.GetProperty("btType").GetString() == "BTMSketchCurveSegment-155");
        entities.EnumerateArray().Should().NotContain(entity =>
            entity.GetProperty("btType").GetString() == "BTMSketchPoint-158");

        var arc = entities[0];
        arc.GetProperty("startPointId").GetString().Should().Be("mixed-profile-segment-0.start");
        arc.GetProperty("endPointId").GetString().Should().Be("mixed-profile-segment-0.end");
        arc.GetProperty("centerId").GetString().Should().Be("mixed-profile-segment-0.center");
        var geometry = arc.GetProperty("geometry");
        geometry.GetProperty("btType").GetString().Should().Be("BTCurveGeometryCircle-115");
        geometry.GetProperty("xCenter").GetDouble().Should().BeApproximately(0.01, 1e-9);
        geometry.GetProperty("yCenter").GetDouble().Should().BeApproximately(0, 1e-9);
        geometry.GetProperty("radius").GetDouble().Should().BeApproximately(0.01, 1e-9);
        geometry.GetProperty("clockwise").GetBoolean().Should().BeFalse();
        arc.GetProperty("startParam").GetDouble().Should().BeApproximately(Math.PI, 1e-8);
        arc.GetProperty("endParam").GetDouble().Should().BeApproximately(2 * Math.PI, 1e-8);

        for (var index = 0; index < constraints.GetArrayLength(); index++)
        {
            var constraint = constraints[index];
            constraint.GetProperty("entityId").GetString().Should().Be($"mixed-profile-constraint-coincident-{index}");
            constraint.GetProperty("constraintType").GetString().Should().Be("COINCIDENT");
            constraint.GetProperty("parameters")[0].GetProperty("value").GetString().Should().Be($"mixed-profile-segment-{index}.end");
            constraint.GetProperty("parameters")[1].GetProperty("value").GetString().Should().Be($"mixed-profile-segment-{(index + 1) % 4}.start");
        }
    }

    [Fact]
    public void BuildSingleArcClosedProfile_PreservesClockwiseArcTraversal()
    {
        var input = new MixedProfileInput(new(0, 0),
        [
            new MixedProfileArc(new(1, 2), new(2, 0)),
            new MixedProfileLine(new(2, -2)),
            new MixedProfileLine(new(0, -2)),
            new MixedProfileLine(new(0, 0)),
        ]);

        using var doc = OnshapeSketchBuilder.BuildSingleArcClosedProfile(input);
        var arc = doc.RootElement.GetProperty("feature").GetProperty("entities")[0];

        arc.GetProperty("geometry").GetProperty("clockwise").GetBoolean().Should().BeTrue();
        arc.GetProperty("endParam").GetDouble().Should().BeLessThan(arc.GetProperty("startParam").GetDouble());
    }

    [Fact]
    public void BuildMixedClosedProfile_EmitsDeterministicTwoArcCapsule()
    {
        using var doc = OnshapeMixedProfileFacade.BuildMixedClosedProfile(new MixedClosedProfile(
            new(0, 0), [
                new MixedProfileThreePointArcSegment(new(1, -1), new(2, 0)),
                new MixedProfileLineSegment(new(2, 4)),
                new MixedProfileThreePointArcSegment(new(1, 5), new(0, 4)),
                new MixedProfileLineSegment(new(0, 0))]), "front");
        var feature = doc.RootElement.GetProperty("feature");
        var entities = feature.GetProperty("entities");
        var constraints = feature.GetProperty("constraints");

        entities.GetArrayLength().Should().Be(4);
        constraints.GetArrayLength().Should().Be(4);
        foreach (var index in new[] { 0, 2 })
        {
            var entity = entities[index];
            entity.GetProperty("btType").GetString().Should().Be("BTMSketchCurveSegment-155");
            entity.GetProperty("startPointId").GetString().Should().Be($"mixed-profile-segment-{index}.start");
            entity.GetProperty("endPointId").GetString().Should().Be($"mixed-profile-segment-{index}.end");
            entity.GetProperty("centerId").GetString().Should().Be($"mixed-profile-segment-{index}.center");
            entity.GetProperty("geometry").GetProperty("btType").GetString().Should().Be("BTCurveGeometryCircle-115");
        }
        entities.EnumerateArray().Should().NotContain(entity => entity.GetProperty("btType").GetString() == "BTMSketchPoint-158");
        constraints.EnumerateArray().Select(constraint => constraint.GetProperty("constraintType").GetString()).Should().OnlyContain(type => type == "COINCIDENT");
        constraints.EnumerateArray().Select(constraint => constraint.GetProperty("parameters")[0].GetProperty("value").GetString()).Should()
            .Equal("mixed-profile-segment-0.end", "mixed-profile-segment-1.end", "mixed-profile-segment-2.end", "mixed-profile-segment-3.end");
    }

    [Fact]
    public void BuildMixedClosedProfile_EmitsFourArcRoundedRectangle()
    {
        using var doc = OnshapeMixedProfileFacade.BuildMixedClosedProfile(RoundedRectangle());
        var feature = doc.RootElement.GetProperty("feature");
        var entities = feature.GetProperty("entities");
        var constraints = feature.GetProperty("constraints");

        entities.GetArrayLength().Should().Be(8);
        entities.EnumerateArray().Select(entity => entity.GetProperty("geometry").GetProperty("btType").GetString()).Should()
            .Equal("BTCurveGeometryLine-117", "BTCurveGeometryCircle-115", "BTCurveGeometryLine-117", "BTCurveGeometryCircle-115",
                "BTCurveGeometryLine-117", "BTCurveGeometryCircle-115", "BTCurveGeometryLine-117", "BTCurveGeometryCircle-115");
        entities.EnumerateArray().Count(entity => entity.GetProperty("geometry").GetProperty("btType").GetString() == "BTCurveGeometryCircle-115").Should().Be(4);
        entities.EnumerateArray().Should().NotContain(entity => entity.GetProperty("btType").GetString() == "BTMSketchPoint-158");
        constraints.GetArrayLength().Should().Be(8);
        constraints.EnumerateArray().Select(constraint => constraint.GetProperty("constraintType").GetString()).Should().OnlyContain(type => type == "COINCIDENT");
    }

    [Fact]
    public void BuildMixedClosedProfile_RejectsZeroOrMoreThanEightArcs()
    {
        var zero = new MixedClosedProfile(new(0, 0), [new MixedProfileLineSegment(new(1, 0)), new MixedProfileLineSegment(new(0, 0))]);
        var nine = ArcRing(9);

        ((Action)(() => OnshapeMixedProfileFacade.BuildMixedClosedProfile(zero))).Should().Throw<ArgumentException>();
        ((Action)(() => OnshapeMixedProfileFacade.BuildMixedClosedProfile(nine))).Should().Throw<ArgumentException>();
    }

    [Fact]
    public void BuildSingleArcClosedProfile_RejectsInputBeforePayloadEmission()
    {
        var invalid = new MixedProfileInput(new(0, 0),
        [new MixedProfileArc(new(1, 0), new(2, 0)), new MixedProfileLine(new(0, 0))]);

        var act = () => OnshapeSketchBuilder.BuildSingleArcClosedProfile(invalid);

        act.Should().Throw<ArgumentException>();
    }

    private static MixedClosedProfile RoundedRectangle() => new(new(1, 0),
    [
        new MixedProfileLineSegment(new(9, 0)),
        new MixedProfileThreePointArcSegment(new(9.707106781186548, 0.2928932188134524), new(10, 1)),
        new MixedProfileLineSegment(new(10, 5)),
        new MixedProfileThreePointArcSegment(new(9.707106781186548, 5.707106781186548), new(9, 6)),
        new MixedProfileLineSegment(new(1, 6)),
        new MixedProfileThreePointArcSegment(new(0.2928932188134524, 5.707106781186548), new(0, 5)),
        new MixedProfileLineSegment(new(0, 1)),
        new MixedProfileThreePointArcSegment(new(0.2928932188134524, 0.2928932188134524), new(1, 0)),
    ]);

    private static MixedClosedProfile ArcRing(int count) => new(new(5, 0), System.Collections.Immutable.ImmutableArray.CreateRange(Enumerable.Range(1, count)
        .Select(index =>
        {
            var start = (index - 1) * 2 * Math.PI / count;
            var end = index * 2 * Math.PI / count;
            return (MixedProfileSegmentInput)new MixedProfileThreePointArcSegment(
                new(5 * Math.Cos((start + end) / 2), 5 * Math.Sin((start + end) / 2)),
                new(5 * Math.Cos(end), 5 * Math.Sin(end)));
        })));

    private static MixedProfileInput D() => new(new(0, 0),
    [
        new MixedProfileArc(new(1, -1), new(2, 0)),
        new MixedProfileLine(new(2, 2)),
        new MixedProfileLine(new(0, 2)),
        new MixedProfileLine(new(0, 0)),
    ]);
}
