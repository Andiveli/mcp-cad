using System.Collections.Immutable;
using System.Text.Json;

namespace McpCad.Onshape.Builders;

/// <summary>A centimeter-space point supplied to a one-or-two-arc mixed closed profile.</summary>
public readonly record struct MixedProfilePointCm(double Xcm, double Ycm);

/// <summary>Base type for a typed one-or-two-arc mixed-profile segment.</summary>
public abstract record MixedProfileSegmentInput(MixedProfilePointCm End);

/// <summary>A straight segment ending at <see cref="End"/>.</summary>
public sealed record MixedProfileLineSegment(MixedProfilePointCm End) : MixedProfileSegmentInput(End);

/// <summary>An arc from the prior endpoint through <see cref="Through"/> to <see cref="End"/>.</summary>
public sealed record MixedProfileThreePointArcSegment(MixedProfilePointCm Through, MixedProfilePointCm End) : MixedProfileSegmentInput(End);

/// <summary>A closed centimeter-space profile containing exactly one three-point arc.</summary>
public sealed record SingleArcClosedProfile(MixedProfilePointCm Start, ImmutableArray<MixedProfileSegmentInput> Segments);

/// <summary>A closed centimeter-space profile containing one or two three-point arcs.</summary>
public sealed record MixedClosedProfile(MixedProfilePointCm Start, ImmutableArray<MixedProfileSegmentInput> Segments);

/// <summary>Public boundary for validated mixed-profile sketch payloads.</summary>
public static class OnshapeMixedProfileFacade
{
    public static JsonDocument BuildSingleArcClosedProfile(SingleArcClosedProfile input, string? plane = null)
    {
        ArgumentNullException.ThrowIfNull(input);
        return OnshapeSketchBuilder.BuildSingleArcClosedProfile(ToInternal(input.Start, input.Segments, nameof(input)), plane);
    }

    public static JsonDocument BuildMixedClosedProfile(MixedClosedProfile input, string? plane = null)
    {
        ArgumentNullException.ThrowIfNull(input);
        return OnshapeSketchBuilder.BuildMixedClosedProfile(ToInternal(input.Start, input.Segments, nameof(input)), plane);
    }

    private static MixedProfileInput ToInternal(MixedProfilePointCm start, ImmutableArray<MixedProfileSegmentInput> input, string parameterName)
    {
        var segments = input.Select(segment => segment switch
        {
            MixedProfileLineSegment line => (MixedProfileSegment)new MixedProfileLine(new SketchPoint(line.End.Xcm, line.End.Ycm)),
            MixedProfileThreePointArcSegment arc => new MixedProfileArc(
                new SketchPoint(arc.Through.Xcm, arc.Through.Ycm),
                new SketchPoint(arc.End.Xcm, arc.End.Ycm)),
            null => throw new ArgumentException("Mixed-profile segments cannot be null.", parameterName),
            _ => throw new ArgumentException("Unsupported mixed-profile segment.", parameterName),
        }).ToArray();

        return new MixedProfileInput(new SketchPoint(start.Xcm, start.Ycm), segments);
    }
}
