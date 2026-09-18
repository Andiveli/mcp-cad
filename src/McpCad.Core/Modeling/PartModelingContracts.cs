using McpCad.Core.Models;

namespace McpCad.Core.Modeling;

public enum CanonicalPlane
{
    Top,
    Front,
    Right,
}

public enum LengthUnit
{
    Millimeter,
    Centimeter,
    Meter,
}

public enum ExtrusionDirection
{
    Positive,
    Negative,
}

public sealed record Point2(double X, double Y);

public abstract record SketchProfile;

public sealed record RectangleProfile(Point2 FirstCorner, Point2 OppositeCorner) : SketchProfile;

public sealed record CircleProfile(Point2 Center, double Radius) : SketchProfile;

public sealed record SketchDefinition(CanonicalPlane Plane, LengthUnit Unit, SketchProfile Profile);

public sealed record ExtrusionDefinition(double Depth, LengthUnit Unit, ExtrusionDirection Direction);

public enum OperationStatus
{
    NotAttempted,
    Succeeded,
    Failed,
    Uncertain,
}

public enum MutationDisposition
{
    None,
    Present,
    Unknown,
}

public enum PartModelingErrorCode
{
    InvalidPlane,
    InvalidUnit,
    InvalidDirection,
    NonFiniteValue,
    NonPositiveValue,
    DegenerateGeometry,
    ConversionOverflow,
    ConversionCollapsed,
}

public sealed record PartModelingError(PartModelingErrorCode Code, string Detail);

public sealed record ValidationResult<T>(T? Value, PartModelingError? Error)
{
    public bool IsValid => Error is null;

    public static ValidationResult<T> Success(T value) => new(value, null);

    public static ValidationResult<T> Failure(PartModelingError error) => new(default, error);
}

public sealed class SketchReference
{
    internal SketchReference()
    {
    }

    public static SketchReference Create() => new();
}

public sealed class FeatureReference
{
    internal FeatureReference()
    {
    }

    public static FeatureReference Create() => new();
}

public sealed record OperationResult<TReference>(
    OperationStatus Status,
    MutationDisposition Mutation,
    PartModelingError? Error,
    TReference? Reference);

public sealed record PartModelingResult(
    OperationResult<SketchReference> Sketch,
    OperationResult<FeatureReference> Extrusion);

public enum OptionalCapabilityStatus
{
    Unsupported,
    Supported,
}

public enum OptionalOperationStatus
{
    NotRequested,
    Unsupported,
    Succeeded,
    Failed,
}

public sealed record PartModelingCapabilities(
    OptionalCapabilityStatus Evidence,
    OptionalCapabilityStatus Revision);

public interface IPartModelingContextFactory
{
    IPartModelingContext Bind(CadElementAddress target);
}

public interface IPartModelingContext
{
    PartModelingCapabilities Capabilities { get; }

    PartModelingResult Result { get; }

    ValueTask<OperationResult<SketchReference>> CreateSketch(
        SketchDefinition definition,
        CancellationToken cancellationToken);

    ValueTask<OperationResult<FeatureReference>> ExtrudeNewBody(
        SketchReference sketch,
        ExtrusionDefinition definition,
        CancellationToken cancellationToken);
}
