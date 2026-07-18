using System.Text.Json;
using McpCad.Onshape.Builders;

namespace McpCad.Tests.Onshape;

public class OnshapeSketchBuilderTests
{
    [Fact]
    public void BuildRectangle_ReturnsFourLineEntities()
    {
        using var doc = OnshapeSketchBuilder.BuildRectangle(0, 0, 0.1, 0.05);
        var feature = doc.RootElement.GetProperty("feature");
        feature.GetProperty("btType").GetString().Should().Be("BTMSketch-151");
        doc.RootElement.GetProperty("rejectMicroversionSkew").GetBoolean().Should().BeTrue();
        AssertSketchPlane(feature, "TopplaneOp", "JDC");

        var entities = feature.GetProperty("entities");
        entities.GetArrayLength().Should().Be(4);

        var expectedEndpoints = new[]
        {
            (Start: "point-0", End: "point-1"),
            (Start: "point-1", End: "point-2"),
            (Start: "point-2", End: "point-3"),
            (Start: "point-3", End: "point-0"),
        };

        for (var index = 0; index < entities.GetArrayLength(); index++)
        {
            var entity = entities[index];
            entity.GetProperty("btType").GetString().Should().Be("BTMSketchCurveSegment-155");
            entity.TryGetProperty("entityId", out _).Should().BeTrue();
            entity.GetProperty("startPointId").GetString().Should().Be(expectedEndpoints[index].Start);
            entity.GetProperty("endPointId").GetString().Should().Be(expectedEndpoints[index].End);
            entity.TryGetProperty("geometry", out _).Should().BeTrue();
            entity.GetProperty("geometry").GetProperty("btType").GetString().Should().Be("BTCurveGeometryLine-117");
            entity.TryGetProperty("startParam", out _).Should().BeTrue();
            entity.TryGetProperty("endParam", out _).Should().BeTrue();
            entity.GetProperty("isConstruction").GetBoolean().Should().BeFalse();
        }

        feature.GetProperty("constraints").GetArrayLength().Should().Be(0);
        entities.EnumerateArray().Should().NotContain(entity =>
            entity.GetProperty("btType").GetString() == "BTMPoint-210");
    }

    [Fact]
    public void BuildLine_ReturnsSingleLineEntity()
    {
        using var doc = OnshapeSketchBuilder.BuildLine(0, 0, 1, 0);
        var feature = doc.RootElement.GetProperty("feature");
        var entities = feature.GetProperty("entities");
        entities.GetArrayLength().Should().Be(1);
        entities[0].GetProperty("btType").GetString().Should().Be("BTMSketchCurveSegment-155");
    }

    [Fact]
    public void BuildCircle_ReturnsCircleEntity()
    {
        using var doc = OnshapeSketchBuilder.BuildCircle(0.05, 0.05, 0.02);
        var feature = doc.RootElement.GetProperty("feature");
        var entities = feature.GetProperty("entities");
        entities.GetArrayLength().Should().Be(1);
        entities[0].GetProperty("btType").GetString().Should().Be("BTMSketchCurve-4");
        entities[0].GetProperty("geometry").GetProperty("btType").GetString().Should().Be("BTCurveGeometryCircle-115");
        entities[0].GetProperty("isConstruction").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public void BuildRectangle_NegativeCoordinates_NormalizesCorners()
    {
        using var doc = OnshapeSketchBuilder.BuildRectangle(0.1, 0.1, -0.1, -0.1);
        doc.RootElement.GetProperty("feature").GetProperty("entities").GetArrayLength().Should().Be(4);
    }

    [Fact]
    public void BuildRectangle_ZeroSize_StillEmitsFourEntities()
    {
        using var doc = OnshapeSketchBuilder.BuildRectangle(1, 2, 1, 2);
        doc.RootElement.GetProperty("feature").GetProperty("entities").GetArrayLength().Should().Be(4);
    }

    [Fact]
    public void Builder_Envelope_ContainsRejectMicroversionSkew()
    {
        using var doc = OnshapeSketchBuilder.BuildRectangle(0, 0, 1, 1);
        doc.RootElement.TryGetProperty("rejectMicroversionSkew", out var rms).Should().BeTrue();
        rms.GetBoolean().Should().BeTrue();
    }

    [Theory]
    [InlineData("Top", "TopplaneOp", "JDC")]
    [InlineData("front", "FrontplaneOp", "JCC")]
    [InlineData("RIGHT", "RightplaneOp", "JBC")]
    public void BuildRectangle_MapsNamedPlaneToSketchPlaneQuery(
        string plane,
        string operationId,
        string deterministicId)
    {
        using var doc = OnshapeSketchBuilder.BuildRectangle(0, 0, 1, 1, plane);

        AssertSketchPlane(doc.RootElement.GetProperty("feature"), operationId, deterministicId);
        doc.RootElement.TryGetProperty("plane", out _).Should().BeFalse();
    }

    [Fact]
    public void BuildRectangle_InvalidPlane_Throws()
    {
        var act = () => OnshapeSketchBuilder.BuildRectangle(0, 0, 1, 1, "XY");

        act.Should().Throw<ArgumentException>().WithMessage("*Top, Front, or Right*");
    }

    private static void AssertSketchPlane(JsonElement feature, string operationId, string deterministicId)
    {
        var parameters = feature.GetProperty("parameters");
        parameters.GetArrayLength().Should().Be(1);
        var plane = parameters[0];
        plane.GetProperty("btType").GetString().Should().Be("BTMParameterQueryList-148");
        plane.GetProperty("parameterId").GetString().Should().Be("sketchPlane");
        plane.GetProperty("filter").GetProperty("btType").GetString().Should().Be("BTOrFilter-167");

        var query = plane.GetProperty("queries")[0];
        query.GetProperty("btType").GetString().Should().Be("BTMIndividualQuery-138");
        query.GetProperty("queryString").GetString().Should().Contain($"${operationId}");
        query.GetProperty("deterministicIds")[0].GetString().Should().Be(deterministicId);
    }
}
