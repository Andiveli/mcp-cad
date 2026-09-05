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
    public void BuildSingleArcClosedProfile_RejectsInputBeforePayloadEmission()
    {
        var invalid = new MixedProfileInput(new(0, 0),
        [new MixedProfileArc(new(1, 0), new(2, 0)), new MixedProfileLine(new(0, 0))]);

        var act = () => OnshapeSketchBuilder.BuildSingleArcClosedProfile(invalid);

        act.Should().Throw<ArgumentException>();
    }

    private static MixedProfileInput D() => new(new(0, 0),
    [
        new MixedProfileArc(new(1, -1), new(2, 0)),
        new MixedProfileLine(new(2, 2)),
        new MixedProfileLine(new(0, 2)),
        new MixedProfileLine(new(0, 0)),
    ]);
}
