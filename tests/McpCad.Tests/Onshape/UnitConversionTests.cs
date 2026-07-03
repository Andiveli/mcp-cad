using McpCad.Onshape;

namespace McpCad.Tests.Onshape;

public class UnitConversionTests
{
    [Fact]
    public void CmToMeters_5cm_Returns0_05()
    {
        OnshapeUnitConversion.CmToMeters(5.0).Should().Be(0.05);
    }

    [Fact]
    public void MetersToCm_0_030m_Returns3_0cm()
    {
        OnshapeUnitConversion.MetersToCm(0.030).Should().Be(3.0);
    }

    [Fact]
    public void G9_RoundTrip_PreservesHostPrecision()
    {
        var originalCm = 12.3456789;
        var meters = OnshapeUnitConversion.CmToMeters(originalCm);
        var backCm = OnshapeUnitConversion.MetersToCm(meters);
        backCm.Should().BeApproximately(originalCm, 1e-6);
    }
}