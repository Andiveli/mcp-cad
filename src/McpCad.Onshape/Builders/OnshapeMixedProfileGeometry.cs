using System.Collections.Immutable;

namespace McpCad.Onshape.Builders;

internal abstract record MixedProfileSegment(SketchPoint End);
internal sealed record MixedProfileLine(SketchPoint End) : MixedProfileSegment(End);
internal sealed record MixedProfileArc(SketchPoint Through, SketchPoint End) : MixedProfileSegment(End);
internal sealed record MixedProfileInput(SketchPoint Start, IReadOnlyList<MixedProfileSegment> Segments);

internal enum MixedProfileSegmentKind { Line, Arc }

/// <summary>Canonical meter-space boundary prepared for later whole-profile validation and sketch emission.</summary>
internal sealed record NormalizedMixedProfile(
    SketchPoint Start,
    ImmutableArray<NormalizedMixedProfileSegment> Segments);

internal sealed record NormalizedMixedProfileSegment(
    MixedProfileSegmentKind Kind,
    SketchPoint Start,
    SketchPoint End,
    SketchPoint? Through = null,
    NormalizedMixedProfileArc? Arc = null);

internal sealed record NormalizedMixedProfileArc(
    SketchPoint Center,
    double Radius,
    bool Clockwise,
    double StartParameter,
    double EndParameter,
    double Sweep);

internal static class OnshapeMixedProfileGeometry
{
    internal const int MinimumMixedArcCount = 1;
        internal const int MaximumMixedArcCount = 8;
        private const double ArcTolerance = 1e-12;

    /// <summary>Normalizes the existing single-arc profile facade.</summary>
    public static NormalizedMixedProfile Normalize(MixedProfileInput input) => Normalize(input, MinimumMixedArcCount, MinimumMixedArcCount);

    internal static NormalizedMixedProfile Normalize(MixedProfileInput input, int minimumArcCount, int maximumArcCount)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(input.Segments);
        if (minimumArcCount < 0 || maximumArcCount < minimumArcCount)
            throw new ArgumentOutOfRangeException(nameof(minimumArcCount));
        if (input.Segments.Count is < 2 or > 256)
            throw new ArgumentException("A mixed profile requires 2 to 256 segments.", nameof(input));
        var arcCount = input.Segments.Count(segment => segment is MixedProfileArc);
        if (arcCount < minimumArcCount || arcCount > maximumArcCount)
            throw new ArgumentException(
                minimumArcCount == maximumArcCount
                    ? minimumArcCount == 1
                        ? "A mixed profile requires exactly one arc."
                        : $"A mixed profile requires exactly {minimumArcCount} arcs."
                    : $"A mixed profile requires {minimumArcCount} to {maximumArcCount} arcs.",
                nameof(input));

        ValidateFinite(input.Start, nameof(input));
        foreach (var segment in input.Segments)
        {
            ArgumentNullException.ThrowIfNull(segment);
            ValidateFinite(segment.End, nameof(input));
            if (segment is MixedProfileArc arc) ValidateFinite(arc.Through, nameof(input));
        }

        var start = Convert(input.Start);
        var converted = input.Segments.Select(segment => segment switch
        {
            MixedProfileLine line => (MixedProfileSegment)new MixedProfileLine(Convert(line.End)),
            MixedProfileArc arc => new MixedProfileArc(Convert(arc.Through), Convert(arc.End)),
            _ => throw new ArgumentException("Unsupported mixed-profile segment.", nameof(input)),
        }).ToArray();
        ValidateFinite(start, nameof(input));
        foreach (var segment in converted)
        {
            ValidateFinite(segment.End, nameof(input));
            if (segment is MixedProfileArc arc) ValidateFinite(arc.Through, nameof(input));
        }
        if (converted[^1].End != start)
            throw new ArgumentException("The final endpoint must close to the initial start after conversion.", nameof(input));

        var vertices = new[] { start }.Concat(converted.Select(segment => segment.End)).ToArray();
        if (vertices.Take(vertices.Length - 1).Distinct().Count() != vertices.Length - 1)
            throw new ArgumentException("Canonical boundary vertices cannot repeat.", nameof(input));

        var normalized = ImmutableArray.CreateBuilder<NormalizedMixedProfileSegment>(converted.Length);
        var segmentStart = start;
        foreach (var segment in converted)
        {
            if (segment is MixedProfileLine line)
            {
                if (line.End == segmentStart)
                    throw new ArgumentException("Mixed-profile lines cannot have zero length.", nameof(input));
                normalized.Add(new(MixedProfileSegmentKind.Line, segmentStart, line.End));
            }
            else if (segment is MixedProfileArc arc)
            {
                if (arc.Through == segmentStart || arc.Through == arc.End || arc.End == segmentStart)
                    throw new ArgumentException("Mixed-profile arc points cannot collapse.", nameof(input));
                normalized.Add(new(MixedProfileSegmentKind.Arc, segmentStart, arc.End, arc.Through,
                    Circumgeometry(segmentStart, arc.Through, arc.End, nameof(input))));
            }
            segmentStart = segment.End;
        }
        return new(start, normalized.MoveToImmutable());
    }

    private static NormalizedMixedProfileArc Circumgeometry(SketchPoint start, SketchPoint through, SketchPoint end, string parameterName)
    {
        var bx = through.X - start.X; var by = through.Y - start.Y;
        var cx = end.X - start.X; var cy = end.Y - start.Y;
        var scale = Math.Max(Math.Max(Math.Abs(bx), Math.Abs(by)), Math.Max(Math.Abs(cx), Math.Abs(cy)));
        if (!double.IsFinite(scale) || scale <= 0) throw new ArgumentException("Arc is ill-conditioned.", parameterName);
        bx /= scale; by /= scale; cx /= scale; cy /= scale;
        var determinant = 2 * (bx * cy - by * cx);
        if (!double.IsFinite(determinant) || Math.Abs(determinant) <= ArcTolerance)
            throw new ArgumentException("Arc points are collinear or ill-conditioned.", parameterName);
        var b2 = bx * bx + by * by; var c2 = cx * cx + cy * cy;
        var ux = (b2 * cy - c2 * by) / determinant;
        var uy = (c2 * bx - b2 * cx) / determinant;
        var center = new SketchPoint(start.X + ux * scale, start.Y + uy * scale);
        var radius = Math.Sqrt(ux * ux + uy * uy) * scale;
        var startAngle = Math.Atan2(start.Y - center.Y, start.X - center.X);
        var throughAngle = Math.Atan2(through.Y - center.Y, through.X - center.X);
        var endAngle = Math.Atan2(end.Y - center.Y, end.X - center.X);
        if (!double.IsFinite(center.X) || !double.IsFinite(center.Y) || !double.IsFinite(radius)
            || radius <= 0 || !double.IsFinite(startAngle) || !double.IsFinite(throughAngle) || !double.IsFinite(endAngle))
            throw new ArgumentException("Arc circumgeometry is not finite.", parameterName);

        var ccwEnd = PositiveAngle(endAngle - startAngle);
        var ccwThrough = PositiveAngle(throughAngle - startAngle);
        var clockwise = ccwThrough > ccwEnd;
        var sweep = clockwise ? -(2 * Math.PI - ccwEnd) : ccwEnd;
        if (Math.Abs(sweep) <= ArcTolerance || 2 * Math.PI - Math.Abs(sweep) <= ArcTolerance)
            throw new ArgumentException("Arc sweep must be neither near-zero nor full-circle.", parameterName);
        return new(center, radius, clockwise, startAngle, startAngle + sweep, sweep);
    }

    private static SketchPoint Convert(SketchPoint point) => new(
        OnshapeUnitConversion.CmToMeters(point.X), OnshapeUnitConversion.CmToMeters(point.Y));

    private static double PositiveAngle(double angle)
    {
        var result = angle % (2 * Math.PI);
        return result < 0 ? result + 2 * Math.PI : result;
    }

    private static void ValidateFinite(SketchPoint point, string parameterName)
    {
        if (!double.IsFinite(point.X) || !double.IsFinite(point.Y))
            throw new ArgumentException("Mixed-profile coordinates must be finite.", parameterName);
    }
}
