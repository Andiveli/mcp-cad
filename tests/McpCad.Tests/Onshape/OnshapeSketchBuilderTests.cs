using System.Text.Json;
using McpCad.Onshape.Builders;

namespace McpCad.Tests.Onshape;

public class OnshapeSketchBuilderTests
{
    [Fact]
    public void BuildRectangle_ReturnsFourLineEntities()
    {
        using var doc = OnshapeSketchBuilder.BuildRectangle(0, 0, 0.1, 0.05);
        var root = doc.RootElement;
        root.GetProperty("entities").GetArrayLength().Should().Be(4);
        foreach (var entity in root.GetProperty("entities").EnumerateArray())
        {
            entity.GetProperty("btType").GetString().Should().Be("BTMSketchLine-184");
            entity.TryGetProperty("entityId", out _).Should().BeTrue();
        }
    }

    [Fact]
    public void BuildLine_ReturnsSingleLineEntity()
    {
        using var doc = OnshapeSketchBuilder.BuildLine(0, 0, 1, 0);
        doc.RootElement.GetProperty("entities").GetArrayLength().Should().Be(1);
        doc.RootElement.GetProperty("entities")[0].GetProperty("btType").GetString().Should().Be("BTMSketchLine-184");
    }

    [Fact]
    public void BuildCircle_ReturnsCircleEntity()
    {
        using var doc = OnshapeSketchBuilder.BuildCircle(0.05, 0.05, 0.02);
        doc.RootElement.GetProperty("entities").GetArrayLength().Should().Be(1);
        doc.RootElement.GetProperty("entities")[0].GetProperty("btType").GetString().Should().Be("BTMSketchCircle-184");
    }

    [Fact]
    public void BuildRectangle_NegativeCoordinates_NormalizesCorners()
    {
        using var doc = OnshapeSketchBuilder.BuildRectangle(0.1, 0.1, -0.1, -0.1);
        doc.RootElement.GetProperty("entities").GetArrayLength().Should().Be(4);
    }

    [Fact]
    public void BuildRectangle_ZeroSize_StillEmitsFourEntities()
    {
        using var doc = OnshapeSketchBuilder.BuildRectangle(1, 2, 1, 2);
        doc.RootElement.GetProperty("entities").GetArrayLength().Should().Be(4);
    }
}