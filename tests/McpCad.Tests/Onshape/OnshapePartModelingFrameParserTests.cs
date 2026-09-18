using System.Text.Json;
using McpCad.Onshape.Modeling;

namespace McpCad.Tests.Onshape;

public sealed class OnshapePartModelingFrameParserTests
{
    [Theory]
    [InlineData(false, 1, 0, 0, 0, 1, 0, 0, 0, 1)]
    [InlineData(false, 1, 0, 0, 0, 0, 1, 0, -1, 0)]
    [InlineData(false, 0, 1, 0, 0, 0, 1, 1, 0, 0)]
    [InlineData(true, 1, 0, 0, 0, 1, 0, 0, 0, 1)]
    [InlineData(true, 1, 0, 0, 0, 0, 1, 0, -1, 0)]
    [InlineData(true, 0, 1, 0, 0, 0, 1, 1, 0, 0)]
    public void Parse_AcceptsMeasuredFramesWithCurrentAndLegacyAliases(
        bool legacy, double xx, double xy, double xz, double yx, double yy, double yz, double nx, double ny, double nz)
    {
        var frame = Parse(Frame(legacy, (xx, xy, xz), (yx, yy, yz), (nx, ny, nz)));

        frame.Origin.Should().Be(new OnshapeFrameVector(0, 0, 0));
        frame.XAxis.Should().Be(new OnshapeFrameVector(xx, xy, xz));
        frame.YAxis.Should().Be(new OnshapeFrameVector(yx, yy, yz));
        frame.Normal.Should().Be(new OnshapeFrameVector(nx, ny, nz));
    }

    [Theory]
    [InlineData("notices", nameof(OnshapePartModelingFrameReadFailure.Notice))]
    [InlineData("microversionSkew", nameof(OnshapePartModelingFrameReadFailure.MicroversionSkew))]
    [InlineData("numeric-string", nameof(OnshapePartModelingFrameReadFailure.MalformedFrame))]
    [InlineData("bool", nameof(OnshapePartModelingFrameReadFailure.MalformedFrame))]
    [InlineData("overflow", nameof(OnshapePartModelingFrameReadFailure.MalformedFrame))]
    [InlineData("duplicate", nameof(OnshapePartModelingFrameReadFailure.MalformedFrame))]
    [InlineData("missing", nameof(OnshapePartModelingFrameReadFailure.MalformedFrame))]
    [InlineData("skew-basis", nameof(OnshapePartModelingFrameReadFailure.MalformedFrame))]
    public void Parse_RejectsInvalidFramesWithTypedFailures(string invalid, string expected)
    {
        Failure(Frame(false, (1, 0, 0), (0, 1, 0), (0, 0, 1), invalid)).ToString().Should().Be(expected);
    }

    [Theory]
    [MemberData(nameof(WrongArrayValues))]
    public void Parse_RejectsEveryNonArrayValueKindAsMalformed(object? value)
    {
        Failure(new { result = Value("Array", value) }).Should().Be(OnshapePartModelingFrameReadFailure.MalformedFrame);
    }

    [Theory]
    [MemberData(nameof(WrongArrayValues))]
    public void Parse_RejectsNonArrayRowsWithTypedFailure(object? value)
    {
        var rows = Enumerable.Repeat(Value("Array", value), 4).ToArray();
        Failure(new { result = Value("Array", rows) }).Should().Be(OnshapePartModelingFrameReadFailure.MalformedFrame);
    }

    public static IEnumerable<object?[]> WrongArrayValues()
    {
        yield return [null];
        yield return [1];
        yield return [new { unexpected = true }];
        yield return [true];
        yield return ["array"];
    }

    private static OnshapePartModelingFrame Parse(object response)
    {
        using var document = JsonDocument.Parse(JsonSerializer.Serialize(response));
        return OnshapePartModelingFrameParser.Parse(document.RootElement);
    }

    private static OnshapePartModelingFrameReadFailure Failure(object response)
    {
        var act = () => Parse(response);
        return act.Should().Throw<OnshapePartModelingFrameReadException>().Which.Failure;
    }

    private static object Frame(bool legacy, (double X, double Y, double Z) x, (double X, double Y, double Z) y,
        (double X, double Y, double Z) normal, string? invalid = null)
    {
        object Value(string type, object? value) => new Dictionary<string, object?>
        {
            ["btType"] = legacy ? "com.belmonttech.serialize.fsvalue.BTFSValue" + type : "BTFSValue" + type + (type == "Array" ? "-1499" : type == "Number" ? "-772" : "-1422"),
            ["value"] = value,
        };
        object Row(string name, (double X, double Y, double Z) vector) => Value("Array", new[]
        {
            Value("String", name), Value("Number", vector.X), Value("Number", vector.Y), Value("Number", vector.Z),
        });
        var rows = new List<object> { Row("origin", (0, 0, 0)), Row("x", x), Row("y", y), Row("normal", normal) };
        if (invalid == "duplicate") rows[3] = Row("x", normal);
        if (invalid == "missing") rows.RemoveAt(3);
        if (invalid == "skew-basis") rows[2] = Row("y", (1, 0, 0));
        if (invalid is "numeric-string" or "bool" or "overflow") rows[1] = Value("Array", new[]
        {
            Value("String", "x"), Value("Number", invalid == "numeric-string" ? "1" : invalid == "bool" ? true : Json("1e400")), Value("Number", 0), Value("Number", 0),
        });
        var response = new Dictionary<string, object?> { ["result"] = Value("Array", rows) };
        if (invalid == "notices") response["notices"] = new[] { new { message = "sanitized" } };
        if (invalid == "microversionSkew") response["microversionSkew"] = true;
        return response;
    }

    private static object Value(string type, object? value) => new Dictionary<string, object?> { ["btType"] = "BTFSValue" + type + "-1499", ["value"] = value };

    private static JsonElement Json(string value)
    {
        using var document = JsonDocument.Parse(value);
        return document.RootElement.Clone();
    }
}
