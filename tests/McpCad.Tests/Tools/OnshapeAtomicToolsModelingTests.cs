using McpCad.Core;
using McpCad.Core.Models;
using McpCad.Tests.Mocks;
using McpCad.Tools.Onshape;
using System.Text.Json;

namespace McpCad.Tests.Tools;

public class OnshapeAtomicToolsModelingTests
{
    private static string SampleAddress() =>
        new CadElementAddress("doc-1", "ws-1", null, "e-part").Serialize();

    [Fact]
    public void onshape_sketch_rectangle_Calls_SketchReplace_WithEntities()
    {
        var mock = new MockOnshapeProvider();
        var tools = new OnshapeAtomicTools(mock);

        var result = tools.onshape_sketch_rectangle(SampleAddress(), 0, 0, 5, 5);

        result["success"].Should().Be(true);
        mock.CallLog.Should().Contain(c => c.Method == "SketchReplace");
        var sketchCall = mock.CallLog.First(c => c.Method == "SketchReplace");
        sketchCall.Args["hasSketchRequest"].Should().Be(true);
    }

    [Theory]
    [InlineData("line")]
    [InlineData("circle")]
    [InlineData("arc")]
    public void OnshapeSketchAtomicTools_DelegateWithSketchPayload(string sketchType)
    {
        var mock = new MockOnshapeProvider();
        var tools = new OnshapeAtomicTools(mock);
        var result = sketchType switch
        {
            "line" => tools.onshape_sketch_line(SampleAddress(), 1, 2, 3, 4, "front"),
            "circle" => tools.onshape_sketch_circle(SampleAddress(), 1, 2, 3, "right"),
            "arc" => tools.onshape_sketch_arc(SampleAddress(), 1, 2, 3, 0, 90, "top"),
            _ => throw new ArgumentOutOfRangeException(nameof(sketchType)),
        };

        result["success"].Should().Be(true);
        var call = mock.CallLog.Should().ContainSingle(c => c.Method == "SketchReplace").Subject;
        var request = (JsonElement)call.Args["sketchRequest"]!;
        request.GetProperty("feature").GetProperty("parameters")[0]
            .GetProperty("queries")[0].GetProperty("deterministicIds")[0].GetString()
            .Should().Be(sketchType == "circle" ? "JEC" : sketchType == "line" ? "JCC" : "JDC");
    }

    [Fact]
    public void onshape_sketch_line_ConvertsCentimetersToMetersAtToolBoundary()
    {
        var mock = new MockOnshapeProvider();
        var tools = new OnshapeAtomicTools(mock);

        tools.onshape_sketch_line(SampleAddress(), 1, 2, 3, 4);

        var request = (JsonElement)mock.CallLog.Single(c => c.Method == "SketchReplace").Args["sketchRequest"]!;
        var geometry = request.GetProperty("feature").GetProperty("entities")[0].GetProperty("geometry");
        geometry.GetProperty("pntX").GetDouble().Should().BeApproximately(0.02, 1e-9);
        geometry.GetProperty("pntY").GetDouble().Should().BeApproximately(0.03, 1e-9);
    }

    [Fact]
    public void onshape_sketch_circle_ConvertsCenterAndRadiusToMeters_AndPropagatesPlane()
    {
        var mock = new MockOnshapeProvider();
        var tools = new OnshapeAtomicTools(mock);

        tools.onshape_sketch_circle(SampleAddress(), 2, 4, 6, "right");

        var request = (JsonElement)mock.CallLog.Single(c => c.Method == "SketchReplace").Args["sketchRequest"]!;
        var entity = request.GetProperty("feature").GetProperty("entities")[0];
        var geometry = entity.GetProperty("geometry");
        geometry.GetProperty("xCenter").GetDouble().Should().BeApproximately(0.02, 1e-9);
        geometry.GetProperty("yCenter").GetDouble().Should().BeApproximately(0.04, 1e-9);
        geometry.GetProperty("radius").GetDouble().Should().BeApproximately(0.06, 1e-9);
        AssertSketchPlane(request, "JEC");
    }

    [Fact]
    public void onshape_sketch_line_WithLength_ConvertsCentimetersToMetersAndPropagatesPlane()
    {
        var mock = new MockOnshapeProvider();
        var tools = new OnshapeAtomicTools(mock);

        tools.onshape_sketch_line(SampleAddress(), 0, 0, 10, 0, "front", 10);

        var request = (JsonElement)mock.CallLog.Single(c => c.Method == "SketchReplace").Args["sketchRequest"]!;
        var constraint = request.GetProperty("feature").GetProperty("constraints")[0];
        constraint.GetProperty("constraintType").GetString().Should().Be("LENGTH");
        constraint.GetProperty("parameters")[1].GetProperty("expression").GetString().Should().Be("0.1*m");
        AssertSketchPlane(request, "JCC");
    }

    [Fact]
    public void onshape_sketch_circle_WithDiameter_ConvertsCentimetersToMetersAndPropagatesPlane()
    {
        var mock = new MockOnshapeProvider();
        var tools = new OnshapeAtomicTools(mock);

        tools.onshape_sketch_circle(SampleAddress(), 2, 4, 2, "right", 4);

        var request = (JsonElement)mock.CallLog.Single(c => c.Method == "SketchReplace").Args["sketchRequest"]!;
        var constraint = request.GetProperty("feature").GetProperty("constraints")[0];
        constraint.GetProperty("constraintType").GetString().Should().Be("DIAMETER");
        constraint.GetProperty("parameters")[1].GetProperty("expression").GetString().Should().Be("0.04*m");
        AssertSketchPlane(request, "JEC");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void onshape_sketch_dimension_RejectsNonPositiveOrNonFiniteValuesWithoutCallingProvider(double dimensionCm)
    {
        var lineMock = new MockOnshapeProvider();
        var circleMock = new MockOnshapeProvider();
        var lineTools = new OnshapeAtomicTools(lineMock);
        var circleTools = new OnshapeAtomicTools(circleMock);

        var lineResult = lineTools.onshape_sketch_line(SampleAddress(), 0, 0, 10, 0, length_cm: dimensionCm);
        var circleResult = circleTools.onshape_sketch_circle(SampleAddress(), 0, 0, 2, diameter_cm: dimensionCm);

        lineResult["success"].Should().Be(false);
        circleResult["success"].Should().Be(false);
        lineMock.CallLog.Should().BeEmpty();
        circleMock.CallLog.Should().BeEmpty();
    }

    [Fact]
    public void onshape_sketch_dimension_WhenProviderRejects_ReturnsError()
    {
        var tools = new OnshapeAtomicTools(MockOnshapeProvider.WithError("feature failed"));

        var lineResult = tools.onshape_sketch_line(SampleAddress(), 0, 0, 10, 0, length_cm: 10);
        var circleResult = tools.onshape_sketch_circle(SampleAddress(), 0, 0, 2, diameter_cm: 4);

        lineResult["success"].Should().Be(false);
        lineResult["error"].Should().Be("feature failed");
        circleResult["success"].Should().Be(false);
        circleResult["error"].Should().Be("feature failed");
    }

    [Fact]
    public void onshape_sketch_ellipse_ConvertsCentimetersAndDimensionsToMeters_AndPropagatesPlane()
    {
        var mock = new MockOnshapeProvider();
        var tools = new OnshapeAtomicTools(mock);

        var result = tools.onshape_sketch_ellipse(SampleAddress(), 2, 4, 6, 3, 30, "front", 12, 6);

        result["success"].Should().Be(true);
        var request = (JsonElement)mock.CallLog.Single(c => c.Method == "SketchReplace").Args["sketchRequest"]!;
        var geometry = request.GetProperty("feature").GetProperty("entities")[0].GetProperty("geometry");
        geometry.GetProperty("xCenter").GetDouble().Should().BeApproximately(0.02, 1e-9);
        geometry.GetProperty("yCenter").GetDouble().Should().BeApproximately(0.04, 1e-9);
        geometry.GetProperty("radius").GetDouble().Should().BeApproximately(0.06, 1e-9);
        geometry.GetProperty("minorRadius").GetDouble().Should().BeApproximately(0.03, 1e-9);
        geometry.GetProperty("xDir").GetDouble().Should().BeApproximately(Math.Sqrt(3) / 2, 1e-9);
        geometry.GetProperty("yDir").GetDouble().Should().BeApproximately(0.5, 1e-9);
        var constraints = request.GetProperty("feature").GetProperty("constraints");
        constraints.GetArrayLength().Should().Be(2);
        constraints[0].GetProperty("constraintType").GetString().Should().Be("MAJOR_DIAMETER");
        constraints[0].GetProperty("parameters")[1].GetProperty("expression").GetString().Should().Be("0.12*m");
        constraints[1].GetProperty("constraintType").GetString().Should().Be("MINOR_DIAMETER");
        constraints[1].GetProperty("parameters")[1].GetProperty("expression").GetString().Should().Be("0.06*m");
        AssertSketchPlane(request, "JCC");
    }

    [Theory]
    [InlineData(double.NaN, 2, 1, 0)]
    [InlineData(0, 0, 1, 0)]
    [InlineData(0, 1, 2, 0)]
    [InlineData(0, 2, 1, double.PositiveInfinity)]
    public void onshape_sketch_ellipse_RejectsInvalidGeometryWithoutCallingProvider(
        double cxCm, double majorRadiusCm, double minorRadiusCm, double rotationDeg)
    {
        var mock = new MockOnshapeProvider();
        var tools = new OnshapeAtomicTools(mock);

        var result = tools.onshape_sketch_ellipse(SampleAddress(), cxCm, 0, majorRadiusCm, minorRadiusCm, rotationDeg);

        result["success"].Should().Be(false);
        mock.CallLog.Should().BeEmpty();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(double.NaN)]
    public void onshape_sketch_ellipse_RejectsInvalidOptionalDimensionsWithoutCallingProvider(double diameterCm)
    {
        var mock = new MockOnshapeProvider();
        var tools = new OnshapeAtomicTools(mock);

        var result = tools.onshape_sketch_ellipse(SampleAddress(), 0, 0, 2, 1, major_diameter_cm: diameterCm);

        result["success"].Should().Be(false);
        mock.CallLog.Should().BeEmpty();
    }

    [Fact]
    public void onshape_sketch_ellipse_WhenProviderRejects_ReturnsError()
    {
        var tools = new OnshapeAtomicTools(MockOnshapeProvider.WithError("feature failed"));

        var result = tools.onshape_sketch_ellipse(SampleAddress(), 0, 0, 2, 1);

        result["success"].Should().Be(false);
        result["error"].Should().Be("feature failed");
    }

    [Fact]
    public void onshape_sketch_arc_ConvertsCenterAndRadius_AndPropagatesAnglesAndPlane()
    {
        var mock = new MockOnshapeProvider();
        var tools = new OnshapeAtomicTools(mock);

        tools.onshape_sketch_arc(SampleAddress(), 2, 4, 6, 30, 120, "front");

        var request = (JsonElement)mock.CallLog.Single(c => c.Method == "SketchReplace").Args["sketchRequest"]!;
        var entity = request.GetProperty("feature").GetProperty("entities")[0];
        entity.GetProperty("btType").GetString().Should().Be("BTMSketchCurveSegment-155");
        entity.GetProperty("startPointId").GetString().Should().Be("point-0");
        entity.GetProperty("endPointId").GetString().Should().Be("point-1");
        entity.GetProperty("centerId").GetString().Should().Be("point-center");
        entity.GetProperty("geometry").GetProperty("btType").GetString().Should().Be("BTCurveGeometryCircle-115");
        var geometry = entity.GetProperty("geometry");
        geometry.GetProperty("xCenter").GetDouble().Should().BeApproximately(0.02, 1e-9);
        geometry.GetProperty("yCenter").GetDouble().Should().BeApproximately(0.04, 1e-9);
        geometry.GetProperty("radius").GetDouble().Should().BeApproximately(0.06, 1e-9);
        entity.GetProperty("startParam").GetDouble().Should().BeApproximately(Math.PI / 6, 1e-8);
        entity.GetProperty("endParam").GetDouble().Should().BeApproximately(2 * Math.PI / 3, 1e-8);
        AssertSketchPlane(request, "JCC");
    }

    [Fact]
    public void onshape_sketch_arc_WhenProviderRejects_ReturnsError()
    {
        var tools = new OnshapeAtomicTools(MockOnshapeProvider.WithError("feature failed"));

        var result = tools.onshape_sketch_arc(SampleAddress(), 1, 2, 3, 0, 90);

        result["success"].Should().Be(false);
        result["error"].Should().Be("feature failed");
    }

    [Fact]
    public void onshape_sketch_polyline_ConvertsCoordinatesAndPropagatesPlane()
    {
        var mock = new MockOnshapeProvider();
        var tools = new OnshapeAtomicTools(mock);

        var result = tools.onshape_sketch_polyline(
            SampleAddress(),
            "[{\"x_cm\":0,\"y_cm\":0},{\"x_cm\":10,\"y_cm\":0},{\"x_cm\":10,\"y_cm\":5}]",
            plane: "right");

        result["success"].Should().Be(true);
        var request = (JsonElement)mock.CallLog.Single(c => c.Method == "SketchReplace").Args["sketchRequest"]!;
        var entities = request.GetProperty("feature").GetProperty("entities");
        entities[0].GetProperty("geometry").GetProperty("pntX").GetDouble().Should().BeApproximately(0.05, 1e-9);
        entities[1].GetProperty("geometry").GetProperty("pntY").GetDouble().Should().BeApproximately(0.025, 1e-9);
        request.GetProperty("feature").GetProperty("constraints").GetArrayLength().Should().Be(3);
        AssertSketchPlane(request, "JEC");
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("[{\"x_cm\":0,\"y_cm\":0}]")]
    [InlineData("[{\"x_cm\":0,\"y_cm\":0},{\"x_cm\":1,\"y_cm\":1}]")]
    public void onshape_sketch_polyline_InvalidPoints_ReturnsErrorWithoutCallingProvider(string pointsJson)
    {
        var mock = new MockOnshapeProvider();
        var tools = new OnshapeAtomicTools(mock);

        var result = tools.onshape_sketch_polyline(SampleAddress(), pointsJson);

        result["success"].Should().Be(false);
        mock.CallLog.Should().BeEmpty();
    }

    [Fact]
    public void onshape_sketch_polyline_WhenProviderRejects_ReturnsError()
    {
        var tools = new OnshapeAtomicTools(MockOnshapeProvider.WithError("feature failed"));

        var result = tools.onshape_sketch_polyline(
            SampleAddress(), "[{\"x_cm\":0,\"y_cm\":0},{\"x_cm\":1,\"y_cm\":0}]");

        result["success"].Should().Be(false);
        result["error"].Should().Be("feature failed");
    }

    [Fact]
    public void onshape_sketch_point_ConvertsCoordinatesAndPropagatesPlaneAndConstructionFlag()
    {
        var mock = new MockOnshapeProvider();
        var tools = new OnshapeAtomicTools(mock);

        var result = tools.onshape_sketch_point(SampleAddress(), 1.25, -2.5, "front", construction: true);

        result["success"].Should().Be(true);
        var request = (JsonElement)mock.CallLog.Single(call => call.Method == "SketchReplace").Args["sketchRequest"]!;
        var point = request.GetProperty("feature").GetProperty("entities")[0];
        point.GetProperty("x").GetDouble().Should().BeApproximately(0.0125, 1e-9);
        point.GetProperty("y").GetDouble().Should().BeApproximately(-0.025, 1e-9);
        point.GetProperty("isConstruction").GetBoolean().Should().BeTrue();
        point.GetProperty("isUserPoint").GetBoolean().Should().BeTrue();
        point.TryGetProperty("geometry", out _).Should().BeFalse();
        AssertSketchPlane(request, "JCC");
    }

    [Theory]
    [InlineData(double.NaN, 0)]
    [InlineData(0, double.NegativeInfinity)]
    public void onshape_sketch_point_RejectsNonFiniteCoordinatesWithoutCallingProvider(double xCm, double yCm)
    {
        var mock = new MockOnshapeProvider();
        var tools = new OnshapeAtomicTools(mock);

        var result = tools.onshape_sketch_point(SampleAddress(), xCm, yCm);

        result["success"].Should().Be(false);
        mock.CallLog.Should().BeEmpty();
    }

    [Fact]
    public void onshape_sketch_point_WhenProviderRejects_ReturnsError()
    {
        var tools = new OnshapeAtomicTools(MockOnshapeProvider.WithError("feature failed"));

        var result = tools.onshape_sketch_point(SampleAddress(), 1, 2);

        result["success"].Should().Be(false);
        result["error"].Should().Be("feature failed");
    }

    private static void AssertSketchPlane(JsonElement request, string deterministicId)
    {
        request.GetProperty("feature").GetProperty("parameters")[0]
            .GetProperty("parameterId").GetString().Should().Be("sketchPlane");
        request.GetProperty("feature").GetProperty("parameters")[0]
            .GetProperty("queries")[0].GetProperty("deterministicIds")[0].GetString()
            .Should().Be(deterministicId);
    }

    [Fact]
    public void onshape_extrude_Calls_FeatureAdd_WithEnvelope()
    {
        var mock = new MockOnshapeProvider();
        var tools = new OnshapeAtomicTools(mock);

        var result = tools.onshape_extrude(SampleAddress(), "sketch-1", 2.5, "positive", "new");

        result["success"].Should().Be(true);
        mock.CallLog.Should().Contain(c => c.Method == "FeatureAdd");
        mock.CallLog.First(c => c.Method == "FeatureAdd").Args["hasEnvelope"].Should().Be(true);
    }

    [Fact]
    public void onshape_sweep_CallsFeatureAdd_WithSweepEnvelope()
    {
        var mock = new MockOnshapeProvider();
        var tools = new OnshapeAtomicTools(mock);

        var result = tools.onshape_sweep(SampleAddress(), "profile-sketch", "path-sketch");

        result["success"].Should().Be(true);
        var call = mock.CallLog.Should().ContainSingle(c => c.Method == "FeatureAdd").Subject;
        call.Args["featureType"].Should().Be("sweep");
        call.Args["hasEnvelope"].Should().Be(true);
    }

    [Fact]
    public void onshape_sweep_WhenProviderRejects_ReturnsError()
    {
        var tools = new OnshapeAtomicTools(MockOnshapeProvider.WithError("sweep failed"));

        var result = tools.onshape_sweep(SampleAddress(), "profile-sketch", "path-sketch");

        result["success"].Should().Be(false);
        result["error"].Should().Be("sweep failed");
    }

    [Fact]
    public void onshape_feature_list_Calls_FeatureList()
    {
        var mock = new MockOnshapeProvider();
        var tools = new OnshapeAtomicTools(mock);

        var result = tools.onshape_feature_list(SampleAddress());

        result["success"].Should().Be(true);
        mock.CallLog.Should().ContainSingle(c => c.Method == "FeatureList");
    }

    [Fact]
    public void onshape_sketch_rectangle_InvalidAddress_ReturnsError()
    {
        var mock = new MockOnshapeProvider();
        var tools = new OnshapeAtomicTools(mock);

        var result = tools.onshape_sketch_rectangle("not-json", 0, 0, 1, 1);

        result["success"].Should().Be(false);
        result.Should().ContainKey("error");
    }

    [Fact]
    public void P0EndToEnd_MockProvider_SketchListExtrude()
    {
        var mock = new MockOnshapeProvider();
        var tools = new OnshapeAtomicTools(mock);
        var addr = SampleAddress();

        tools.onshape_sketch_rectangle(addr, 0, 0, 10, 10).Should().ContainKey("success");
        tools.onshape_feature_list(addr).Should().ContainKey("success");
        tools.onshape_extrude(addr, "sketch-1", 1.0).Should().ContainKey("success");

        mock.CallLog.Select(c => c.Method).Should().ContainInOrder("SketchReplace", "FeatureList", "FeatureAdd");
    }

    [Theory]
    [InlineData("revolve")]
    [InlineData("fillet")]
    [InlineData("chamfer")]
    public void P1Tool_CallsFeatureAdd_WithExpectedEnvelope(string featureType)
    {
        var mock = new MockOnshapeProvider();
        var tools = new OnshapeAtomicTools(mock);
        var address = SampleAddress();

        var result = featureType switch
        {
            "revolve" => tools.onshape_revolve(address, "sketch-1", "axis-1", 180),
            "fillet" => tools.onshape_fillet(address, ["edge-1"], 0.5),
            "chamfer" => tools.onshape_chamfer(address, ["edge-1"], 0.3),
            _ => throw new ArgumentOutOfRangeException(nameof(featureType)),
        };

        result["success"].Should().Be(true);
        var call = mock.CallLog.Should().ContainSingle(c => c.Method == "FeatureAdd").Subject;
        call.Args["featureType"].Should().Be(featureType);
        call.Args["hasEnvelope"].Should().Be(true);
    }

    [Fact]
    public void onshape_shell_ConvertsCentimetersAndDelegatesLiveProvenEnvelope()
    {
        string? payload = null;
        var provider = new Mock<ICadWebProvider>();
        provider.Setup(p => p.FeatureAdd(
                It.IsAny<CadElementAddress>(), "shell", null, It.IsAny<string?>()))
            .Callback<CadElementAddress, string, IReadOnlyList<Dictionary<string, object?>>?, string?>(
                (_, _, _, featurePostBodyJson) => payload = featurePostBodyJson)
            .Returns(new Dictionary<string, object?> { ["success"] = true });
        var tools = new OnshapeAtomicTools(provider.Object);

        var result = tools.onshape_shell(SampleAddress(), ["JHK", "JHL"], 0.1, opposite_direction: true);

        result["success"].Should().Be(true);
        using var envelope = JsonDocument.Parse(payload!);
        var parameters = envelope.RootElement.GetProperty("feature").GetProperty("parameters");
        parameters[1].GetProperty("queries")[0].GetProperty("deterministicIds").EnumerateArray()
            .Select(id => id.GetString()).Should().Equal("JHK", "JHL");
        parameters[3].GetProperty("expression").GetString().Should().Be("0.001 m");
        parameters[4].GetProperty("value").GetBoolean().Should().BeTrue();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(double.NaN)]
    public void onshape_shell_RejectsInvalidInputWithoutCallingProvider(double thicknessCm)
    {
        var mock = new MockOnshapeProvider();
        var tools = new OnshapeAtomicTools(mock);

        var result = tools.onshape_shell(SampleAddress(), ["JHK"], thicknessCm);

        result["success"].Should().Be(false);
        mock.CallLog.Should().BeEmpty();
    }

    [Fact]
    public void onshape_shell_RejectsEmptyFaceIdsWithoutCallingProvider()
    {
        var mock = new MockOnshapeProvider();
        var tools = new OnshapeAtomicTools(mock);

        var result = tools.onshape_shell(SampleAddress(), [], 0.1);

        result["success"].Should().Be(false);
        mock.CallLog.Should().BeEmpty();
    }

    [Fact]
    public void onshape_shell_RejectsInvalidAddressWithoutCallingProvider()
    {
        var mock = new MockOnshapeProvider();
        var tools = new OnshapeAtomicTools(mock);

        var result = tools.onshape_shell("not-json", ["JHK"], 0.1);

        result["success"].Should().Be(false);
        mock.CallLog.Should().BeEmpty();
    }

    [Fact]
    public void onshape_shell_WhenProviderRejects_ReturnsError()
    {
        var tools = new OnshapeAtomicTools(MockOnshapeProvider.WithError("shell failed"));

        var result = tools.onshape_shell(SampleAddress(), ["JHK"], 0.1);

        result["success"].Should().Be(false);
        result["error"].Should().Be("shell failed");
    }

    [Fact]
    public void onshape_circular_pattern_UsesFeatureAddWithTypedNativeEnvelope()
    {
        var mock = new MockOnshapeProvider();
        var tools = new OnshapeAtomicTools(mock);

        var result = tools.onshape_circular_pattern(
            SampleAddress(), "extrude-1", "axis-sketch-1", 4, 180, opposite_direction: true, equal_spacing: false);

        result["success"].Should().Be(true);
        var call = mock.CallLog.Should().ContainSingle(c => c.Method == "FeatureAdd").Subject;
        call.Args["featureType"].Should().Be("circularPattern");
        call.Args["hasEnvelope"].Should().Be(true);
    }

    [Fact]
    public void onshape_rectangular_pattern_ConvertsCentimetersAndDelegatesTypedLinearPatternEnvelope()
    {
        string? payload = null;
        var provider = new Mock<ICadWebProvider>();
        provider.Setup(p => p.FeatureAdd(
                It.IsAny<CadElementAddress>(), "linearPattern", null, It.IsAny<string?>()))
            .Callback<CadElementAddress, string, IReadOnlyList<Dictionary<string, object?>>?, string?>(
                (_, _, _, featurePostBodyJson) => payload = featurePostBodyJson)
            .Returns(new Dictionary<string, object?> { ["success"] = true });
        var tools = new OnshapeAtomicTools(provider.Object);

        var result = tools.onshape_rectangular_pattern(
            SampleAddress(), "extrude-1", "direction-one-1", 4.7, 2,
            "direction-two-1", 6.3, 3, true, true, true, true);

        result["success"].Should().Be(true);
        using var envelope = JsonDocument.Parse(payload!);
        var parameters = envelope.RootElement.GetProperty("feature").GetProperty("parameters");
        parameters[6].GetProperty("expression").GetString().Should().Be("0.047 m");
        parameters[12].GetProperty("expression").GetString().Should().Be("0.063 m");
    }

        [Fact]
        public void onshape_rectangular_pattern_RejectsPartialSecondDirectionWithoutCallingProvider()
        {
            var mock = new MockOnshapeProvider();
            var tools = new OnshapeAtomicTools(mock);

            var result = tools.onshape_rectangular_pattern(
                SampleAddress(), "extrude-1", "direction-one-1", 3, 2,
                direction_two_sketch_feature_id: "direction-two-1");

            result["success"].Should().Be(false);
            mock.CallLog.Should().BeEmpty();
        }

        [Theory]
        [InlineData(1, 180)]
        [InlineData(101, 180)]
        [InlineData(2, 0)]
        [InlineData(2, 360.1)]
        [InlineData(2, double.NaN)]
        public void onshape_circular_pattern_RejectsOutOfRangeOrNonFiniteInputsWithoutCallingProvider(
        int instanceCount, double totalAngleDeg)
    {
        var mock = new MockOnshapeProvider();
        var tools = new OnshapeAtomicTools(mock);

        var result = tools.onshape_circular_pattern(
            SampleAddress(), "extrude-1", "axis-sketch-1", instanceCount, totalAngleDeg);

        result["success"].Should().Be(false);
        mock.CallLog.Should().BeEmpty();
    }

    [Fact]
    public void onshape_circular_pattern_WhenProviderRejects_ReturnsError()
    {
        var tools = new OnshapeAtomicTools(MockOnshapeProvider.WithError("pattern failed"));

        var result = tools.onshape_circular_pattern(SampleAddress(), "extrude-1", "axis-sketch-1", 4, 180);

        result["success"].Should().Be(false);
        result["error"].Should().Be("pattern failed");
    }

    [Fact]
    public void onshape_mirror_feature_UsesFeatureAddWithTypedNativeEnvelope()
    {
        var mock = new MockOnshapeProvider();
        var tools = new OnshapeAtomicTools(mock);

        var result = tools.onshape_mirror_feature(SampleAddress(), "extrude-1", "front");

        result["success"].Should().Be(true);
        var call = mock.CallLog.Should().ContainSingle(c => c.Method == "FeatureAdd").Subject;
        call.Args["featureType"].Should().Be("mirror");
        call.Args["hasEnvelope"].Should().Be(true);
    }

    [Theory]
    [InlineData("")]
    [InlineData("diagonal")]
    public void onshape_mirror_feature_RejectsInvalidPlaneWithoutCallingProvider(string mirrorPlane)
    {
        var mock = new MockOnshapeProvider();
        var tools = new OnshapeAtomicTools(mock);

        var result = tools.onshape_mirror_feature(SampleAddress(), "extrude-1", mirrorPlane);

        result["success"].Should().Be(false);
        mock.CallLog.Should().BeEmpty();
    }

    [Fact]
    public void onshape_mirror_feature_WhenProviderRejects_ReturnsError()
    {
        var tools = new OnshapeAtomicTools(MockOnshapeProvider.WithError("mirror failed"));

        var result = tools.onshape_mirror_feature(SampleAddress(), "extrude-1", "Front");

        result["success"].Should().Be(false);
        result["error"].Should().Be("mirror failed");
    }

    [Fact]
    public void onshape_work_axis_CreatesSketchBackedConstructionAxis()
        {
            var mock = new MockOnshapeProvider();
            var tools = new OnshapeAtomicTools(mock);

            var result = tools.onshape_work_axis(SampleAddress(), "Front", "y");

            result["success"].Should().Be(true);
            var request = (JsonElement)mock.CallLog.Should().ContainSingle(call => call.Method == "SketchReplace").Subject.Args["sketchRequest"]!;
            var feature = request.GetProperty("feature");
            feature.GetProperty("parameters")[0].GetProperty("queries")[0].GetProperty("deterministicIds")[0].GetString().Should().Be("JCC");
            feature.GetProperty("parameters")[1].GetProperty("parameterId").GetString().Should().Be("disableImprinting");
            feature.GetProperty("parameters")[1].GetProperty("value").GetBoolean().Should().BeTrue();
            var axis = feature.GetProperty("entities");
            axis.GetArrayLength().Should().Be(1);
            axis = axis[0];
            axis.GetProperty("isConstruction").GetBoolean().Should().BeTrue();
            axis.GetProperty("geometry").GetProperty("dirX").GetDouble().Should().Be(0);
            axis.GetProperty("geometry").GetProperty("dirY").GetDouble().Should().Be(1);
        }

        [Theory]
        [InlineData("diagonal", "x")]
        [InlineData("Top", "z")]
        public void onshape_work_axis_RejectsInvalidPlaneOrDirectionWithoutCallingProvider(string canonicalPlane, string localDirection)
        {
            var mock = new MockOnshapeProvider();
            var tools = new OnshapeAtomicTools(mock);

            var result = tools.onshape_work_axis(SampleAddress(), canonicalPlane, localDirection);

            result["success"].Should().Be(false);
            mock.CallLog.Should().BeEmpty();
        }

        [Fact]
        public void onshape_work_axis_WhenProviderRejects_ReturnsError()
        {
            var tools = new OnshapeAtomicTools(MockOnshapeProvider.WithError("axis failed"));

            var result = tools.onshape_work_axis(SampleAddress(), "Front");

            result["success"].Should().Be(false);
            result["error"].Should().Be("axis failed");
        }

        [Fact]
        public void onshape_work_point_DelegatesNativeEdgePointEnvelope()
        {
            var mock = new MockOnshapeProvider();
            var tools = new OnshapeAtomicTools(mock);

            var result = tools.onshape_work_point(SampleAddress(), "JPV");

            result["success"].Should().Be(true);
            var call = mock.CallLog.Should().ContainSingle(c => c.Method == "FeatureAdd").Subject;
            call.Args["featureType"].Should().Be("cPoint");
            call.Args["hasEnvelope"].Should().Be(true);
        }

        [Theory]
        [InlineData("", 0.5)]
        [InlineData("JPV", -0.1)]
        [InlineData("JPV", 1.1)]
        [InlineData("JPV", double.NaN)]
        public void onshape_work_point_RejectsInvalidInputWithoutCallingProvider(string edgeId, double parameter)
        {
            var mock = new MockOnshapeProvider();
            var tools = new OnshapeAtomicTools(mock);

            var result = tools.onshape_work_point(SampleAddress(), edgeId, parameter);

            result["success"].Should().Be(false);
            mock.CallLog.Should().BeEmpty();
        }

        [Fact]
        public void onshape_work_point_WhenProviderRejects_ReturnsError()
        {
            var tools = new OnshapeAtomicTools(MockOnshapeProvider.WithError("point failed"));

            var result = tools.onshape_work_point(SampleAddress(), "JPV");

            result["success"].Should().Be(false);
            result["error"].Should().Be("point failed");
        }

        [Fact]
        public void onshape_work_plane_ConvertsCentimetersAndDelegatesOffsetPlaneEnvelope()
    {
        string? payload = null;
        var provider = new Mock<ICadWebProvider>();
        provider.Setup(p => p.FeatureAdd(
                It.IsAny<CadElementAddress>(), "cPlane", null, It.IsAny<string?>()))
            .Callback<CadElementAddress, string, IReadOnlyList<Dictionary<string, object?>>?, string?>(
                (_, _, _, featurePostBodyJson) => payload = featurePostBodyJson)
            .Returns(new Dictionary<string, object?> { ["success"] = true });
        var tools = new OnshapeAtomicTools(provider.Object);

        var result = tools.onshape_work_plane(SampleAddress(), "Front", 1);

        result["success"].Should().Be(true);
        using var envelope = JsonDocument.Parse(payload!);
        var parameters = envelope.RootElement.GetProperty("feature").GetProperty("parameters");
        parameters[0].GetProperty("queries")[0].GetProperty("deterministicIds")[0].GetString().Should().Be("JCC");
        parameters[2].GetProperty("expression").GetString().Should().Be("0.01 m");
        parameters[4].GetProperty("value").GetBoolean().Should().BeFalse();
    }

    [Theory]
    [InlineData("diagonal", 1)]
    [InlineData("Top", -1)]
    [InlineData("Top", double.NaN)]
    public void onshape_work_plane_RejectsInvalidPlaneOrOffsetWithoutCallingProvider(string canonicalPlane, double offsetCm)
    {
        var mock = new MockOnshapeProvider();
        var tools = new OnshapeAtomicTools(mock);

        var result = tools.onshape_work_plane(SampleAddress(), canonicalPlane, offsetCm);

        result["success"].Should().Be(false);
        mock.CallLog.Should().BeEmpty();
    }

    [Fact]
    public void onshape_work_plane_WhenProviderRejects_ReturnsError()
    {
        var tools = new OnshapeAtomicTools(MockOnshapeProvider.WithError("plane failed"));

        var result = tools.onshape_work_plane(SampleAddress(), "Top", 1);

        result["success"].Should().Be(false);
        result["error"].Should().Be("plane failed");
    }

    [Fact]
    public void onshape_sketch_spline_ConvertsPointsAndPropagatesPlaneAndConstruction()
    {
        var mock = new MockOnshapeProvider();
        var tools = new OnshapeAtomicTools(mock);

        var result = tools.onshape_sketch_spline(
            SampleAddress(),
            "[{\"x_cm\":0,\"y_cm\":0},{\"x_cm\":3,\"y_cm\":2},{\"x_cm\":6,\"y_cm\":0}]",
            "right",
            construction: true);

        result["success"].Should().Be(true);
        var request = (JsonElement)mock.CallLog.Single(c => c.Method == "SketchReplace").Args["sketchRequest"]!;
        var entity = request.GetProperty("feature").GetProperty("entities")[0];
        var geometry = entity.GetProperty("geometry");
        entity.GetProperty("isConstruction").GetBoolean().Should().BeTrue();
        geometry.GetProperty("interpolationPoints")[2].GetDouble().Should().BeApproximately(0.03, 1e-9);
        geometry.GetProperty("interpolationPoints")[3].GetDouble().Should().BeApproximately(0.02, 1e-9);
        geometry.GetProperty("isPeriodic").GetBoolean().Should().BeFalse();
        entity.TryGetProperty("internalIds", out _).Should().BeTrue();
        foreach (var oldProperty in new[] { "periodic", "points", "startDerivative", "endDerivative", "startHandle", "endHandle", "internalIds" })
        {
            geometry.TryGetProperty(oldProperty, out _).Should().BeFalse();
        }
        AssertSketchPlane(request, "JEC");
    }

    [Theory]
    [InlineData("[")]
    [InlineData("{}")]
    [InlineData("[{\"x_cm\":0,\"y_cm\":0},{\"x_cm\":1,\"y_cm\":0}]")]
    [InlineData("[{\"x_cm\":0,\"y_cm\":0},{\"x_cm\":1,\"y_cm\":0},{\"x_cm\":1,\"y_cm\":0}]")]
    [InlineData("[{\"x_cm\":0,\"y_cm\":0},{\"x_cm\":1e400,\"y_cm\":0},{\"x_cm\":2,\"y_cm\":0}]")]
    public void onshape_sketch_spline_InvalidPoints_ReturnsErrorWithoutCallingProvider(string pointsJson)
    {
        var mock = new MockOnshapeProvider();
        var tools = new OnshapeAtomicTools(mock);

        var result = tools.onshape_sketch_spline(SampleAddress(), pointsJson);

        result["success"].Should().Be(false);
        mock.CallLog.Should().BeEmpty();
    }

    [Fact]
    public void onshape_sketch_linear_pattern_circle_ConvertsCentimetersAndPropagatesPlane()
    {
        var mock = new MockOnshapeProvider();
        var tools = new OnshapeAtomicTools(mock);

        var result = tools.onshape_sketch_linear_pattern_circle(SampleAddress(), 2, -1, 0.3, 3, 1.5, "right");

        result["success"].Should().Be(true);
        var request = (JsonElement)mock.CallLog.Single(call => call.Method == "SketchReplace").Args["sketchRequest"]!;
        var entities = request.GetProperty("feature").GetProperty("entities");
        entities.GetArrayLength().Should().Be(4);
        entities[0].GetProperty("geometry").GetProperty("xCenter").GetDouble().Should().BeApproximately(0.02, 1e-9);
        entities[0].GetProperty("geometry").GetProperty("yCenter").GetDouble().Should().BeApproximately(-0.01, 1e-9);
        entities[0].GetProperty("geometry").GetProperty("radius").GetDouble().Should().BeApproximately(0.003, 1e-9);
        entities[1].GetProperty("geometry").GetProperty("xCenter").GetDouble().Should().BeApproximately(0.035, 1e-9);
        request.GetProperty("feature").GetProperty("constraints")[0].GetProperty("parameters")[2]
            .GetProperty("expression").GetString().Should().Be("0.015*m");
        AssertSketchPlane(request, "JEC");
    }

    [Theory]
    [InlineData(double.NaN, 0, 1, 2, 1)]
    [InlineData(0, 0, 0, 2, 1)]
    [InlineData(0, 0, 1, 1, 1)]
    [InlineData(0, 0, 1, 101, 1)]
    [InlineData(0, 0, 1, 2, 0)]
    public void onshape_sketch_linear_pattern_circle_RejectsInvalidInputWithoutCallingProvider(
        double cxCm, double cyCm, double radiusCm, int columns, double spacingCm)
    {
        var mock = new MockOnshapeProvider();
        var tools = new OnshapeAtomicTools(mock);

        var result = tools.onshape_sketch_linear_pattern_circle(SampleAddress(), cxCm, cyCm, radiusCm, columns, spacingCm);

        result["success"].Should().Be(false);
        mock.CallLog.Should().BeEmpty();
    }

    [Fact]
    public void onshape_sketch_linear_pattern_circle_WhenProviderRejects_ReturnsError()
    {
        var tools = new OnshapeAtomicTools(MockOnshapeProvider.WithError("feature failed"));

        var result = tools.onshape_sketch_linear_pattern_circle(SampleAddress(), 0, 0, 1, 2, 1);

        result["success"].Should().Be(false);
        result["error"].Should().Be("feature failed");
    }

    [Fact]
    public void onshape_sketch_circular_pattern_line_ConvertsCentimetersAndPropagatesPlane()
    {
        var mock = new MockOnshapeProvider();
        var tools = new OnshapeAtomicTools(mock);

        var result = tools.onshape_sketch_circular_pattern_line(SampleAddress(), 2, -1, 3, 0.8, 3, "right");

        result["success"].Should().Be(true);
        var request = (JsonElement)mock.CallLog.Single(call => call.Method == "SketchReplace").Args["sketchRequest"]!;
        var entities = request.GetProperty("feature").GetProperty("entities");
        entities[0].GetProperty("geometry").GetProperty("xCenter").GetDouble().Should().BeApproximately(0.02, 1e-9);
        entities[0].GetProperty("geometry").GetProperty("radius").GetDouble().Should().BeApproximately(0.03, 1e-9);
        entities[1].GetProperty("startParam").GetDouble().Should().BeApproximately(-0.004, 1e-9);
        AssertSketchPlane(request, "JEC");
    }

    [Theory]
    [InlineData(0, 0, 0, 1, 2)]
    [InlineData(0, 0, 1, 0, 2)]
    [InlineData(0, 0, 1, 1, 1)]
    public void onshape_sketch_circular_pattern_line_RejectsInvalidInputWithoutCallingProvider(double centerXCm, double centerYCm, double orbitRadiusCm, double lineLengthCm, int instances)
    {
        var mock = new MockOnshapeProvider();
        var tools = new OnshapeAtomicTools(mock);

        var result = tools.onshape_sketch_circular_pattern_line(SampleAddress(), centerXCm, centerYCm, orbitRadiusCm, lineLengthCm, instances);

        result["success"].Should().Be(false);
        mock.CallLog.Should().BeEmpty();
    }

    [Fact]
    public void onshape_sketch_circular_pattern_line_WhenProviderRejects_ReturnsError()
    {
        var tools = new OnshapeAtomicTools(MockOnshapeProvider.WithError("feature failed"));

        var result = tools.onshape_sketch_circular_pattern_line(SampleAddress(), 0, 0, 1, 1, 2);

        result["success"].Should().Be(false);
        result["error"].Should().Be("feature failed");
    }

    [Fact]
    public void onshape_inspect_edges_DelegatesParsedAddressAndOptionalExactBodyFilter()
        {
            var mock = new MockOnshapeProvider();
            var tools = new OnshapeAtomicTools(mock);

            var result = tools.onshape_inspect_edges(SampleAddress(), "Body-A");

            result["success"].Should().Be(true);
            result["operation"].Should().Be("inspect_edges");
            var call = mock.CallLog.Should().ContainSingle(c => c.Method == "InspectEdges").Subject;
            call.Args["address"].Should().Be(SampleAddress());
            call.Args["bodyId"].Should().Be("Body-A");
        }

        [Fact]
        public void onshape_inspect_faces_DelegatesParsedAddressAndOptionalExactBodyFilter()
        {
            var mock = new MockOnshapeProvider();
            var tools = new OnshapeAtomicTools(mock);

            var result = tools.onshape_inspect_faces(SampleAddress(), "Body-B");

            result["success"].Should().Be(true);
            result["operation"].Should().Be("inspect_faces");
            var call = mock.CallLog.Should().ContainSingle(c => c.Method == "InspectFaces").Subject;
            call.Args["address"].Should().Be(SampleAddress());
            call.Args["bodyId"].Should().Be("Body-B");
        }

        [Fact]
        public void onshape_inspect_faces_WhenProviderRejects_ReturnsError()
        {
            var tools = new OnshapeAtomicTools(MockOnshapeProvider.WithError("body not found"));

            var result = tools.onshape_inspect_faces(SampleAddress(), "missing");

            result["success"].Should().Be(false);
            result["error"].Should().Be("body not found");
        }

        [Fact]
        public void onshape_asm_list_instances_Calls_AssemblyListInstances_AndReturnsEmptyInstances()
    {
        var mock = new MockOnshapeProvider();
        var tools = new OnshapeAtomicTools(mock);

        var result = tools.onshape_asm_list_instances(SampleAddress());

        result["success"].Should().Be(true);
        result["instances"].Should().BeOfType<object[]>().Which.Should().BeEmpty();
        mock.CallLog.Should().ContainSingle(c => c.Method == "AssemblyListInstances");
    }

    [Fact]
    public void onshape_asm_create_instance_Calls_AssemblyCreateInstance_WithPartReferences()
    {
        var mock = new MockOnshapeProvider();
        var tools = new OnshapeAtomicTools(mock);

        var result = tools.onshape_asm_create_instance(SampleAddress(), "part-doc-1", "part-element-1");

        result["success"].Should().Be(true);
        var call = mock.CallLog.Should().ContainSingle(c => c.Method == "AssemblyCreateInstance").Subject;
        call.Args["partDocumentId"].Should().Be("part-doc-1");
        call.Args["partElementId"].Should().Be("part-element-1");
    }

    [Fact]
    public void onshape_asm_delete_instance_Calls_AssemblyDeleteInstance()
    {
        var mock = new MockOnshapeProvider();
        var tools = new OnshapeAtomicTools(mock);

        var result = tools.onshape_asm_delete_instance(SampleAddress(), "instance-1");

        result["success"].Should().Be(true);
        var call = mock.CallLog.Should().ContainSingle(c => c.Method == "AssemblyDeleteInstance").Subject;
        call.Args["instanceId"].Should().Be("instance-1");
    }

    [Fact]
    public void onshape_asm_delete_instance_WhenProviderRejectsUnknownInstance_ReturnsError()
    {
        var tools = new OnshapeAtomicTools(MockOnshapeProvider.WithError("Instance does not exist."));

        var result = tools.onshape_asm_delete_instance(SampleAddress(), "missing-instance");

        result["success"].Should().Be(false);
        result["error"].Should().Be("Instance does not exist.");
    }
}
