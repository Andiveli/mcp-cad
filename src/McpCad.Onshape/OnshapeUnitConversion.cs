using System.Globalization;

namespace McpCad.Onshape;

/// <summary>
/// Host uses cm; Onshape REST uses meters. Single conversion site per design.
/// </summary>
public static class OnshapeUnitConversion
{
    private const string Format = "G9";

    public static double CmToMeters(double centimeters) =>
        double.Parse((centimeters / 100.0).ToString(Format, CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);

    public static double MetersToCm(double meters) =>
        double.Parse((meters * 100.0).ToString(Format, CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);
}