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
    public void SweepFromSketch_UsesSolidNewBodyProfileAndPathQueries()
    {
        using var doc = OnshapeFeatureTemplates.SweepFromSketch("profile-sketch", "path-sketch");
        var feature = doc.RootElement.GetProperty("feature");
        var parameters = feature.GetProperty("parameters");

        feature.GetProperty("featureType").GetString().Should().Be("sweep");
        parameters.EnumerateArray().Single(p => p.GetProperty("parameterId").GetString() == "bodyType")
            .GetProperty("value").GetString().Should().Be("SOLID");
        parameters.EnumerateArray().Single(p => p.GetProperty("parameterId").GetString() == "operationType")
            .GetProperty("value").GetString().Should().Be("NEW");
        parameters.EnumerateArray().Single(p => p.GetProperty("parameterId").GetString() == "profileControl")
            .GetProperty("value").GetString().Should().Be("NONE");

        var profile = parameters.EnumerateArray().Single(p => p.GetProperty("parameterId").GetString() == "profiles")
            .GetProperty("queries")[0];
        profile.GetProperty("btType").GetString().Should().Be("BTMIndividualSketchRegionQuery-140");
        profile.GetProperty("featureId").GetString().Should().Be("profile-sketch");
        profile.GetProperty("filterInnerLoops").GetBoolean().Should().BeTrue();

        var path = parameters.EnumerateArray().Single(p => p.GetProperty("parameterId").GetString() == "path")
            .GetProperty("queries")[0];
        path.GetProperty("btType").GetString().Should().Be("BTMIndividualQuery-138");
        path.GetProperty("queryString").GetString().Should()
            .Be("query = qCreatedBy(id + \"path-sketch\" + \"wireOp\", EntityType.EDGE);");
    }

    [Theory]
    [InlineData("join", "ADD")]
    [InlineData("cut", "REMOVE")]
    public void SweepFromSketch_MapsExistingBooleanOperations(string booleanOp, string expectedOperation)
    {
        using var doc = OnshapeFeatureTemplates.SweepFromSketch("profile-sketch", "path-sketch", booleanOp);

        doc.RootElement.GetProperty("feature").GetProperty("parameters").EnumerateArray()
            .Single(p => p.GetProperty("parameterId").GetString() == "operationType")
            .GetProperty("value").GetString().Should().Be(expectedOperation);
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
    public void CircularPatternFromFeature_UsesLiveVerifiedNativeContractInOrder()
    {
        using var doc = OnshapeFeatureTemplates.CircularPatternFromFeature(
            "extrude-1", "axis-sketch-1", 6, 270, oppositeDirection: true, equalSpace: false);

        var feature = doc.RootElement.GetProperty("feature");
        feature.GetProperty("featureType").GetString().Should().Be("circularPattern");
        var parameters = feature.GetProperty("parameters").EnumerateArray().ToArray();
        parameters.Select(parameter => parameter.GetProperty("parameterId").GetString()).Should().Equal(
            "patternType", "operationType", "entities", "faces", "instanceFunction", "axis", "angle",
            "instanceCount", "oppositeDirection", "equalSpace", "isCentered", "defaultScope", "booleanScope",
            "fullFeaturePattern", "skipInstances", "skippedInstances");
        parameters[0].GetProperty("value").GetString().Should().Be("FEATURE");
        parameters[4].GetProperty("btType").GetString().Should().Be("BTMParameterFeatureList-1749");
        parameters[4].GetProperty("featureIds")[0].GetString().Should().Be("extrude-1");
        parameters[5].GetProperty("queries")[0].GetProperty("queryString").GetString().Should()
            .Be("query = qCreatedBy(id + \"axis-sketch-1\" + \"wireOp\", EntityType.EDGE);");
        parameters[6].GetProperty("expression").GetString().Should().Be("270 deg");
        parameters[7].GetProperty("isInteger").GetBoolean().Should().BeTrue();
        parameters[7].GetProperty("expression").GetString().Should().Be("6");
        parameters[8].GetProperty("value").GetBoolean().Should().BeTrue();
        parameters[9].GetProperty("value").GetBoolean().Should().BeFalse();
        parameters[10].GetProperty("value").GetBoolean().Should().BeFalse();
        parameters[15].GetProperty("items").GetArrayLength().Should().Be(0);
    }

    [Fact]
    public void RectangularPatternFromFeature_UsesLiveVerifiedTwoDirectionNativeContractInOrder()
        {
            using var doc = OnshapeFeatureTemplates.RectangularPatternFromFeature(
                "extrude-1", "direction-one-1", 0.03, 2, "direction-two-1", 0.03, 2,
                oppositeDirection: true, isCentered: true, oppositeDirectionTwo: true, isCenteredTwo: true);

            var feature = doc.RootElement.GetProperty("feature");
            feature.GetProperty("featureType").GetString().Should().Be("linearPattern");
            var parameters = feature.GetProperty("parameters").EnumerateArray().ToArray();
            parameters.Select(parameter => parameter.GetProperty("parameterId").GetString()).Should().Equal(
                "patternType", "operationType", "entities", "faces", "instanceFunction", "directionOne", "distance",
                "instanceCount", "oppositeDirection", "isCentered", "hasSecondDir", "directionTwo", "distanceTwo",
                "instanceCountTwo", "oppositeDirectionTwo", "isCenteredTwo", "defaultScope", "booleanScope",
                "fullFeaturePattern", "skipInstances", "skippedInstances");
            parameters[0].GetProperty("value").GetString().Should().Be("FEATURE");
            parameters[1].GetProperty("value").GetString().Should().Be("NEW");
            parameters[4].GetProperty("btType").GetString().Should().Be("BTMParameterFeatureList-1749");
            parameters[5].GetProperty("queries")[0].GetProperty("queryString").GetString().Should()
                .Be("query = qCreatedBy(id + \"direction-one-1\" + \"wireOp\", EntityType.EDGE);");
            parameters[6].GetProperty("expression").GetString().Should().Be("0.03 m");
            parameters[7].GetProperty("expression").GetString().Should().Be("2");
            parameters[10].GetProperty("value").GetBoolean().Should().BeTrue();
            parameters[11].GetProperty("queries")[0].GetProperty("queryString").GetString().Should()
                .Be("query = qCreatedBy(id + \"direction-two-1\" + \"wireOp\", EntityType.EDGE);");
            parameters[12].GetProperty("expression").GetString().Should().Be("0.03 m");
            parameters[13].GetProperty("isInteger").GetBoolean().Should().BeTrue();
            parameters[14].GetProperty("value").GetBoolean().Should().BeTrue();
            parameters[15].GetProperty("value").GetBoolean().Should().BeTrue();
            parameters[20].GetProperty("items").GetArrayLength().Should().Be(0);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(double.NaN)]
        public void RectangularPatternFromFeature_RejectsInvalidFirstDirectionDistance(double distanceMeters)
        {
            var act = () => OnshapeFeatureTemplates.RectangularPatternFromFeature(
                "extrude-1", "direction-one-1", distanceMeters, 2);

            act.Should().Throw<ArgumentOutOfRangeException>();
        }

        [Fact]
        public void RectangularPatternFromFeature_OneDirection_UsesFeatureSpecSecondDirectionDefaults()
        {
            using var doc = OnshapeFeatureTemplates.RectangularPatternFromFeature(
                "extrude-1", "direction-one-1", 0.03, 2);

            var parameters = doc.RootElement.GetProperty("feature").GetProperty("parameters");
            parameters[10].GetProperty("value").GetBoolean().Should().BeFalse();
            parameters[11].GetProperty("btType").GetString().Should().Be("BTMParameterQueryList-148");
            parameters[11].GetProperty("queries").GetArrayLength().Should().Be(0);
            parameters[12].GetProperty("expression").GetString().Should().BeEmpty();
            parameters[12].GetProperty("value").GetDouble().Should().Be(0.025);
            parameters[12].GetProperty("units").GetString().Should().Be("meter");
            parameters[13].GetProperty("expression").GetString().Should().BeEmpty();
            parameters[13].GetProperty("value").GetDouble().Should().Be(1.0);
            parameters[13].GetProperty("isInteger").GetBoolean().Should().BeTrue();
            parameters[14].GetProperty("value").GetBoolean().Should().BeFalse();
            parameters[15].GetProperty("value").GetBoolean().Should().BeFalse();
        }

        [Theory]
        [InlineData(true, false)]
        [InlineData(false, true)]
        public void RectangularPatternFromFeature_RejectsPartialSecondDirectionOrSecondDirectionFlags(
            bool oppositeDirectionTwo,
            bool isCenteredTwo)
        {
            var act = () => OnshapeFeatureTemplates.RectangularPatternFromFeature(
                "extrude-1", "direction-one-1", 0.03, 2,
                oppositeDirectionTwo: oppositeDirectionTwo,
                isCenteredTwo: isCenteredTwo);

            act.Should().Throw<ArgumentException>();
        }

        [Fact]
        public void RectangularPatternFromFeature_RejectsPartialSecondDirection()
        {
            var act = () => OnshapeFeatureTemplates.RectangularPatternFromFeature(
                "extrude-1", "direction-one-1", 0.03, 2, directionTwoSketchFeatureId: "direction-two-1");

            act.Should().Throw<ArgumentException>();
        }

        [Fact]
        public void MirrorFeature_UsesLiveVerifiedNativeContractAndCanonicalFrontPlane()
    {
        using var doc = OnshapeFeatureTemplates.MirrorFeature("extrude-1", "Front");

        var feature = doc.RootElement.GetProperty("feature");
        feature.GetProperty("featureType").GetString().Should().Be("mirror");
        var parameters = feature.GetProperty("parameters").EnumerateArray().ToArray();
        parameters.Select(parameter => parameter.GetProperty("parameterId").GetString()).Should().Equal(
            "patternType", "operationType", "entities", "faces", "instanceFunction", "mirrorPlane",
            "defaultScope", "booleanScope", "fullFeaturePattern");
        parameters[0].GetProperty("enumName").GetString().Should().Be("MirrorType");
        parameters[0].GetProperty("value").GetString().Should().Be("FEATURE");
        parameters[1].GetProperty("enumName").GetString().Should().Be("NewBodyOperationType");
        parameters[1].GetProperty("value").GetString().Should().Be("NEW");
        parameters[2].GetProperty("queries").GetArrayLength().Should().Be(0);
        parameters[3].GetProperty("queries").GetArrayLength().Should().Be(0);
        parameters[4].GetProperty("btType").GetString().Should().Be("BTMParameterFeatureList-1749");
        parameters[4].GetProperty("featureIds")[0].GetString().Should().Be("extrude-1");
        parameters[5].GetProperty("btType").GetString().Should().Be("BTMParameterQueryList-148");
        var mirrorPlane = parameters[5].GetProperty("queries")[0];
        mirrorPlane.GetProperty("btType").GetString().Should().Be("BTMIndividualQuery-138");
        mirrorPlane.GetProperty("queryString").GetString().Should().Be(
            "query=qCompressed(1.0,\"%B5$QueryM4Sa$entityTypeBa$EntityTypeS4$FACESb$historyTypeS8$CREATIONSb$operationIdB2$IdA1S5.7$FrontplaneOpS9$queryTypeS5$DUMMY\",id);");
        mirrorPlane.GetProperty("deterministicIds")[0].GetString().Should().Be("JCC");
        parameters[6].GetProperty("value").GetBoolean().Should().BeFalse();
        parameters[7].GetProperty("queries").GetArrayLength().Should().Be(0);
        parameters[8].GetProperty("value").GetBoolean().Should().BeFalse();
    }

    [Theory]
    [InlineData("Top", "JDC")]
    [InlineData("Right", "JEC")]
    public void MirrorFeature_UsesExistingCanonicalPlaneMappings(string mirrorPlane, string deterministicId)
    {
        using var doc = OnshapeFeatureTemplates.MirrorFeature("extrude-1", mirrorPlane);

        doc.RootElement.GetProperty("feature").GetProperty("parameters")[5]
            .GetProperty("queries")[0].GetProperty("deterministicIds")[0].GetString()
            .Should().Be(deterministicId);
    }

    [Theory]
    [InlineData("")]
    [InlineData("diagonal")]
    public void MirrorFeature_RejectsBlankSourceOrUnknownPlane(string mirrorPlane)
    {
        var sourceAct = () => OnshapeFeatureTemplates.MirrorFeature("", "Front");
        var planeAct = () => OnshapeFeatureTemplates.MirrorFeature("extrude-1", mirrorPlane);

        sourceAct.Should().Throw<ArgumentException>();
        planeAct.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData("Top", "JDC")]
    [InlineData("Front", "JCC")]
    [InlineData("Right", "JEC")]
    public void OffsetPlane_UsesLiveProvenFlatContractWithCanonicalPlaneQuery(string canonicalPlane, string deterministicId)
    {
        using var doc = OnshapeFeatureTemplates.OffsetPlane(canonicalPlane, 0.01);

        var feature = doc.RootElement.GetProperty("feature");
        feature.GetProperty("featureType").GetString().Should().Be("cPlane");
        var parameters = feature.GetProperty("parameters").EnumerateArray().ToArray();
        parameters.Select(parameter => parameter.GetProperty("parameterId").GetString()).Should().Equal(
            "entities", "cplaneType", "offset", "angle", "oppositeDirection", "flipAlignment", "flipNormal", "width", "height");
        parameters[0].GetProperty("queries")[0].GetProperty("deterministicIds")[0].GetString().Should().Be(deterministicId);
        parameters[1].GetProperty("enumName").GetString().Should().Be("CPlaneType");
        parameters[1].GetProperty("value").GetString().Should().Be("OFFSET");
        parameters[2].GetProperty("expression").GetString().Should().Be("0.01 m");
        parameters[3].GetProperty("expression").GetString().Should().Be("0 deg");
        parameters[4].GetProperty("value").GetBoolean().Should().BeFalse();
        parameters[5].GetProperty("value").GetBoolean().Should().BeFalse();
        parameters[6].GetProperty("value").GetBoolean().Should().BeFalse();
        parameters[7].GetProperty("expression").GetString().Should().Be("0.15 m");
        parameters[8].GetProperty("expression").GetString().Should().Be("0.15 m");
    }

    [Theory]
    [InlineData(-0.01)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void OffsetPlane_RejectsNegativeOrNonFiniteOffset(double offsetMeters)
    {
        var act = () => OnshapeFeatureTemplates.OffsetPlane("Top", offsetMeters);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void OffsetPlane_AllowsZeroOffsetAndPreservesOppositeDirection()
    {
        using var doc = OnshapeFeatureTemplates.OffsetPlane("Right", 0, oppositeDirection: true);
        var parameters = doc.RootElement.GetProperty("feature").GetProperty("parameters");

        parameters[2].GetProperty("expression").GetString().Should().Be("0 m");
        parameters[4].GetProperty("value").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public void Shell_UsesLiveProvenFlatContractInExactParameterOrder()
    {
        using var doc = OnshapeFeatureTemplates.Shell(["JHK", "JHL"], 0.001, oppositeDirection: true);

        var feature = doc.RootElement.GetProperty("feature");
        feature.GetProperty("featureType").GetString().Should().Be("shell");
        var parameters = feature.GetProperty("parameters").EnumerateArray().ToArray();
        parameters.Select(parameter => parameter.GetProperty("parameterId").GetString()).Should().Equal(
            "isHollow", "entities", "parts", "thickness", "oppositeDirection");
        parameters[0].GetProperty("value").GetBoolean().Should().BeFalse();
        parameters[1].GetProperty("btType").GetString().Should().Be("BTMParameterQueryList-148");
        parameters[1].GetProperty("queries")[0].GetProperty("btType").GetString().Should().Be("BTMIndividualQuery-138");
        parameters[1].GetProperty("queries")[0].GetProperty("deterministicIds").EnumerateArray()
            .Select(id => id.GetString()).Should().Equal("JHK", "JHL");
        parameters[2].GetProperty("queries").GetArrayLength().Should().Be(0);
        parameters[3].GetProperty("btType").GetString().Should().Be("BTMParameterQuantity-147");
        parameters[3].GetProperty("expression").GetString().Should().Be("0.001 m");
        parameters[4].GetProperty("value").GetBoolean().Should().BeTrue();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void Shell_RejectsNonPositiveOrNonFiniteThickness(double thicknessMeters)
    {
        var act = () => OnshapeFeatureTemplates.Shell(["JHK"], thicknessMeters);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void Shell_RejectsEmptyFaceIds(string faceId)
    {
        var act = () => OnshapeFeatureTemplates.Shell([faceId], 0.001);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Shell_RejectsNoFaceIds()
    {
        var act = () => OnshapeFeatureTemplates.Shell([], 0.001);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void WorkPointOnEdge_UsesLiveProvenFlatContractInExactParameterOrder()
    {
        using var doc = OnshapeFeatureTemplates.WorkPointOnEdge("JPV", 0.5);

        var feature = doc.RootElement.GetProperty("feature");
        feature.GetProperty("featureType").GetString().Should().Be("cPoint");
        var parameters = feature.GetProperty("parameters").EnumerateArray().ToArray();
        parameters.Select(parameter => parameter.GetProperty("parameterId").GetString()).Should().Equal(
            "pointType", "entities", "parameter", "mesh", "x", "y", "z");
        parameters[0].GetProperty("btType").GetString().Should().Be("BTMParameterEnum-145");
        parameters[0].GetProperty("enumName").GetString().Should().Be("PointType");
        parameters[0].GetProperty("value").GetString().Should().Be("EDGE_POINT");
        parameters[1].GetProperty("queries")[0].GetProperty("deterministicIds")[0].GetString().Should().Be("JPV");
        parameters[2].GetProperty("btType").GetString().Should().Be("BTMParameterQuantity-147");
        parameters[2].GetProperty("expression").GetString().Should().Be("0.5");
        parameters[3].GetProperty("queries").GetArrayLength().Should().Be(0);
        foreach (var coordinate in parameters[4..])
        {
            coordinate.GetProperty("expression").GetString().Should().BeEmpty();
            coordinate.GetProperty("value").GetDouble().Should().Be(0.025);
            coordinate.GetProperty("units").GetString().Should().Be("meter");
        }
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void WorkPointOnEdge_RejectsBlankEdgeId(string edgeId)
    {
        var act = () => OnshapeFeatureTemplates.WorkPointOnEdge(edgeId, 0.5);

        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void WorkPointOnEdge_AllowsNormalizedParameterEndpoints(double parameter)
    {
        using var doc = OnshapeFeatureTemplates.WorkPointOnEdge("JPV", parameter);

        doc.RootElement.GetProperty("feature").GetProperty("parameters")[2]
            .GetProperty("expression").GetString().Should().Be(parameter == 0 ? "0" : "1");
    }

    [Theory]
    [InlineData(-0.001)]
    [InlineData(1.001)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void WorkPointOnEdge_RejectsOutOfRangeOrNonFiniteParameter(double parameter)
    {
        var act = () => OnshapeFeatureTemplates.WorkPointOnEdge("JPV", parameter);

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
