using McpCad.Onshape.Builders;

namespace McpCad.Tests.Onshape;

public class OnshapeClosedProfileTests
{
    [Theory]
    [MemberData(nameof(ValidProfiles))]
    public void BuildClosedProfile_EmitsDeterministicDiagonalCapableClosedSketch(
        SketchPoint[] points,
        string plane,
        int expectedVertices,
        double expectedFirstDirectionX,
        double expectedFirstDirectionY)
    {
        using var doc = OnshapeSketchBuilder.BuildClosedProfile(points, plane);
        var feature = doc.RootElement.GetProperty("feature");
        var entities = feature.GetProperty("entities");
        var constraints = feature.GetProperty("constraints");

        entities.GetArrayLength().Should().Be(expectedVertices);
        constraints.GetArrayLength().Should().Be(expectedVertices);
        entities[0].GetProperty("geometry").GetProperty("dirX").GetDouble().Should().BeApproximately(expectedFirstDirectionX, 1e-9);
        entities[0].GetProperty("geometry").GetProperty("dirY").GetDouble().Should().BeApproximately(expectedFirstDirectionY, 1e-9);

        for (var index = 0; index < expectedVertices; index++)
        {
            var entity = entities[index];
            entity.GetProperty("btType").GetString().Should().Be("BTMSketchCurveSegment-155");
            entity.GetProperty("entityId").GetString().Should().Be($"closed-profile-line-{index}");
            entity.GetProperty("startPointId").GetString().Should().Be($"closed-profile-point-{index}");
            entity.GetProperty("endPointId").GetString().Should().Be($"closed-profile-point-{(index + 1) % expectedVertices}");
            entity.GetProperty("geometry").GetProperty("btType").GetString().Should().Be("BTCurveGeometryLine-117");
            entity.GetProperty("isConstruction").GetBoolean().Should().BeFalse();

            var constraint = constraints[index];
            constraint.GetProperty("entityId").GetString().Should().Be($"closed-profile-constraint-coincident-{index}");
            constraint.GetProperty("constraintType").GetString().Should().Be("COINCIDENT");
            constraint.GetProperty("parameters")[0].GetProperty("value").GetString().Should().Be($"closed-profile-line-{index}.end");
            constraint.GetProperty("parameters")[1].GetProperty("value").GetString().Should().Be($"closed-profile-line-{(index + 1) % expectedVertices}.start");
        }

        constraints.EnumerateArray().Select(constraint => constraint.GetProperty("constraintType").GetString()).Should()
            .NotContain("HORIZONTAL").And.NotContain("VERTICAL");
        entities.EnumerateArray().Should().NotContain(entity => entity.GetProperty("btType").GetString() == "BTMSketchPoint-158");
        AssertSketchPlane(feature, plane);
    }

    public static IEnumerable<object[]> ValidProfiles()
    {
        yield return [
            new[] { new SketchPoint(0, 0), new SketchPoint(4, 0), new SketchPoint(2, 3) },
            "Top", 3, 1d, 0d,
        ];
        yield return [
            new[] { new SketchPoint(0, 0), new SketchPoint(4, 0), new SketchPoint(4, 4), new SketchPoint(2, 2), new SketchPoint(0, 4) },
            "Front", 5, 1d, 0d,
        ];
        yield return [
            new[] { new SketchPoint(0, 0), new SketchPoint(2, 3), new SketchPoint(4, 0) },
            "Right", 3, 0.554700196d, 0.832050294d,
        ];
        yield return [
            new[] { new SketchPoint(0, 0), new SketchPoint(4, 0), new SketchPoint(2, 3), new SketchPoint(0, 0) },
            "Top", 3, 1d, 0d,
        ];
        yield return [
            new[] { new SketchPoint(0, 0), new SketchPoint(2, 0), new SketchPoint(4, 0), new SketchPoint(4, 4), new SketchPoint(0, 4) },
            "Top", 5, 1d, 0d,
        ];
    }

    [Theory]
    [MemberData(nameof(InvalidProfiles))]
    public void BuildClosedProfile_RejectsInvalidProfiles(SketchPoint[] points)
    {
        var act = () => OnshapeSketchBuilder.BuildClosedProfile(points);

        act.Should().Throw<ArgumentException>();
    }

    public static IEnumerable<object[]> InvalidProfiles()
    {
        yield return [new[] { new SketchPoint(double.NaN, 0), new SketchPoint(1, 0), new SketchPoint(0, 1) }];
        yield return [new[] { new SketchPoint(0, 0), new SketchPoint(1, 0) }];
        yield return [Enumerable.Range(0, 257).Select(index => new SketchPoint(index, index * index + 1)).ToArray()];
        yield return [new[] { new SketchPoint(0, 0), new SketchPoint(2, 0), new SketchPoint(2, 2), new SketchPoint(0, 2), new SketchPoint(2, 0) }];
        yield return [new[] { new SketchPoint(0, 0), new SketchPoint(2, 0), new SketchPoint(2, 0), new SketchPoint(0, 2) }];
        yield return [new[] { new SketchPoint(0, 0), new SketchPoint(1, 0), new SketchPoint(2, 0) }];
        yield return [new[] { new SketchPoint(0, 0), new SketchPoint(3, 3), new SketchPoint(0, 3), new SketchPoint(3, 0) }];
        yield return [new[] { new SketchPoint(0, 0), new SketchPoint(3, 0), new SketchPoint(3, 3), new SketchPoint(1.5, 0), new SketchPoint(0, 3) }];
        yield return [new[] { new SketchPoint(0, 0), new SketchPoint(3, 0), new SketchPoint(1, 0), new SketchPoint(1, 2), new SketchPoint(0, 2) }];
        yield return [new[] { new SketchPoint(1, 0), new SketchPoint(1, 1), new SketchPoint(1.000000001, 1), new SketchPoint(1.000000001, 0) }];
    }

    private static void AssertSketchPlane(System.Text.Json.JsonElement feature, string plane)
    {
        var operationId = plane switch
        {
            "Top" => "TopplaneOp",
            "Front" => "FrontplaneOp",
            "Right" => "RightplaneOp",
            _ => throw new ArgumentOutOfRangeException(nameof(plane)),
        };
        feature.GetProperty("parameters")[0].GetProperty("queries")[0].GetProperty("queryString").GetString().Should().Contain($"${operationId}");
    }
}
