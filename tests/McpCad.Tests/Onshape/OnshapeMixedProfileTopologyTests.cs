using McpCad.Onshape.Builders;

namespace McpCad.Tests.Onshape;

public class OnshapeMixedProfileTopologyTests
{
    private const double DAreaSquareMeters = (4 + Math.PI / 2) * 1e-4;

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
    private static MixedProfileInput Translate(MixedProfileInput input, double x, double y) => Transform(input, point => new(point.X + x, point.Y + y));
    private static MixedProfileInput RotateQuarterTurn(MixedProfileInput input) => Transform(input, point => new(-point.Y, point.X));
    private static MixedProfileInput Transform(MixedProfileInput input, Func<SketchPoint, SketchPoint> map) => new(map(input.Start), input.Segments.Select(segment => segment switch
    {
        MixedProfileLine line => (MixedProfileSegment)new MixedProfileLine(map(line.End)),
        MixedProfileArc arc => new MixedProfileArc(map(arc.Through), map(arc.End)),
        _ => throw new InvalidOperationException(),
    }).ToArray());
}
