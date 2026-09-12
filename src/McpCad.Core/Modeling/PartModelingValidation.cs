namespace McpCad.Core.Modeling;

public static class PartModelingValidation
{
    public static ValidationResult<SketchDefinition> ValidateSketch(SketchDefinition? definition)
    {
        if (definition is null)
        {
            return Failure<SketchDefinition>(PartModelingErrorCode.DegenerateGeometry, "A sketch definition is required.");
        }

        if (!Enum.IsDefined(definition.Plane))
        {
            return Failure<SketchDefinition>(PartModelingErrorCode.InvalidPlane, "The canonical plane is not supported.");
        }

        if (!Enum.IsDefined(definition.Unit))
        {
            return Failure<SketchDefinition>(PartModelingErrorCode.InvalidUnit, "The length unit is not supported.");
        }

        return definition.Profile switch
        {
            RectangleProfile rectangle => ValidateRectangle(definition, rectangle),
            CircleProfile circle => ValidateCircle(definition, circle),
            _ => Failure<SketchDefinition>(PartModelingErrorCode.DegenerateGeometry, "The sketch profile is not supported."),
        };
    }

    public static ValidationResult<ExtrusionDefinition> ValidateExtrusion(ExtrusionDefinition? definition)
    {
        if (definition is null)
        {
            return Failure<ExtrusionDefinition>(PartModelingErrorCode.DegenerateGeometry, "An extrusion definition is required.");
        }

        if (!Enum.IsDefined(definition.Unit))
        {
            return Failure<ExtrusionDefinition>(PartModelingErrorCode.InvalidUnit, "The length unit is not supported.");
        }

        if (!Enum.IsDefined(definition.Direction))
        {
            return Failure<ExtrusionDefinition>(PartModelingErrorCode.InvalidDirection, "The extrusion direction is not supported.");
        }

        if (!TryConvert(definition.Depth, definition.Unit, out var depth, out var error))
        {
            return ValidationResult<ExtrusionDefinition>.Failure(error!);
        }

        if (PositiveLengthError(definition.Depth, depth, "extrusion depth") is { } positiveError)
        {
            return ValidationResult<ExtrusionDefinition>.Failure(positiveError);
        }

        return ValidationResult<ExtrusionDefinition>.Success(new ExtrusionDefinition(depth, LengthUnit.Meter, definition.Direction));
    }

    private static ValidationResult<SketchDefinition> ValidateRectangle(SketchDefinition definition, RectangleProfile rectangle)
    {
        if (!TryConvertPoint(rectangle.FirstCorner, definition.Unit, out var first, out var error)
            || !TryConvertPoint(rectangle.OppositeCorner, definition.Unit, out var opposite, out error))
        {
            return ValidationResult<SketchDefinition>.Failure(error!);
        }

        if (first.X == opposite.X || first.Y == opposite.Y)
        {
            return Failure<SketchDefinition>(PartModelingErrorCode.DegenerateGeometry, "Rectangle corners must differ on both axes after conversion.");
        }

        var lower = new Point2(Math.Min(first.X, opposite.X), Math.Min(first.Y, opposite.Y));
        var upper = new Point2(Math.Max(first.X, opposite.X), Math.Max(first.Y, opposite.Y));
        return ValidationResult<SketchDefinition>.Success(new SketchDefinition(
            definition.Plane,
            LengthUnit.Meter,
            new RectangleProfile(lower, upper)));
    }

    private static ValidationResult<SketchDefinition> ValidateCircle(SketchDefinition definition, CircleProfile circle)
    {
        if (!TryConvertPoint(circle.Center, definition.Unit, out var center, out var error)
            || !TryConvert(circle.Radius, definition.Unit, out var radius, out error))
        {
            return ValidationResult<SketchDefinition>.Failure(error!);
        }

        if (PositiveLengthError(circle.Radius, radius, "circle radius") is { } positiveError)
        {
            return ValidationResult<SketchDefinition>.Failure(positiveError);
        }

        return ValidationResult<SketchDefinition>.Success(new SketchDefinition(
            definition.Plane,
            LengthUnit.Meter,
            new CircleProfile(center, radius)));
    }

    private static bool TryConvertPoint(Point2? point, LengthUnit unit, out Point2 converted, out PartModelingError? error)
    {
        if (point is null)
        {
            converted = default!;
            error = new PartModelingError(PartModelingErrorCode.DegenerateGeometry, "A profile point is required.");
            return false;
        }

        if (TryConvert(point.X, unit, out var x, out error)
            && TryConvert(point.Y, unit, out var y, out error))
        {
            converted = new Point2(x, y);
            return true;
        }

        converted = default!;
        return false;
    }

    private static bool TryConvert(double value, LengthUnit unit, out double converted, out PartModelingError? error)
    {
        if (!double.IsFinite(value))
        {
            converted = default;
            error = new PartModelingError(PartModelingErrorCode.NonFiniteValue, "Lengths must be finite.");
            return false;
        }

        converted = value * UnitScale(unit);
        if (!double.IsFinite(converted))
        {
            error = new PartModelingError(PartModelingErrorCode.ConversionOverflow, "Length conversion overflowed.");
            return false;
        }

        error = null;
        return true;
    }

    private static PartModelingError? PositiveLengthError(double original, double converted, string name)
    {
        if (original <= 0)
        {
            return new PartModelingError(PartModelingErrorCode.NonPositiveValue, $"The {name} must be positive.");
        }

        return converted <= 0
            ? new PartModelingError(PartModelingErrorCode.ConversionCollapsed, $"The converted {name} must be positive.")
            : null;
    }

    private static double UnitScale(LengthUnit unit) => unit switch
    {
        LengthUnit.Millimeter => 0.001,
        LengthUnit.Centimeter => 0.01,
        LengthUnit.Meter => 1,
        _ => throw new ArgumentOutOfRangeException(nameof(unit)),
    };

    private static ValidationResult<T> Failure<T>(PartModelingErrorCode code, string detail) =>
        ValidationResult<T>.Failure(new PartModelingError(code, detail));
}
