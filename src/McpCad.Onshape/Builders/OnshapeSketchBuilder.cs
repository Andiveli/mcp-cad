using System.Globalization;
using System.Text.Json;

namespace McpCad.Onshape.Builders;

/// <summary>
/// Builds BTMSketch-style entity graphs for Part Studio sketch replace POST bodies.
/// Coordinates are in meters (convert from cm via <see cref="OnshapeUnitConversion"/> at call sites).
/// </summary>
public static class OnshapeSketchBuilder
{
    private const string LineType = "BTMSketchLine-184";
    private const string CircleType = "BTMSketchCircle-184";

    public static JsonDocument BuildRectangle(double x1M, double y1M, double x2M, double y2M)
    {
        var minX = Math.Min(x1M, x2M);
        var maxX = Math.Max(x1M, x2M);
        var minY = Math.Min(y1M, y2M);
        var maxY = Math.Max(y1M, y2M);

        return BuildSketch(
            new[]
            {
                Line("line-0", minX, minY, maxX, minY),
                Line("line-1", maxX, minY, maxX, maxY),
                Line("line-2", maxX, maxY, minX, maxY),
                Line("line-3", minX, maxY, minX, minY),
            },
            Array.Empty<object>());
    }

    public static JsonDocument BuildLine(double x1M, double y1M, double x2M, double y2M) =>
        BuildSketch(new[] { Line("line-0", x1M, y1M, x2M, y2M) }, Array.Empty<object>());

    public static JsonDocument BuildCircle(double cxM, double cyM, double radiusM) =>
        BuildSketch(
            new[]
            {
                new Dictionary<string, object?>
                {
                    ["btType"] = CircleType,
                    ["entityId"] = "circle-0",
                    ["center"] = Pt(cxM, cyM),
                    ["radius"] = radiusM,
                },
            },
            Array.Empty<object>());

    public static JsonDocument BuildSketch(
        IReadOnlyList<Dictionary<string, object?>> entities,
        IReadOnlyList<object> constraints)
    {
        var root = new Dictionary<string, object?>
        {
            ["entities"] = entities,
            ["constraints"] = constraints,
        };
        var json = JsonSerializer.Serialize(root);
        return JsonDocument.Parse(json);
    }

    private static Dictionary<string, object?> Line(string entityId, double x1, double y1, double x2, double y2) =>
        new()
        {
            ["btType"] = LineType,
            ["entityId"] = entityId,
            ["start"] = Pt(x1, y1),
            ["end"] = Pt(x2, y2),
        };

    private static Dictionary<string, object?> Pt(double x, double y) =>
        new()
        {
            ["x"] = double.Parse(x.ToString("G9", CultureInfo.InvariantCulture), CultureInfo.InvariantCulture),
            ["y"] = double.Parse(y.ToString("G9", CultureInfo.InvariantCulture), CultureInfo.InvariantCulture),
        };
}