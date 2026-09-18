using McpCad.Core.Exceptions;
using McpCad.Core.Modeling;

namespace McpCad.Onshape.Modeling;

internal enum OnshapePartModelingGeometryMapFailure
{
    InvalidInput,
    UnsupportedFrame,
}

internal sealed class OnshapePartModelingGeometryMapException : CadProviderException
{
    public OnshapePartModelingGeometryMapException(OnshapePartModelingGeometryMapFailure failure)
        : base("The Onshape geometry mapping input is invalid.")
    {
        Failure = failure;
    }

    public OnshapePartModelingGeometryMapFailure Failure { get; }
}

internal readonly record struct OnshapeMappedPoint(double X, double Y);

internal abstract record OnshapeMappedProfile;

/// <summary>All coordinates are meters; a future adapter converts only when passing a closed profile builder.</summary>
internal sealed record OnshapeMappedRectangle(IReadOnlyList<OnshapeMappedPoint> CornersM) : OnshapeMappedProfile;

/// <summary>Center and radius are meters and can be passed directly to the circle builder boundary.</summary>
internal sealed record OnshapeMappedCircle(OnshapeMappedPoint CenterM, double RadiusM) : OnshapeMappedProfile;

internal sealed record OnshapeMappedExtrusion(double DepthM, ExtrusionDirection Direction);

internal sealed record OnshapePartModelingGeometry(OnshapeMappedProfile Profile, OnshapeMappedExtrusion Extrusion);

internal static class OnshapePartModelingGeometryMapper
{
    private const double BasisTolerance = 1e-9;
    private const double OriginToleranceM = 1e-8;

    internal static OnshapePartModelingGeometry Map(
        SketchDefinition definition,
        ExtrusionDefinition extrusion,
        OnshapePartModelingFrame frame)
    {
        ValidateNormalizedInput(definition, extrusion);
        var canonical = GetCanonicalFrame(definition.Plane);
        var normalSign = ValidateFrame(frame, canonical);
        OnshapeMappedProfile profile = definition.Profile switch
        {
            RectangleProfile rectangle => MapRectangle(rectangle, canonical, frame),
            CircleProfile circle => new OnshapeMappedCircle(Project(circle.Center, canonical, frame), circle.Radius),
            _ => throw Failure(OnshapePartModelingGeometryMapFailure.InvalidInput),
        };
        var direction = normalSign > 0 ? extrusion.Direction : Flip(extrusion.Direction);
        return new OnshapePartModelingGeometry(profile, new OnshapeMappedExtrusion(extrusion.Depth, direction));
    }

    private static OnshapeMappedRectangle MapRectangle(
        RectangleProfile rectangle, CanonicalFrame canonical, OnshapePartModelingFrame frame)
    {
        var first = rectangle.FirstCorner;
        var opposite = rectangle.OppositeCorner;
        return new OnshapeMappedRectangle(
        [
            Project(first, canonical, frame),
            Project(new Point2(opposite.X, first.Y), canonical, frame),
            Project(opposite, canonical, frame),
            Project(new Point2(first.X, opposite.Y), canonical, frame),
        ]);
    }

    private static OnshapeMappedPoint Project(Point2 point, CanonicalFrame canonical, OnshapePartModelingFrame frame)
    {
        var world = Add(Scale(canonical.XAxis, point.X), Scale(canonical.YAxis, point.Y));
        var relative = Subtract(world, frame.Origin);
        var mapped = new OnshapeMappedPoint(Dot(frame.XAxis, relative), Dot(frame.YAxis, relative));
        return double.IsFinite(mapped.X) && double.IsFinite(mapped.Y)
            ? mapped
            : throw Failure(OnshapePartModelingGeometryMapFailure.InvalidInput);
    }

    private static int ValidateFrame(OnshapePartModelingFrame? frame, CanonicalFrame canonical)
    {
        if (frame is null || !Finite(frame.Origin) || !Finite(frame.XAxis) || !Finite(frame.YAxis) || !Finite(frame.Normal)
            || !NearZero(frame.Origin) || Math.Abs(Dot(frame.Origin, canonical.Normal)) > OriginToleranceM
            || !Unit(frame.XAxis) || !Unit(frame.YAxis) || !Unit(frame.Normal)
            || !Orthogonal(frame.XAxis, frame.YAxis) || !Orthogonal(frame.XAxis, frame.Normal) || !Orthogonal(frame.YAxis, frame.Normal)
            || !Near(Cross(frame.XAxis, frame.YAxis), frame.Normal))
        {
            throw Failure(OnshapePartModelingGeometryMapFailure.UnsupportedFrame);
        }

        var normalAlignment = Dot(frame.Normal, canonical.Normal);
        if (!double.IsFinite(normalAlignment) || Math.Abs(Math.Abs(normalAlignment) - 1) > BasisTolerance)
        {
            throw Failure(OnshapePartModelingGeometryMapFailure.UnsupportedFrame);
        }

        var m11 = Dot(frame.XAxis, canonical.XAxis);
        var m12 = Dot(frame.XAxis, canonical.YAxis);
        var m21 = Dot(frame.YAxis, canonical.XAxis);
        var m22 = Dot(frame.YAxis, canonical.YAxis);
        var determinant = m11 * m22 - m12 * m21;
        if (!double.IsFinite(determinant)
            || Math.Abs(m11 * m11 + m21 * m21 - 1) > BasisTolerance
            || Math.Abs(m12 * m12 + m22 * m22 - 1) > BasisTolerance
            || Math.Abs(m11 * m12 + m21 * m22) > BasisTolerance
            || Math.Abs(Math.Abs(determinant) - 1) > BasisTolerance
            || Math.Abs(determinant - normalAlignment) > BasisTolerance)
        {
            throw Failure(OnshapePartModelingGeometryMapFailure.UnsupportedFrame);
        }

        return normalAlignment > 0 ? 1 : -1;
    }

    private static void ValidateNormalizedInput(SketchDefinition? definition, ExtrusionDefinition? extrusion)
    {
        if (definition is null || extrusion is null || !Enum.IsDefined(definition.Plane)
            || definition.Unit != LengthUnit.Meter || extrusion.Unit != LengthUnit.Meter
            || !Enum.IsDefined(extrusion.Direction) || !double.IsFinite(extrusion.Depth) || extrusion.Depth <= 0)
        {
            throw Failure(OnshapePartModelingGeometryMapFailure.InvalidInput);
        }

        switch (definition.Profile)
        {
            case RectangleProfile rectangle when Finite(rectangle.FirstCorner) && Finite(rectangle.OppositeCorner)
                && rectangle.FirstCorner.X != rectangle.OppositeCorner.X && rectangle.FirstCorner.Y != rectangle.OppositeCorner.Y:
                return;
            case CircleProfile circle when Finite(circle.Center) && double.IsFinite(circle.Radius) && circle.Radius > 0:
                return;
            default:
                throw Failure(OnshapePartModelingGeometryMapFailure.InvalidInput);
        }
    }

    private static CanonicalFrame GetCanonicalFrame(CanonicalPlane plane) => plane switch
    {
        CanonicalPlane.Top => new(new OnshapeFrameVector(1, 0, 0), new OnshapeFrameVector(0, 1, 0), new OnshapeFrameVector(0, 0, 1)),
        CanonicalPlane.Front => new(new OnshapeFrameVector(1, 0, 0), new OnshapeFrameVector(0, 0, 1), new OnshapeFrameVector(0, -1, 0)),
        CanonicalPlane.Right => new(new OnshapeFrameVector(0, 1, 0), new OnshapeFrameVector(0, 0, 1), new OnshapeFrameVector(1, 0, 0)),
        _ => throw Failure(OnshapePartModelingGeometryMapFailure.InvalidInput),
    };

    private static ExtrusionDirection Flip(ExtrusionDirection direction) => direction switch
    {
        ExtrusionDirection.Positive => ExtrusionDirection.Negative,
        ExtrusionDirection.Negative => ExtrusionDirection.Positive,
        _ => throw Failure(OnshapePartModelingGeometryMapFailure.InvalidInput),
    };

    private static bool Finite(Point2? point) => point is not null && double.IsFinite(point.X) && double.IsFinite(point.Y);

    private static bool Finite(OnshapeFrameVector vector) =>
        double.IsFinite(vector.X) && double.IsFinite(vector.Y) && double.IsFinite(vector.Z);

    private static bool NearZero(OnshapeFrameVector vector) =>
        Math.Abs(vector.X) <= OriginToleranceM && Math.Abs(vector.Y) <= OriginToleranceM && Math.Abs(vector.Z) <= OriginToleranceM;

    private static bool Unit(OnshapeFrameVector vector) => Math.Abs(Dot(vector, vector) - 1) <= BasisTolerance;

    private static bool Orthogonal(OnshapeFrameVector left, OnshapeFrameVector right) => Math.Abs(Dot(left, right)) <= BasisTolerance;

    private static bool Near(OnshapeFrameVector left, OnshapeFrameVector right) =>
        Math.Abs(left.X - right.X) <= BasisTolerance && Math.Abs(left.Y - right.Y) <= BasisTolerance && Math.Abs(left.Z - right.Z) <= BasisTolerance;

    private static double Dot(OnshapeFrameVector left, OnshapeFrameVector right) => left.X * right.X + left.Y * right.Y + left.Z * right.Z;

    private static OnshapeFrameVector Cross(OnshapeFrameVector left, OnshapeFrameVector right) => new(
        left.Y * right.Z - left.Z * right.Y,
        left.Z * right.X - left.X * right.Z,
        left.X * right.Y - left.Y * right.X);

    private static OnshapeFrameVector Add(OnshapeFrameVector left, OnshapeFrameVector right) => new(left.X + right.X, left.Y + right.Y, left.Z + right.Z);

    private static OnshapeFrameVector Subtract(OnshapeFrameVector left, OnshapeFrameVector right) => new(left.X - right.X, left.Y - right.Y, left.Z - right.Z);

    private static OnshapeFrameVector Scale(OnshapeFrameVector vector, double scale) => new(vector.X * scale, vector.Y * scale, vector.Z * scale);

    private static OnshapePartModelingGeometryMapException Failure(OnshapePartModelingGeometryMapFailure failure) => new(failure);

    private sealed record CanonicalFrame(OnshapeFrameVector XAxis, OnshapeFrameVector YAxis, OnshapeFrameVector Normal);
}
