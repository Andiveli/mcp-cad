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

    [Fact]
    public void RevolveFromSketch_ReturnsFeatureEnvelope()
    {
        using var doc = OnshapeFeatureTemplates.RevolveFromSketch("sketch-abc", 180, "positive", "new");

        doc.RootElement.GetProperty("feature").GetProperty("featureType").GetString().Should().Be("revolve");
        doc.RootElement.GetRawText().Should().Contain("\"parameterId\":\"angle\"");
    }

    [Theory]
    [InlineData(0, "positive")]
    [InlineData(360, "positive")]
    [InlineData(90, "negative")]
    public void RevolveFromSketch_P1_PreservesAngleAndDirection(double angleDeg, string direction)
    {
        using var doc = OnshapeFeatureTemplates.RevolveFromSketch("sketch-1", angleDeg, direction, "new");

        doc.RootElement.GetRawText().Should().Contain($"\"value\":{angleDeg}");
        doc.RootElement.GetRawText().Should().Contain(direction);
    }

    [Fact]
    public void Fillet_ReturnsFeatureEnvelope()
    {
        using var doc = OnshapeFeatureTemplates.Fillet(["edge-1"], 0.005);

        doc.RootElement.GetProperty("feature").GetProperty("featureType").GetString().Should().Be("fillet");
        doc.RootElement.GetRawText().Should().Contain("\"parameterId\":\"radius\"");
    }

    [Fact]
    public void Fillet_P1_MultipleEdges_PreservesEverySelection()
    {
        using var doc = OnshapeFeatureTemplates.Fillet(["edge-1", "edge-2"], 0.005);

        doc.RootElement.GetRawText().Should().Contain("edge-1").And.Contain("edge-2");
    }

    [Fact]
    public void Fillet_P1_ZeroRadius_ThrowsArgumentOutOfRangeException()
    {
        var act = () => OnshapeFeatureTemplates.Fillet(["edge-1"], 0);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Chamfer_ReturnsFeatureEnvelope()
    {
        using var doc = OnshapeFeatureTemplates.Chamfer(["edge-1"], 0.003);

        doc.RootElement.GetProperty("feature").GetProperty("featureType").GetString().Should().Be("chamfer");
    }

    [Fact]
    public void Chamfer_P1_EqualDistance_ContainsDistanceParameter()
    {
        using var doc = OnshapeFeatureTemplates.Chamfer(["edge-1", "edge-2"], 0.003);

        doc.RootElement.GetRawText().Should().Contain("\"parameterId\":\"distance\"");
    }

    [Fact]
    public void Hole_ReturnsFeatureEnvelope()
    {
        using var doc = OnshapeFeatureTemplates.Hole("vertex-1", 0.006, 0.02, "simple");

        doc.RootElement.GetProperty("feature").GetProperty("featureType").GetString().Should().Be("hole");
        doc.RootElement.GetRawText().Should().Contain("\"parameterId\":\"diameter\"")
            .And.Contain("\"parameterId\":\"depth\"");
    }

    [Theory]
    [InlineData("blind")]
    [InlineData("through")]
    [InlineData("threaded")]
    public void Hole_P1_PreservesHoleType(string holeType)
    {
        using var doc = OnshapeFeatureTemplates.Hole("vertex-1", 0.006, 0.02, holeType);

        doc.RootElement.GetRawText().Should().Contain(holeType);
    }
}
