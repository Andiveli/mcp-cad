using McpCad.Onshape.Builders;

namespace McpCad.Tests.Onshape;

public class OnshapeMixedProfileGeometryTests
{
    [Theory]
    [MemberData(nameof(ValidProfiles))]
    public void Normalize_PreservesConvertedTraversalAndArcRoute(
        object value, bool clockwise, bool major, bool semicircle)
    {
        var input = (MixedProfileInput)value;
        var result = OnshapeMixedProfileGeometry.Normalize(input);

        result.Start.Should().Be(new SketchPoint(0, 0));
        result.Segments.Should().HaveCount(input.Segments.Count);
        result.Segments.Count(segment => segment.Kind == MixedProfileSegmentKind.Arc).Should().Be(1);
        var arc = result.Segments.Single(segment => segment.Kind == MixedProfileSegmentKind.Arc);
        arc.Arc!.Clockwise.Should().Be(clockwise);
        (Math.Abs(arc.Arc.Sweep) > Math.PI).Should().Be(major);
        (Math.Abs(Math.Abs(arc.Arc.Sweep) - Math.PI) < 1e-9).Should().Be(semicircle);
        (clockwise ? arc.Arc.Sweep < 0 : arc.Arc.Sweep > 0).Should().BeTrue();
        arc.Arc.EndParameter.Should().BeApproximately(arc.Arc.StartParameter + arc.Arc.Sweep, 1e-12);
        result.Segments[^1].End.Should().Be(result.Start);
    }

    public static IEnumerable<object[]> ValidProfiles()
    {
        yield return [new MixedProfileInput(new(0, 0), [new MixedProfileArc(new(1, -.5), new(2, 0)), new MixedProfileLine(new(0, 0))]), false, false, false];
        yield return [new MixedProfileInput(new(0, 0), [new MixedProfileArc(new(1, -1), new(2, 0)), new MixedProfileLine(new(0, 0))]), false, false, true];
        yield return [new MixedProfileInput(new(0, 0), [new MixedProfileArc(new(1, -2), new(2, 0)), new MixedProfileLine(new(0, 0))]), false, true, false];
        yield return [new MixedProfileInput(new(0, 0), [new MixedProfileArc(new(1, 2), new(2, 0)), new MixedProfileLine(new(0, 0))]), true, true, false];
        yield return [new MixedProfileInput(new(0, 0), [new MixedProfileLine(new(0, 2)), new MixedProfileArc(new(-1, 1), new(0, 0))]), false, false, true];
        yield return [new MixedProfileInput(new(0, 0), [new MixedProfileArc(new(1, .5), new(2, 0)), new MixedProfileLine(new(0, 0))]), true, false, false];
        yield return [new MixedProfileInput(new(0, 0), [new MixedProfileArc(new(1, 1), new(2, 0)), new MixedProfileLine(new(0, 0))]), true, false, true];
    }

    [Fact]
    public void Normalize_ConvertsAndRoundsOnceAndIsDeterministic()
    {
        var input = new MixedProfileInput(new(12.3456789123, 0), [new MixedProfileArc(new(13.3456789123, -.5), new(14.3456789123, 0)), new MixedProfileLine(new(12.3456789123, 0))]);
        var first = OnshapeMixedProfileGeometry.Normalize(input);
        var second = OnshapeMixedProfileGeometry.Normalize(input);

        first.Start.Should().Be(second.Start);
        first.Segments.Should().Equal(second.Segments);
        first.Start.X.Should().Be(0.123456789);
        first.Segments[0].Start.Should().Be(first.Start);
    }

    [Theory]
    [MemberData(nameof(InvalidProfiles))]
    public void Normalize_RejectsEveryA1InvalidInput(object value)
    {
        var input = (MixedProfileInput)value;
        var act = () => OnshapeMixedProfileGeometry.Normalize(input);

        act.Should().Throw<ArgumentException>();
    }

    public static IEnumerable<object[]> InvalidProfiles()
    {
        var start = new SketchPoint(0, 0); var arc = new MixedProfileArc(new(1, 1), new(2, 0)); var close = new MixedProfileLine(start);
        yield return [new MixedProfileInput(start, [close])]; // zero arcs
        yield return [new MixedProfileInput(start, [arc, new MixedProfileArc(new(1, -1), new(2, 0)), close])]; // two arcs
        yield return [new MixedProfileInput(start, [arc])]; // no closure
        yield return [new MixedProfileInput(new(double.NaN, 0), [arc, close])]; // non-finite before conversion
        yield return [new MixedProfileInput(start, [new MixedProfileLine(start), arc, close])]; // zero line
        yield return [new MixedProfileInput(start, [new MixedProfileArc(start, new(2, 0)), close])]; // collapsed arc
        yield return [new MixedProfileInput(start, [new MixedProfileArc(new(double.Epsilon, 0), new(2, 0)), close])]; // collapsed after G9 conversion
        yield return [new MixedProfileInput(start, [new MixedProfileArc(new(1, 0), new(2, 0)), close])]; // collinear
        yield return [new MixedProfileInput(start, [new MixedProfileArc(new(1, 1e-15), new(2, 0)), close])]; // ill-conditioned/rounded
        yield return [new MixedProfileInput(start, [new MixedProfileLine(new(2, 0)), new MixedProfileArc(new(1, 1), start), close])]; // duplicate vertex
        yield return [new MixedProfileInput(start, [new MixedProfileArc(new(1, 1), new(2, 0)), new MixedProfileLine(new(2, 0)), close])]; // duplicate vertex
    }

    [Theory]
    [MemberData(nameof(ValidTwoArcProfiles))]
    public void Normalize_OneOrTwoArcs_PreservesEachArcIndependently(
        object value, bool firstClockwise, bool secondClockwise, bool firstMajor, bool secondSemicircle)
    {
        var input = (MixedProfileInput)value;
        var result = OnshapeMixedProfileGeometry.Normalize(input, 1, 2);

        var arcs = result.Segments.Where(segment => segment.Kind == MixedProfileSegmentKind.Arc).ToArray();
        result.Segments.Should().HaveCount(input.Segments.Count);
        arcs.Should().HaveCount(2);
        arcs[0].Arc!.Clockwise.Should().Be(firstClockwise);
        arcs[1].Arc!.Clockwise.Should().Be(secondClockwise);
        (Math.Abs(arcs[0].Arc!.Sweep) > Math.PI).Should().Be(firstMajor);
        (Math.Abs(Math.Abs(arcs[1].Arc!.Sweep) - Math.PI) < 1e-9).Should().Be(secondSemicircle);
        result.Segments[^1].End.Should().Be(result.Start);
    }

    [Fact]
        public void Normalize_LaterArcDuplicateCanonicalEndpoint_RejectsBeforeTopology()
        {
            var input = new MixedProfileInput(new(1, 0), [
                new MixedProfileArc(new(Math.Sqrt(.5), Math.Sqrt(.5)), new(0, 1)),
                new MixedProfileArc(new(-Math.Sqrt(.5), Math.Sqrt(.5)), new(-1, 0)),
                new MixedProfileArc(new(-Math.Sqrt(.5), Math.Sqrt(.5)), new(0, 1)),
                new MixedProfileArc(new(Math.Sqrt(.5), -Math.Sqrt(.5)), new(1, 0)),
            ]);

            var act = () => OnshapeMixedProfileGeometry.Normalize(input, 1, 8);

            act.Should().Throw<ArgumentException>().WithMessage("*vertices cannot repeat*");
        }

        public static IEnumerable<object[]> ValidTwoArcProfiles()
    {
        yield return [new MixedProfileInput(new(0, 1), [
            new MixedProfileArc(new(1, 2), new(2, 1)), new MixedProfileLine(new(2, -1)),
            new MixedProfileArc(new(1, -2), new(0, -1)), new MixedProfileLine(new(0, 1))]), true, true, false, true];
        yield return [new MixedProfileInput(new(0, 1), [
            new MixedProfileLine(new(0, -1)), new MixedProfileArc(new(1, -2), new(2, -1)),
            new MixedProfileLine(new(2, 1)), new MixedProfileArc(new(1, 2), new(0, 1))]), false, false, false, true];
        yield return [new MixedProfileInput(new(0, 0), [
            new MixedProfileArc(new(1, -.5), new(2, 0)), new MixedProfileLine(new(2, 2)),
            new MixedProfileArc(new(1, 1), new(0, 2)), new MixedProfileLine(new(0, 0))]), false, true, false, true];
    }

    [Theory]
    [MemberData(nameof(InvalidTwoArcProfiles))]
    public void Normalize_OneOrTwoArcs_RejectsInvalidArcCountsAndSecondArcGeometry(object value)
    {
        var act = () => OnshapeMixedProfileGeometry.Normalize((MixedProfileInput)value, 1, 2);

        act.Should().Throw<ArgumentException>();
    }

    public static IEnumerable<object[]> InvalidTwoArcProfiles()
    {
        yield return [new MixedProfileInput(new(0, 0), [new MixedProfileLine(new(1, 0)), new MixedProfileLine(new(0, 0))])];
        yield return [new MixedProfileInput(new(0, 0), [
            new MixedProfileArc(new(1, 1), new(2, 0)), new MixedProfileArc(new(3, 1), new(4, 0)),
            new MixedProfileArc(new(3, -1), new(0, 0))])];
        yield return [new MixedProfileInput(new(0, 0), [
            new MixedProfileArc(new(1, 1), new(2, 0)), new MixedProfileLine(new(2, 2)),
            new MixedProfileArc(new(2, 2), new(0, 2)), new MixedProfileLine(new(0, 0))])];
        yield return [new MixedProfileInput(new(0, 0), [
            new MixedProfileArc(new(1, 1), new(2, 0)), new MixedProfileLine(new(2, 2)),
            new MixedProfileArc(new(1, 2), new(0, 2)), new MixedProfileLine(new(0, 0))])];
    }
}
