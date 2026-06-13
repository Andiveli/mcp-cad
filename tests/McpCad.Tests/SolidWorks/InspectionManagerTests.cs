using McpCad.Core;
using McpCad.Core.Exceptions;
using McpCad.SolidWorks;
using McpCad.SolidWorks.Managers;
using McpCad.SolidWorks.Helpers;

namespace McpCad.Tests.SolidWorks;

/// <summary>
/// Inspection manager structural tests (SolidWorks provider).
/// Minimal safety net for GetFeatureTree / Capture / BBox paths when no live SW.
/// </summary>
public class InspectionManagerTests
{
    [Fact]
    public void GetFeatureTree_WithNoActiveDoc_ThrowsInventorConnectionException()
    {
        var driver = new SolidWorksDriver(); // not connected
        var inspect = new InspectionManager(driver);

        var ex = Assert.Throws<InventorConnectionException>(() => inspect.GetFeatureTree());
        Assert.Contains("No active document", ex.Message);
    }
}
