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
    [InlineData("work_axis_btmsketch.golden.json")]
    [InlineData("closed_profile_btmsketch.golden.json")]
    public void SketchBuilders_MatchGoldenShape(string fixtureName)
    {
        using var actual = fixtureName switch
        {
            "rectangle_btmsketch.golden.json" => OnshapeSketchBuilder.BuildRectangle(0, 0, 0.1, 0.05),
            "line_btmsketch.golden.json" => OnshapeSketchBuilder.BuildLine(0, 0, 1, 0),
            "circle_btmsketch.golden.json" => OnshapeSketchBuilder.BuildCircle(0.05, 0.05, 0.02),
            "work_axis_btmsketch.golden.json" => OnshapeSketchBuilder.BuildWorkAxis("Front", "x"),
            "closed_profile_btmsketch.golden.json" => OnshapeSketchBuilder.BuildClosedProfile([new(0, 0), new(4, 0), new(2, 3)]),
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

    [Fact]
    public void CircularPatternTemplate_MatchesLiveVerifiedGoldenShape()
    {
        using var actual = OnshapeFeatureTemplates.CircularPatternFromFeature("extrude-1", "axis-sketch-1", 4, 180);

        MatchesGoldenShape(LoadFixture("Features", "circular_pattern_feature_envelope.golden.json"), JsonNode.Parse(actual.RootElement.GetRawText()))
            .Should().BeTrue(actual.RootElement.GetRawText());
    }

    [Fact]
    public void RectangularPatternTemplate_MatchesLiveVerifiedGoldenShape()
    {
        using var actual = OnshapeFeatureTemplates.RectangularPatternFromFeature(
            "extrude-1", "direction-one-1", 0.03, 2, "direction-two-1", 0.03, 2);

        MatchesGoldenShape(LoadFixture("Features", "rectangular_pattern_feature_envelope.golden.json"), JsonNode.Parse(actual.RootElement.GetRawText()))
            .Should().BeTrue(actual.RootElement.GetRawText());
    }

    [Fact]
    public void MirrorFeatureTemplate_MatchesLiveVerifiedGoldenShape()
    {
        using var actual = OnshapeFeatureTemplates.MirrorFeature("extrude-1", "Front");

        MatchesGoldenShape(LoadFixture("Features", "mirror_feature_envelope.golden.json"), JsonNode.Parse(actual.RootElement.GetRawText()))
            .Should().BeTrue(actual.RootElement.GetRawText());
    }

    [Fact]
    public void WorkPointOnEdgeTemplate_MatchesLiveProvenGoldenShape()
    {
        using var actual = OnshapeFeatureTemplates.WorkPointOnEdge("JPV", 0.5);

        MatchesGoldenShape(LoadFixture("Features", "work_point_edge_feature_envelope.golden.json"), JsonNode.Parse(actual.RootElement.GetRawText()))
            .Should().BeTrue(actual.RootElement.GetRawText());
    }

    [Fact]
    public void OffsetPlaneTemplate_MatchesLiveProvenGoldenShape()
    {
        using var actual = OnshapeFeatureTemplates.OffsetPlane("Top", 0.01);

        MatchesGoldenShape(LoadFixture("Features", "offset_plane_feature_envelope.golden.json"), JsonNode.Parse(actual.RootElement.GetRawText()))
            .Should().BeTrue(actual.RootElement.GetRawText());
    }

    [Fact]
    public void ShellTemplate_MatchesLiveProvenGoldenShape()
    {
        using var actual = OnshapeFeatureTemplates.Shell(["JHK"], 0.001);

        MatchesGoldenShape(LoadFixture("Features", "shell_feature_envelope.golden.json"), JsonNode.Parse(actual.RootElement.GetRawText()))
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
