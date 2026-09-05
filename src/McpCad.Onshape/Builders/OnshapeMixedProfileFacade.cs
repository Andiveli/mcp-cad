using System.Collections.Immutable;
using System.Text.Json;

namespace McpCad.Onshape.Builders;

/// <summary>A centimeter-space point supplied to a single-arc closed profile.</summary>
public readonly record struct MixedProfilePointCm(double Xcm, double Ycm);

/// <summary>Base type for a typed single-arc profile segment.</summary>
public abstract record MixedProfileSegmentInput(MixedProfilePointCm End);

/// <summary>A straight segment ending at <see cref="End"/>.</summary>
public sealed record MixedProfileLineSegment(MixedProfilePointCm End) : MixedProfileSegmentInput(End);

/// <summary>An arc from the prior endpoint through <see cref="Through"/> to <see cref="End"/>.</summary>
public sealed record MixedProfileThreePointArcSegment(MixedProfilePointCm Through, MixedProfilePointCm End) : MixedProfileSegmentInput(End);

/// <summary>A closed centimeter-space profile containing exactly one three-point arc.</summary>
public sealed record SingleArcClosedProfile(MixedProfilePointCm Start, ImmutableArray<MixedProfileSegmentInput> Segments);

/// <summary>Public boundary for validated single-arc profile sketch payloads.</summary>
public static class OnshapeMixedProfileFacade
{
    public static JsonDocument BuildSingleArcClosedProfile(SingleArcClosedProfile input, string? plane = null)
    {
        ArgumentNullException.ThrowIfNull(input);
        var segments = input.Segments.Select(segment => segment switch
        {
            MixedProfileLineSegment line => (MixedProfileSegment)new MixedProfileLine(new SketchPoint(line.End.Xcm, line.End.Ycm)),
            MixedProfileThreePointArcSegment arc => new MixedProfileArc(
                new SketchPoint(arc.Through.Xcm, arc.Through.Ycm),
                new SketchPoint(arc.End.Xcm, arc.End.Ycm)),
            null => throw new ArgumentException("Mixed-profile segments cannot be null.", nameof(input)),
            _ => throw new ArgumentException("Unsupported mixed-profile segment.", nameof(input)),
        }).ToArray();

        return OnshapeSketchBuilder.BuildSingleArcClosedProfile(new MixedProfileInput(
            new SketchPoint(input.Start.Xcm, input.Start.Ycm), segments), plane);
    }
}
