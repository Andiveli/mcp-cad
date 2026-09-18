using System.Text.Json;

namespace McpCad.Onshape.Modeling;

internal static class OnshapePartModelingFrameParser
{
    private const double BasisTolerance = 1e-9;

    internal static OnshapePartModelingFrame Parse(JsonElement response)
    {
        if (response.ValueKind != JsonValueKind.Object)
        {
            throw Failure(OnshapePartModelingFrameReadFailure.MalformedFrame);
        }
        if (response.TryGetProperty("notices", out var notices)
            && (notices.ValueKind != JsonValueKind.Array || notices.GetArrayLength() != 0))
        {
            throw Failure(OnshapePartModelingFrameReadFailure.Notice);
        }
        if (response.TryGetProperty("microversionSkew", out var skew)
            && (skew.ValueKind != JsonValueKind.False && skew.ValueKind != JsonValueKind.True
                || skew.ValueKind == JsonValueKind.True))
        {
            throw Failure(skew.ValueKind == JsonValueKind.True
                ? OnshapePartModelingFrameReadFailure.MicroversionSkew
                : OnshapePartModelingFrameReadFailure.MalformedFrame);
        }
        if (!response.TryGetProperty("result", out var result))
        {
            throw Failure(OnshapePartModelingFrameReadFailure.MalformedFrame);
        }

        var rows = ArrayValue(result);
        if (rows.GetArrayLength() != 4)
        {
            throw Failure(OnshapePartModelingFrameReadFailure.MalformedFrame);
        }
        var values = new Dictionary<string, OnshapeFrameVector>(StringComparer.Ordinal);
        foreach (var row in rows.EnumerateArray())
        {
            var parsed = ParseRow(row);
            if (!values.TryAdd(parsed.Name, parsed.Vector))
            {
                throw Failure(OnshapePartModelingFrameReadFailure.MalformedFrame);
            }
        }
        if (!values.TryGetValue("origin", out var origin)
            || !values.TryGetValue("x", out var xAxis)
            || !values.TryGetValue("y", out var yAxis)
            || !values.TryGetValue("normal", out var normal)
            || values.Count != 4)
        {
            throw Failure(OnshapePartModelingFrameReadFailure.MalformedFrame);
        }

        var frame = new OnshapePartModelingFrame(origin, xAxis, yAxis, normal);
        return IsRigidRightHanded(frame) ? frame : throw Failure(OnshapePartModelingFrameReadFailure.MalformedFrame);
    }

    private static (string Name, OnshapeFrameVector Vector) ParseRow(JsonElement value)
    {
        var items = ArrayValue(value);
        if (items.GetArrayLength() != 4)
        {
            throw Failure(OnshapePartModelingFrameReadFailure.MalformedFrame);
        }
        var enumerator = items.EnumerateArray();
        enumerator.MoveNext();
        var name = StringValue(enumerator.Current);
        enumerator.MoveNext();
        var x = NumberValue(enumerator.Current);
        enumerator.MoveNext();
        var y = NumberValue(enumerator.Current);
        enumerator.MoveNext();
        return (name, new OnshapeFrameVector(x, y, NumberValue(enumerator.Current)));
    }

    private static JsonElement ArrayValue(JsonElement value)
    {
        var items = Value(value, "BTFSValueArray-1499", "BTFSValueArray");
        return items.ValueKind == JsonValueKind.Array
            ? items
            : throw Failure(OnshapePartModelingFrameReadFailure.MalformedFrame);
    }

    private static string StringValue(JsonElement value)
    {
        var item = Value(value, "BTFSValueString-1422", "BTFSValueString");
        return item.ValueKind == JsonValueKind.String && !string.IsNullOrEmpty(item.GetString())
            ? item.GetString()!
            : throw Failure(OnshapePartModelingFrameReadFailure.MalformedFrame);
    }

    private static double NumberValue(JsonElement value)
    {
        var item = Value(value, "BTFSValueNumber-772", "BTFSValueNumber");
        return item.ValueKind == JsonValueKind.Number && item.TryGetDouble(out var number) && double.IsFinite(number)
            ? number
            : throw Failure(OnshapePartModelingFrameReadFailure.MalformedFrame);
    }

    private static JsonElement Value(JsonElement item, string current, string legacy)
    {
        if (item.ValueKind != JsonValueKind.Object || !item.TryGetProperty("btType", out var type)
            || type.ValueKind != JsonValueKind.String || (type.GetString() != current && type.GetString() != "com.belmonttech.serialize.fsvalue." + legacy)
            || !item.TryGetProperty("value", out var value))
        {
            throw Failure(OnshapePartModelingFrameReadFailure.UnsupportedValue);
        }
        return value;
    }

    private static bool IsRigidRightHanded(OnshapePartModelingFrame frame)
    {
        var x = frame.XAxis;
        var y = frame.YAxis;
        var normal = frame.Normal;
        var cross = new OnshapeFrameVector(x.Y * y.Z - x.Z * y.Y, x.Z * y.X - x.X * y.Z, x.X * y.Y - x.Y * y.X);
        return Unit(x) && Unit(y) && Unit(normal)
            && Math.Abs(Dot(x, y)) <= BasisTolerance
            && Math.Abs(Dot(x, normal)) <= BasisTolerance
            && Math.Abs(Dot(y, normal)) <= BasisTolerance
            && Near(cross, normal);
    }

    private static bool Unit(OnshapeFrameVector vector) => Math.Abs(Dot(vector, vector) - 1) <= BasisTolerance;

    private static double Dot(OnshapeFrameVector left, OnshapeFrameVector right) =>
        left.X * right.X + left.Y * right.Y + left.Z * right.Z;

    private static bool Near(OnshapeFrameVector left, OnshapeFrameVector right) =>
        Math.Abs(left.X - right.X) <= BasisTolerance
        && Math.Abs(left.Y - right.Y) <= BasisTolerance
        && Math.Abs(left.Z - right.Z) <= BasisTolerance;

    private static OnshapePartModelingFrameReadException Failure(OnshapePartModelingFrameReadFailure failure) => new(failure);
}
