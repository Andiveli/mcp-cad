using McpCad.Onshape.Builders;

namespace McpCad.Tests.Onshape;

public class OnshapeMixedProfileTopologyTests
{
    private const double DAreaSquareMeters = (4 + Math.PI / 2) * 1e-4;
    private const double CapsuleAreaSquareMeters = (8 + Math.PI) * 1e-4;

    [Theory]
    [MemberData(nameof(ValidProfiles))]
    public void Validate_AcceptsSimpleProfilesAndRetainsNormalizedBoundary(object value, int winding, double expectedArea)
    {
        var result = OnshapeMixedProfileTopology.Validate((MixedProfileInput)value);

        result.Profile.Segments.Should().HaveCount(((MixedProfileInput)value).Segments.Count);
        Math.Sign(result.SignedArea).Should().Be(winding);
        Math.Abs(result.SignedArea).Should().BeApproximately(expectedArea, 1e-12);
    }

    public static IEnumerable<object[]> ValidProfiles()
    {
        yield return [D(), 1, DAreaSquareMeters];
        yield return [Translate(D(), 7, -4), 1, DAreaSquareMeters];
        yield return [RotateQuarterTurn(D()), 1, DAreaSquareMeters];
        yield return [Hook(), 1, DAreaSquareMeters];
        yield return [ReverseD(), -1, DAreaSquareMeters];
        yield return [Capsule(), 1, CapsuleAreaSquareMeters];
        yield return [ReverseCapsule(), -1, CapsuleAreaSquareMeters];
        yield return [Translate(Capsule(), 7, -4), 1, CapsuleAreaSquareMeters];
        yield return [RotateQuarterTurn(Capsule()), 1, CapsuleAreaSquareMeters];
        yield return [AnnularSector(), 1, 3 * Math.PI * 1e-4 / 4];
        yield return [ReverseAnnularSector(), -1, 3 * Math.PI * 1e-4 / 4];
    }

    [Theory]
    [MemberData(nameof(InvalidProfiles))]
    public void Validate_RejectsDistinctA2ContactsAfterA1Normalization(string _, object value)
    {
        var input = (MixedProfileInput)value;
        var normalized = OnshapeMixedProfileGeometry.Normalize(input);
        var act = () => OnshapeMixedProfileTopology.Validate(normalized);

        normalized.Segments.Should().HaveCount(input.Segments.Count);
        if (_ == "near-contact ambiguity")
            normalized.Segments[3].End.Y.Should().NotBe(
                OnshapeMixedProfileGeometry.Normalize(TangentProfile(-1)).Segments[3].End.Y);
        var exception = act.Should().Throw<ArgumentException>();
        if (_ == "negligible area") exception.WithMessage("*negligible*");
        else exception.WithMessage("*contact*");
    }

    public static IEnumerable<object[]> InvalidProfiles()
    {
        yield return ["non-adjacent line touch", new MixedProfileInput(new(0, 0), [Arc(), new MixedProfileLine(new(2, 2)), new MixedProfileLine(new(0, 2)), new MixedProfileLine(new(2, 1)), new MixedProfileLine(new(0, 0))])];
        yield return ["non-adjacent line overlap", new MixedProfileInput(new(0, 0), [Arc(), new MixedProfileLine(new(2, 2)), new MixedProfileLine(new(0, 2)), new MixedProfileLine(new(0, 3)), new MixedProfileLine(new(2, 1)), new MixedProfileLine(new(2, .5)), new MixedProfileLine(new(0, 0))])];
        yield return ["line-line crossing", new MixedProfileInput(new(0, 0), [Arc(), new MixedProfileLine(new(2, 2)), new MixedProfileLine(new(0, 2)), new MixedProfileLine(new(3, 1)), new MixedProfileLine(new(0, 0))])];
        yield return ["line-arc crossing", TangentProfile(-.9)];
        yield return ["exact tangent", TangentProfile(-1)];
        yield return ["near-contact ambiguity", NearContactAmbiguityProfile()];
        yield return ["adjacent extra arc contact", new MixedProfileInput(new(0, 0), [Arc(), new MixedProfileLine(new(1, -2)), new MixedProfileLine(new(0, 2)), new MixedProfileLine(new(0, 0))])];
        yield return ["negligible area", new MixedProfileInput(new(0, 0), [new MixedProfileArc(new(.0001, -.0001), new(.0002, 0)), new MixedProfileLine(new(0, 0))])];
    }

    [Fact]
    public void Validate_PublicNearEqualRadiusConcentricArcs_FailsClosed()
    {
        var normalized = OnshapeMixedProfileGeometry.Normalize(NearEqualRadiusAnnularSector(), 1, 2);
        var arcs = normalized.Segments.Where(segment => segment.Arc is not null).Select(segment => segment.Arc!).ToArray();

        Math.Sqrt(Math.Pow(arcs[0].Center.X - arcs[1].Center.X, 2) + Math.Pow(arcs[0].Center.Y - arcs[1].Center.Y, 2)).Should().BeGreaterThan(0).And.BeLessThanOrEqualTo(4e-10);
        Math.Abs(arcs[0].Radius - arcs[1].Radius).Should().BeGreaterThan(0).And.BeLessThanOrEqualTo(4e-10);
        var act = () => OnshapeMixedProfileTopology.Validate(normalized);
        act.Should().Throw<ArgumentException>().WithMessage("*contact*");
    }

    [Theory]
    [MemberData(nameof(PublicNearTangentProfiles))]
    public void Validate_PublicG9DistinctNearTangencies_FailsClosed(string _, object value, double exactDistance)
    {
        var normalized = OnshapeMixedProfileGeometry.Normalize((MixedProfileInput)value, 1, 2);
        var arcs = normalized.Segments.Where(segment => segment.Arc is not null).Select(segment => segment.Arc!).ToArray();
        var distance = Math.Sqrt(Math.Pow(arcs[1].Center.X - arcs[0].Center.X, 2) + Math.Pow(arcs[1].Center.Y - arcs[0].Center.Y, 2));
        var boundary = _ == "external" ? arcs[0].Radius + arcs[1].Radius : Math.Abs(arcs[0].Radius - arcs[1].Radius);

        arcs[0].Center.Should().NotBe(arcs[1].Center);
        arcs[1].Center.X.Should().NotBe(.03);
        arcs[0].Radius.Should().BeGreaterThan(0);
        arcs[1].Radius.Should().NotBe(.01);
        distance.Should().NotBe(exactDistance);
        Math.Abs(distance - boundary).Should().BeGreaterThan(0).And.BeLessThanOrEqualTo(2e-10);
        var act = () => OnshapeMixedProfileTopology.Validate(normalized);
        act.Should().Throw<ArgumentException>().WithMessage("*contact*");
    }

    public static IEnumerable<object[]> PublicNearTangentProfiles()
    {
        yield return ["external", NearExternalTangentProfile(), .02];
        yield return ["internal", NearInternalTangentProfile(), .01];
    }

    [Theory]
    [MemberData(nameof(PublicArcArcRejections))]
    public void Normalize_A2DuplicateOrReverseCoincidentArcs_RejectsBeforeTopology(string _, object value)
    {
        var input = (MixedProfileInput)value;
        var act = () => OnshapeMixedProfileGeometry.Normalize(input, 1, 2);

        act.Should().Throw<ArgumentException>().WithMessage("*vertices cannot repeat*");
    }

    public static IEnumerable<object[]> PublicArcArcRejections()
    {
        yield return ["duplicate coincident arcs", DuplicateCoincidentArcs()];
        yield return ["reverse coincident arcs", ReverseCoincidentArcs()];
    }

    [Theory]
    [MemberData(nameof(ArcArcDefenseInDepthCases))]
    public void Validate_A2ArcArcContacts_DefenseInDepth(string name, object value, bool accepted)
    {
        var profile = (NormalizedMixedProfile)value;
        profile.Segments.Count(segment => segment.Arc is not null).Should().Be(2, name);
        var act = () => OnshapeMixedProfileTopology.Validate(profile);

        if (accepted) act.Should().NotThrow(name);
        else act.Should().Throw<ArgumentException>().WithMessage("*contact*", name);
    }

    public static IEnumerable<object[]> ArcArcDefenseInDepthCases()
    {
        yield return ["distinct circles / global intersections outside bounded arcs", ArcArcFixture(false, Arc(new(0, 0), 1, Math.PI, -Math.PI / 2), Arc(new(1, 0), 1, -Math.PI / 2, Math.PI / 2)), true];
        yield return ["external tangent / adjacent designated endpoint", ArcArcFixture(true, Arc(new(0, 0), 1, Math.PI, -Math.PI), Arc(new(2, 0), 1, Math.PI, -Math.PI / 2)), true];
        yield return ["internal tangent / adjacent designated endpoint", ArcArcFixture(true, Arc(new(0, 0), 2, Math.PI, -Math.PI), Arc(new(1, 0), 1, 0, Math.PI / 2)), true];
        yield return ["coincident circles / adjacent disjoint intervals", ArcArcFixture(true, Arc(new(0, 0), 1, Math.PI, Math.PI), Arc(new(0, 0), 1, 0, Math.PI / 2)), true];
        yield return ["concentric circles / distinct radii", ArcArcFixture(false, Arc(new(0, 0), 2, Math.PI, -Math.PI), Arc(new(0, 0), 1, Math.PI, -Math.PI)), true];
        yield return ["bounded interior crossing", ArcArcFixture(false, Arc(new(0, 0), 1, Math.PI, Math.PI), Arc(new(1, 0), 1, 0, -Math.PI)), false];
        yield return ["external tangent / non-adjacent", ArcArcFixture(false, Arc(new(0, 0), 1, Math.PI, -Math.PI), Arc(new(2, 0), 1, Math.PI, -Math.PI / 2)), false];
        yield return ["internal tangent / non-adjacent", ArcArcFixture(false, Arc(new(0, 0), 2, Math.PI, -Math.PI), Arc(new(1, 0), 1, 0, Math.PI / 2)), false];
        yield return ["non-adjacent endpoint touch", ArcArcFixture(false, Arc(new(0, 0), 1, Math.PI, -Math.PI), Arc(new(2, 0), 1, Math.PI, Math.PI / 2)), false];
        yield return ["adjacent wrong endpoint", ArcArcFixture(true, Arc(new(0, 0), 1, 0, -Math.PI / 2), Arc(new(2, 0), 1, Math.PI, -Math.PI / 2)), false];
        yield return ["adjacent second bounded intersection", ArcArcFixture(true, Arc(new(0, 0), 1, Math.PI, 4 * Math.PI / 3), Arc(new(1, 0), 1, 2 * Math.PI / 3, -5 * Math.PI / 3)), false];
        yield return ["coincident positive-angle overlap", ArcArcFixture(false, Arc(new(0, 0), 1, Math.PI, -Math.PI), Arc(new(0, 0), 1, 5 * Math.PI / 4, -Math.PI / 2)), false];
        yield return ["duplicate coincident arcs", ArcArcFixture(false, Arc(new(0, 0), 1, Math.PI, -Math.PI), Arc(new(0, 0), 1, Math.PI, -Math.PI)), false];
        yield return ["reverse coincident arcs", ArcArcFixture(false, Arc(new(0, 0), 1, Math.PI, -Math.PI), Arc(new(0, 0), 1, 0, Math.PI)), false];
        yield return ["near-coincident circles", ArcArcFixture(false, Arc(new(0, 0), 1, Math.PI, -Math.PI), Arc(new(5e-11, 0), 1, Math.PI, -Math.PI)), false];
        yield return ["near-external tangent", ArcArcFixture(false, Arc(new(0, 0), 1, Math.PI, -Math.PI), Arc(new(2 + 5e-11, 0), 1, Math.PI, -Math.PI / 2)), false];
        yield return ["near-internal tangent", ArcArcFixture(false, Arc(new(0, 0), 2, Math.PI, -Math.PI), Arc(new(1 + 5e-11, 0), 1, 0, Math.PI / 2)), false];
    }

    private static NormalizedMixedProfile ArcArcFixture(bool adjacent, NormalizedMixedProfileSegment first, NormalizedMixedProfileSegment second)
    {
        var baseline = OnshapeMixedProfileGeometry.Normalize(Capsule(), 1, 2);
        var remoteFirst = new NormalizedMixedProfileSegment(MixedProfileSegmentKind.Line, new(100, 100), new(100, 101));
        var remoteSecond = new NormalizedMixedProfileSegment(MixedProfileSegmentKind.Line, new(102, 100), new(102, 101));
        return baseline with { Segments = adjacent ? [first, second, remoteFirst, remoteSecond] : [first, remoteFirst, second, remoteSecond] };
    }

    private static NormalizedMixedProfileSegment Arc(SketchPoint center, double radius, double startParameter, double sweep)
    {
        var arc = new NormalizedMixedProfileArc(center, radius, sweep < 0, startParameter, startParameter + sweep, sweep);
        return new(MixedProfileSegmentKind.Arc, PointOn(arc, startParameter), PointOn(arc, startParameter + sweep), Arc: arc);
    }

    private static MixedProfileInput DuplicateCoincidentArcs() => new(new(0, 0), [
        new MixedProfileArc(new(1, -1), new(2, 0)), new MixedProfileLine(new(0, 0)),
        new MixedProfileArc(new(1, -1), new(2, 0)), new MixedProfileLine(new(0, 0))]);
    private static MixedProfileInput ReverseCoincidentArcs() => new(new(0, 0), [
        new MixedProfileArc(new(1, -1), new(2, 0)), new MixedProfileArc(new(1, -1), new(0, 0)),
        new MixedProfileLine(new(0, 2)), new MixedProfileLine(new(0, 0))]);
    private static MixedProfileInput TwoArcs(SketchPoint start, SketchPoint throughA, SketchPoint endA, SketchPoint startB, SketchPoint throughB, SketchPoint endB) =>
        new(start, [new MixedProfileArc(throughA, endA), new MixedProfileLine(startB), new MixedProfileArc(throughB, endB), new MixedProfileLine(new(endB.X + 2, endB.Y + 2)), new MixedProfileLine(new(-2, endB.Y + 2)), new MixedProfileLine(start)]);
    private static SketchPoint PointOn(NormalizedMixedProfileArc arc, double angle) => new(arc.Center.X + arc.Radius * Math.Cos(angle), arc.Center.Y + arc.Radius * Math.Sin(angle));

    private static MixedProfileInput AnnularSector() => new(new(2, 0), [
        new MixedProfileArc(new(Math.Sqrt(2), Math.Sqrt(2)), new(0, 2)), new MixedProfileLine(new(0, 1)),
        new MixedProfileArc(new(Math.Sqrt(.5), Math.Sqrt(.5)), new(1, 0)), new MixedProfileLine(new(2, 0))]);
    private static MixedProfileInput ReverseAnnularSector() => new(new(2, 0), [
        new MixedProfileLine(new(1, 0)), new MixedProfileArc(new(Math.Sqrt(.5), Math.Sqrt(.5)), new(0, 1)),
        new MixedProfileLine(new(0, 2)), new MixedProfileArc(new(Math.Sqrt(2), Math.Sqrt(2)), new(2, 0))]);
    private static MixedProfileInput NearEqualRadiusAnnularSector()
    {
        const double innerRadius = 1.999999995;
        return new(new(2, 0), [
            new MixedProfileArc(new(Math.Sqrt(2), Math.Sqrt(2)), new(0, 2)), new MixedProfileLine(new(0, innerRadius)),
            new MixedProfileArc(new(innerRadius / Math.Sqrt(2), innerRadius / Math.Sqrt(2)), new(innerRadius, 0)), new MixedProfileLine(new(2, 0))]);
    }
    private static MixedProfileInput NearExternalTangentProfile()
    {
        const double centerEpsilon = 1.5e-8;
        const double radius = 1.000000001;
        return new(new(0, 0), [
            new MixedProfileArc(new(1, -1), new(2, 0)), new MixedProfileLine(new(3 + centerEpsilon + radius, 0)),
            new MixedProfileArc(new(3 + centerEpsilon, -radius), new(3 + centerEpsilon - radius, 0)), new MixedProfileLine(new(0, 0))]);
    }
    private static MixedProfileInput NearInternalTangentProfile()
    {
        const double centerEpsilon = 1.5e-8;
        const double radius = 1.000000001;
        return new(new(0, 0), [
            new MixedProfileArc(new(2, -2), new(4, 0)), new MixedProfileLine(new(3 + centerEpsilon + radius, 0)),
            new MixedProfileArc(new(3 + centerEpsilon, -radius), new(3 + centerEpsilon - radius, 0)), new MixedProfileLine(new(0, 0))]);
    }

    private static MixedProfileInput TangentProfile(double y) => new(new(0, 0), [
        Arc(), new MixedProfileLine(new(3, 0)), new MixedProfileLine(new(3, -2)), new MixedProfileLine(new(1.5, y)),
        new MixedProfileLine(new(.5, y)), new MixedProfileLine(new(-1, -2)), new MixedProfileLine(new(-1, 0)), new MixedProfileLine(new(0, 0))]);
    private static MixedProfileInput NearContactAmbiguityProfile() => new(new(0, 0), [
        Arc(), new MixedProfileLine(new(3, 0)), new MixedProfileLine(new(2000, -2)), new MixedProfileLine(new(2000, -1.0000001)),
        new MixedProfileLine(new(-2000, -1.0000001)), new MixedProfileLine(new(-2000, -2)), new MixedProfileLine(new(-1, -2)),
        new MixedProfileLine(new(-1, 0)), new MixedProfileLine(new(0, 0))]);
    private static MixedProfileArc Arc() => new(new(1, -1), new(2, 0));
    private static MixedProfileInput D() => new(new(0, 0), [Arc(), new MixedProfileLine(new(2, 2)), new MixedProfileLine(new(0, 2)), new MixedProfileLine(new(0, 0))]);
    private static MixedProfileInput Hook() => new(new(0, 0), [Arc(), new MixedProfileLine(new(2, 3)), new MixedProfileLine(new(1, 3)), new MixedProfileLine(new(1, 1)), new MixedProfileLine(new(0, 1)), new MixedProfileLine(new(0, 0))]);
    private static MixedProfileInput ReverseD() => new(new(0, 0), [new MixedProfileLine(new(0, 2)), new MixedProfileLine(new(2, 2)), new MixedProfileLine(new(2, 0)), new MixedProfileArc(new(1, -1), new(0, 0))]);
    private static MixedProfileInput Capsule() => new(new(0, 0), [
        new MixedProfileArc(new(1, -1), new(2, 0)), new MixedProfileLine(new(2, 4)),
        new MixedProfileArc(new(1, 5), new(0, 4)), new MixedProfileLine(new(0, 0))]);
    private static MixedProfileInput ReverseCapsule() => new(new(0, 0), [
        new MixedProfileLine(new(0, 4)), new MixedProfileArc(new(1, 5), new(2, 4)),
        new MixedProfileLine(new(2, 0)), new MixedProfileArc(new(1, -1), new(0, 0))]);
    private static MixedProfileInput Translate(MixedProfileInput input, double x, double y) => Transform(input, point => new(point.X + x, point.Y + y));
    private static MixedProfileInput RotateQuarterTurn(MixedProfileInput input) => Transform(input, point => new(-point.Y, point.X));
    private static MixedProfileInput Transform(MixedProfileInput input, Func<SketchPoint, SketchPoint> map) => new(map(input.Start), input.Segments.Select(segment => segment switch
    {
        MixedProfileLine line => (MixedProfileSegment)new MixedProfileLine(map(line.End)),
        MixedProfileArc arc => new MixedProfileArc(map(arc.Through), map(arc.End)),
        _ => throw new InvalidOperationException(),
    }).ToArray());
}
