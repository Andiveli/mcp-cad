using System.Text.Json;
using System.Text.Json.Nodes;
using McpCad.Onshape.Builders;

namespace McpCad.Tests.Onshape;

public class GoldenFixtureConformanceTests
{
    [Theory]
    [InlineData("rectangle_btmsketch.golden.json")]
    [InlineData("line_btmsketch.golden.json")]
    [InlineData("circle_btmsketch.golden.json")]
    public void SketchBuilders_MatchGoldenShape(string fixtureName)
    {
        using var actual = fixtureName switch
        {
            "rectangle_btmsketch.golden.json" => OnshapeSketchBuilder.BuildRectangle(0, 0, 0.1, 0.05),
            "line_btmsketch.golden.json" => OnshapeSketchBuilder.BuildLine(0, 0, 1, 0),
            "circle_btmsketch.golden.json" => OnshapeSketchBuilder.BuildCircle(0.05, 0.05, 0.02),
            _ => throw new ArgumentOutOfRangeException(nameof(fixtureName)),
        };

        MatchesGoldenShape(LoadFixture("Sketch", fixtureName), JsonNode.Parse(actual.RootElement.GetRawText()))
            .Should().BeTrue(actual.RootElement.GetRawText());
    }

    [Fact]
    public void ExtrudeTemplate_MatchesGoldenShape()
    {
        using var actual = OnshapeFeatureTemplates.ExtrudeFromSketch("sketch-1", 0.1, "positive", "new");

        MatchesGoldenShape(LoadFixture("Features", "extrude_envelope.golden.json"), JsonNode.Parse(actual.RootElement.GetRawText()))
            .Should().BeTrue(actual.RootElement.GetRawText());
    }

    private static JsonNode? LoadFixture(string folder, string fixtureName)
    {
        var fixturePath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..",
            "Onshape", "Fixtures", folder, fixtureName));

        return JsonNode.Parse(File.ReadAllText(fixturePath));
    }

    private static bool MatchesGoldenShape(JsonNode? expected, JsonNode? actual) =>
        (expected, actual) switch
        {
            (JsonObject expectedObject, JsonObject actualObject) => expectedObject.All(pair =>
                actualObject.TryGetPropertyValue(pair.Key, out var actualValue)
                && MatchesGoldenShape(pair.Value, actualValue)),
            (JsonArray expectedArray, JsonArray actualArray) =>
                expectedArray.Count == actualArray.Count
                && expectedArray.Zip(actualArray).All(pair => MatchesGoldenShape(pair.First, pair.Second)),
            _ => JsonNode.DeepEquals(expected, actual),
        };
}
