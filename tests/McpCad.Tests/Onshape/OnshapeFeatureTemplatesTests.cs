using System.Text.Json;
using McpCad.Onshape.Builders;

namespace McpCad.Tests.Onshape;

public class OnshapeFeatureTemplatesTests
{
    [Fact]
    public void ExtrudeFromSketch_ReturnsFeatureEnvelope()
    {
        using var doc = OnshapeFeatureTemplates.ExtrudeFromSketch("sketch-abc", 0.05, "positive", "new");
        var root = doc.RootElement;

        root.TryGetProperty("feature", out var feature).Should().BeTrue();
        feature.GetProperty("featureType").GetString().Should().Be("extrude");
        feature.GetProperty("parameters").ValueKind.Should().Be(JsonValueKind.Array);
        root.GetProperty("rejectMicroversionSkew").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public void ExtrudeFromSketch_NegativeDepth_ReversesDirection()
    {
        using var doc = OnshapeFeatureTemplates.ExtrudeFromSketch("s1", -0.02, "positive", "new");
        var json = doc.RootElement.GetRawText();
        json.Should().Contain("negative");
    }

    [Fact]
    public void ExtrudeFromSketch_JoinedBoolean_MapsOperationType()
    {
        using var doc = OnshapeFeatureTemplates.ExtrudeFromSketch("s1", 0.01, "positive", "join");
        doc.RootElement.GetRawText().Should().Contain("JOIN");
    }
}