namespace McpCad.Onshape.Builders;

/// <summary>Whole-boundary contact validation and Green-theorem area for one- to eight-arc profiles.</summary>
internal sealed record ValidatedMixedProfile(NormalizedMixedProfile Profile, double SignedArea);

internal static class OnshapeMixedProfileTopology
{
    private const double RelativeTolerance = 1e-10;
    private const double MinimumArea = 1e-10; // square metres

    public static ValidatedMixedProfile Validate(MixedProfileInput input) => Validate(OnshapeMixedProfileGeometry.Normalize(input, OnshapeMixedProfileGeometry.MinimumMixedArcCount, OnshapeMixedProfileGeometry.MaximumMixedArcCount));

    public static ValidatedMixedProfile Validate(NormalizedMixedProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        var segments = profile.Segments;
        if (segments.Count(segment => segment.Kind == MixedProfileSegmentKind.Arc) is < OnshapeMixedProfileGeometry.MinimumMixedArcCount or > OnshapeMixedProfileGeometry.MaximumMixedArcCount)
            throw new ArgumentException("A mixed profile requires one to eight arcs.", nameof(profile));
        var tolerance = RelativeTolerance * Scale(profile);
        for (var i = 0; i < segments.Length; i++)
            for (var j = i + 1; j < segments.Length; j++)
                ValidatePair(segments[i], segments[j], AreAdjacent(i, j, segments.Length), tolerance);
        var area = SignedArea(segments);
        if (!double.IsFinite(area) || Math.Abs(area) <= MinimumArea)
            throw new ArgumentException("Mixed-profile area is non-finite or negligible.", nameof(profile));
        return new(profile, area);
    }

    private static void ValidatePair(NormalizedMixedProfileSegment a, NormalizedMixedProfileSegment b, bool adjacent, double tolerance)
    {
        if (a.Kind == MixedProfileSegmentKind.Line && b.Kind == MixedProfileSegmentKind.Line)
            ValidateLines(a, b, adjacent, tolerance);
        else if (a.Kind == MixedProfileSegmentKind.Line)
            ValidateLineArc(a, b, adjacent, tolerance);
        else if (b.Kind == MixedProfileSegmentKind.Line)
            ValidateLineArc(b, a, adjacent, tolerance);
        else
            ValidateArcArc(a, b, adjacent, tolerance);
    }

    private static void ValidateArcArc(NormalizedMixedProfileSegment a, NormalizedMixedProfileSegment b, bool adjacent, double tolerance)
    {
        var first = a.Arc ?? throw new ArgumentException("Arc geometry is required.");
        var second = b.Arc ?? throw new ArgumentException("Arc geometry is required.");
        var delta = Subtract(second.Center, first.Center); var distance = Math.Sqrt(Dot(delta, delta));
        var local = Math.Max(Math.Max(first.Radius, second.Radius), distance);
        var lengthTolerance = 4 * Math.Max(tolerance, RelativeTolerance * Math.Max(1, local));
        if (!double.IsFinite(distance)) Reject();
        var radiusDifference = Math.Abs(first.Radius - second.Radius);
        if (distance == 0)
        {
            if (radiusDifference > lengthTolerance) return;
            ValidateCoincidentArcs(a, b, adjacent, lengthTolerance);
            return;
        }
        if (distance <= lengthTolerance)
        {
            if (radiusDifference > lengthTolerance) return;
            Reject();
        }
        var sum = first.Radius + second.Radius; var difference = radiusDifference;
        if (distance > sum + lengthTolerance || distance < difference - lengthTolerance) return;
        var externalTangentOffset = Math.Abs(distance - sum); var internalTangentOffset = Math.Abs(distance - difference);
        var tangent = externalTangentOffset <= lengthTolerance || internalTangentOffset <= lengthTolerance;
        if (tangent && externalTangentOffset != 0 && internalTangentOffset != 0) Reject();
        var along = (first.Radius * first.Radius - second.Radius * second.Radius + distance * distance) / (2 * distance);
        var heightSquared = first.Radius * first.Radius - along * along;
        var heightTolerance = RelativeTolerance * Math.Max(Math.Max(first.Radius * first.Radius, along * along), local * local);
        if (!double.IsFinite(heightSquared) || heightSquared < -heightTolerance) Reject();
        if (tangent) heightSquared = 0;
        else if (Math.Abs(heightSquared) <= heightTolerance) Reject();
        var basePoint = Add(first.Center, Scale(delta, along / distance)); var contacts = 0;
        if (heightSquared == 0) ValidateArcArcContact(a, b, basePoint, adjacent, lengthTolerance, ref contacts);
        else
        {
            var height = Math.Sqrt(heightSquared); var offset = new SketchPoint(-delta.Y * height / distance, delta.X * height / distance);
            ValidateArcArcContact(a, b, Add(basePoint, offset), adjacent, lengthTolerance, ref contacts);
            ValidateArcArcContact(a, b, Subtract(basePoint, offset), adjacent, lengthTolerance, ref contacts);
        }
        if (contacts > 1) Reject();
    }

    private static void ValidateArcArcContact(NormalizedMixedProfileSegment a, NormalizedMixedProfileSegment b, SketchPoint point, bool adjacent, double tolerance, ref int contacts)
    {
        var first = ArcMembership(point, a.Arc!, tolerance); var second = ArcMembership(point, b.Arc!, tolerance);
        if (first == 0 || second == 0) return;
        if (first < 0 || second < 0 || !adjacent || !IsDesignatedArcArcJoin(a, b, point, first, second, tolerance)) Reject();
        contacts++;
    }

    private static void ValidateCoincidentArcs(NormalizedMixedProfileSegment a, NormalizedMixedProfileSegment b, bool adjacent, double tolerance)
    {
        var first = a.Arc!; var second = b.Arc!; var angleTolerance = AngularTolerance(first, tolerance);
        foreach (var left in DirectedIntervals(first)) foreach (var right in DirectedIntervals(second))
            if (Math.Min(left.End, right.End) - Math.Max(left.Start, right.Start) > angleTolerance) Reject();
        var contacts = new List<SketchPoint>();
        foreach (var point in new[] { a.Start, a.End, b.Start, b.End })
        {
            var firstMembership = ArcMembership(point, first, tolerance); var secondMembership = ArcMembership(point, second, tolerance);
            if (firstMembership <= 1 || secondMembership <= 1 || contacts.Any(existing => Distance(existing, point) <= tolerance)) continue;
            if (!adjacent || !IsDesignatedArcArcJoin(a, b, point, firstMembership, secondMembership, tolerance)) Reject();
            contacts.Add(point);
        }
        if (contacts.Count > 1) Reject();
    }

    private static IEnumerable<(double Start, double End)> DirectedIntervals(NormalizedMixedProfileArc arc)
    {
        var start = arc.Sweep > 0 ? Positive(arc.StartParameter) : Positive(arc.EndParameter); var end = start + Math.Abs(arc.Sweep);
        if (end <= 2 * Math.PI) { yield return (start, end); yield break; }
        yield return (start, 2 * Math.PI); yield return (0, end - 2 * Math.PI);
    }

    private static bool IsDesignatedArcArcJoin(NormalizedMixedProfileSegment a, NormalizedMixedProfileSegment b, SketchPoint point, int first, int second, double tolerance) =>
        first == 3 && second == 2 && Distance(point, a.End) <= tolerance && Distance(point, b.Start) <= tolerance
        || first == 2 && second == 3 && Distance(point, a.Start) <= tolerance && Distance(point, b.End) <= tolerance;

    private static void ValidateLines(NormalizedMixedProfileSegment a, NormalizedMixedProfileSegment b, bool adjacent, double tolerance)
    {
        var r = Subtract(a.End, a.Start); var s = Subtract(b.End, b.Start);
        var qmp = Subtract(b.Start, a.Start); var denominator = Cross(r, s);
        if (Math.Abs(denominator) <= tolerance)
        {
            if (Math.Abs(Cross(qmp, r)) > tolerance) return;
            if (adjacent && Dot(r, s) > tolerance) return;
            if (!adjacent && !BoxesOverlap(a.Start, a.End, b.Start, b.End, tolerance)) return;
            Reject();
        }
        var t = Cross(qmp, s) / denominator; var u = Cross(qmp, r) / denominator;
        if (InOrNearUnit(t, tolerance) && InOrNearUnit(u, tolerance))
        {
            if (adjacent && IsDesignatedJoin(a, b, t, u, tolerance)) return;
            Reject();
        }
    }

    private static void ValidateLineArc(NormalizedMixedProfileSegment line, NormalizedMixedProfileSegment arcSegment, bool adjacent, double tolerance)
    {
        var arc = arcSegment.Arc ?? throw new ArgumentException("Arc geometry is required.");
        var direction = Subtract(line.End, line.Start); var offset = Subtract(line.Start, arc.Center);
        var a = Dot(direction, direction); var b = 2 * Dot(offset, direction); var c = Dot(offset, offset) - arc.Radius * arc.Radius;
        var discriminant = b * b - 4 * a * c;
        var localScale = Math.Max(arc.Radius, Math.Max(Math.Sqrt(a), Distance(line.Start, arc.Center)));
        var discriminantTolerance = RelativeTolerance * Math.Max(Math.Pow(localScale, 4), Math.Max(Math.Abs(b * b), Math.Abs(4 * a * c)));
        if (!double.IsFinite(discriminant) || !double.IsFinite(discriminantTolerance)) Reject();
        if (discriminant < -discriminantTolerance) return;
        if (Math.Abs(discriminant) <= discriminantTolerance)
        {
            var t = -b / (2 * a);
            if (!InOrNearUnit(t, tolerance)) return;
            var point = Add(line.Start, Scale(direction, t));
            var membership = ArcMembership(point, arc, tolerance);
            if (membership == 0) return;
            if (membership < 0 || !adjacent || !IsDesignatedLineArcJoin(line, arcSegment, point, membership, tolerance)) Reject();
            return;
        }

        var root = Math.Sqrt(discriminant);
        CheckRoot((-b - root) / (2 * a));
        CheckRoot((-b + root) / (2 * a));

        void CheckRoot(double t)
        {
            if (!InOrNearUnit(t, tolerance)) return;
            var point = Add(line.Start, Scale(direction, t));
            var membership = ArcMembership(point, arc, tolerance);
            if (membership == 0) return;
            if (membership < 0 || !adjacent || !IsDesignatedLineArcJoin(line, arcSegment, point, membership, tolerance)) Reject();
        }
    }

    // 0 = outside, 1 = clearly on bounded arc, 2 = at its start, 3 = at its end; -1 = angular ambiguity.
    private static int ArcMembership(SketchPoint point, NormalizedMixedProfileArc arc, double tolerance)
    {
        var radialDistance = Distance(point, arc.Center);
        if (!double.IsFinite(radialDistance) || Math.Abs(radialDistance - arc.Radius) > tolerance) return 0;
        var angle = Math.Atan2(point.Y - arc.Center.Y, point.X - arc.Center.X);
        if (!double.IsFinite(angle)) return -1;
        var angularTolerance = AngularTolerance(arc, tolerance);
        var travelled = arc.Sweep >= 0 ? Positive(angle - arc.StartParameter) : -Positive(arc.StartParameter - angle);
        var startDistance = Distance(point, PointOnArc(arc, arc.StartParameter));
        var endDistance = Distance(point, PointOnArc(arc, arc.EndParameter));
        if (startDistance <= tolerance) return 2;
        if (endDistance <= tolerance) return 3;
        if (arc.Sweep > 0 && travelled > angularTolerance && travelled < arc.Sweep - angularTolerance) return 1;
        if (arc.Sweep < 0 && travelled < -angularTolerance && travelled > arc.Sweep + angularTolerance) return 1;
        return Math.Abs(travelled) <= angularTolerance || Math.Abs(travelled - arc.Sweep) <= angularTolerance ? -1 : 0;
    }

    private static double AngularTolerance(NormalizedMixedProfileArc arc, double lengthTolerance) => Math.Max(RelativeTolerance, lengthTolerance / arc.Radius);

    private static bool IsDesignatedLineArcJoin(NormalizedMixedProfileSegment line, NormalizedMixedProfileSegment arc, SketchPoint point, int membership, double tolerance) =>
        membership == 2 && Distance(point, line.End) <= tolerance && Distance(point, arc.Start) <= tolerance
        || membership == 3 && Distance(point, line.Start) <= tolerance && Distance(point, arc.End) <= tolerance;

    private static bool IsDesignatedJoin(NormalizedMixedProfileSegment a, NormalizedMixedProfileSegment b, double t, double u, double tolerance)
    {
        var point = Add(a.Start, Scale(Subtract(a.End, a.Start), t));
        return Distance(point, a.End) <= tolerance && Distance(point, b.Start) <= tolerance
            || Distance(point, a.Start) <= tolerance && Distance(point, b.End) <= tolerance;
    }

    private static double SignedArea(IEnumerable<NormalizedMixedProfileSegment> segments)
    {
        var integral = 0d;
        foreach (var segment in segments)
            integral += segment.Arc is { } arc
                ? Cross(arc.Center, Subtract(segment.End, segment.Start)) + arc.Radius * arc.Radius * arc.Sweep
                : Cross(segment.Start, segment.End);
        return integral / 2;
    }

    private static bool AreAdjacent(int a, int b, int count) => b == a + 1 || a == 0 && b == count - 1;
    private static bool InOrNearUnit(double value, double tolerance) => double.IsFinite(value) && value >= -tolerance && value <= 1 + tolerance;
    private static bool BoxesOverlap(SketchPoint a, SketchPoint b, SketchPoint c, SketchPoint d, double tolerance) => Math.Max(Math.Min(a.X, b.X), Math.Min(c.X, d.X)) <= Math.Min(Math.Max(a.X, b.X), Math.Max(c.X, d.X)) + tolerance && Math.Max(Math.Min(a.Y, b.Y), Math.Min(c.Y, d.Y)) <= Math.Min(Math.Max(a.Y, b.Y), Math.Max(c.Y, d.Y)) + tolerance;
    private static double Scale(NormalizedMixedProfile profile) => Math.Max(1, profile.Segments.SelectMany(s => new[] { Math.Abs(s.Start.X), Math.Abs(s.Start.Y), Math.Abs(s.End.X), Math.Abs(s.End.Y) }).Max());
    private static SketchPoint Subtract(SketchPoint a, SketchPoint b) => new(a.X - b.X, a.Y - b.Y);
    private static SketchPoint Add(SketchPoint a, SketchPoint b) => new(a.X + b.X, a.Y + b.Y);
    private static SketchPoint Scale(SketchPoint point, double scalar) => new(point.X * scalar, point.Y * scalar);
    private static SketchPoint PointOnArc(NormalizedMixedProfileArc arc, double parameter) => new(arc.Center.X + arc.Radius * Math.Cos(parameter), arc.Center.Y + arc.Radius * Math.Sin(parameter));
    private static double Cross(SketchPoint a, SketchPoint b) => a.X * b.Y - a.Y * b.X;
    private static double Dot(SketchPoint a, SketchPoint b) => a.X * b.X + a.Y * b.Y;
    private static double Distance(SketchPoint a, SketchPoint b) => Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));
    private static double Positive(double angle) { var value = angle % (2 * Math.PI); return value < 0 ? value + 2 * Math.PI : value; }
    private static void Reject() => throw new ArgumentException("Mixed-profile boundary has an ambiguous or invalid contact.");
}
