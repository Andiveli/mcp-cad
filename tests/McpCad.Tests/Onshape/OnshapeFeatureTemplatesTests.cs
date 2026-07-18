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
    public void ExtrudeFromSketch_UsesMetersAndSketchRegionQuery()
    {
        using var doc = OnshapeFeatureTemplates.ExtrudeFromSketch("sketch-abc", 0.3, "positive", "new");
        var parameters = doc.RootElement.GetProperty("feature").GetProperty("parameters");

        var depth = parameters.EnumerateArray().Single(parameter =>
            parameter.GetProperty("parameterId").GetString() == "depth");
        depth.GetProperty("btType").GetString().Should().Be("BTMParameterQuantity-147");
        depth.GetProperty("expression").GetString().Should().Be("0.3 m");
        depth.TryGetProperty("value", out _).Should().BeFalse();

        var operation = parameters.EnumerateArray().Single(parameter =>
            parameter.GetProperty("parameterId").GetString() == "operationType");
        operation.GetProperty("btType").GetString().Should().Be("BTMParameterEnum-145");
        operation.GetProperty("enumName").GetString().Should().Be("NewBodyOperationType");
        operation.GetProperty("value").GetString().Should().Be("NEW");

        var entities = parameters.EnumerateArray().Single(parameter =>
            parameter.GetProperty("parameterId").GetString() == "entities");
        entities.GetProperty("btType").GetString().Should().Be("BTMParameterQueryList-148");
        var query = entities.GetProperty("queries")[0];
        query.GetProperty("btType").GetString().Should().Be("BTMIndividualSketchRegionQuery-140");
        query.GetProperty("featureId").GetString().Should().Be("sketch-abc");
        query.GetProperty("filterInnerLoops").GetBoolean().Should().BeTrue();
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
        doc.RootElement.GetRawText().Should().Contain("ADD");
    }

    [Fact]
    public void RevolveFromSketch_ReturnsFeatureEnvelope()
    {
        using var doc = OnshapeFeatureTemplates.RevolveFromSketch("sketch-abc", "axis-sketch-1", 180, "positive", "new");

        doc.RootElement.GetProperty("feature").GetProperty("featureType").GetString().Should().Be("revolve");
        var json = doc.RootElement.GetRawText();
        var axisQuery = doc.RootElement.GetProperty("feature").GetProperty("parameters")
            .EnumerateArray().Single(parameter => parameter.GetProperty("parameterId").GetString() == "axis")
            .GetProperty("queries")[0];
        json.Should().Contain("\"parameterId\":\"axis\"")
            .And.Contain("\"expression\":\"180 deg\"")
            .And.NotContain("\"deterministicIds\":[\"axis-sketch-1\"]");
        axisQuery.GetProperty("queryString").GetString().Should()
            .Be("query = qCreatedBy(id + \"axis-sketch-1\" + \"wireOp\", EntityType.EDGE);");
    }

    [Theory]
    [InlineData(0, "positive")]
    [InlineData(360, "positive")]
    [InlineData(90, "negative")]
    public void RevolveFromSketch_P1_PreservesAngleAndDirection(double angleDeg, string direction)
    {
        using var doc = OnshapeFeatureTemplates.RevolveFromSketch("sketch-1", "axis-1", angleDeg, direction, "new");

        doc.RootElement.GetRawText().Should().Contain($"\"expression\":\"{angleDeg} deg\"");
        doc.RootElement.GetRawText().Should().Contain(direction == "negative" ? "\"value\":true" : "\"value\":false");
    }

    [Fact]
    public void Fillet_ReturnsFeatureEnvelope()
    {
        using var doc = OnshapeFeatureTemplates.Fillet(["edge-1"], 0.005);

        doc.RootElement.GetProperty("feature").GetProperty("featureType").GetString().Should().Be("fillet");
        doc.RootElement.GetRawText().Should().Contain("\"parameterId\":\"radius\"")
            .And.Contain("\"expression\":\"0.005 m\"")
            .And.Contain("\"deterministicIds\":[\"edge-1\"]");
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

        doc.RootElement.GetRawText().Should().Contain("\"parameterId\":\"width\"")
            .And.Contain("\"value\":\"EQUAL_OFFSETS\"")
            .And.Contain("\"deterministicIds\":[\"edge-1\",\"edge-2\"]");
    }

}
