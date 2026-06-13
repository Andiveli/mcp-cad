using McpCad.SolidWorks;
using McpCad.SolidWorks.Managers;
using McpCad.Core.Exceptions;

namespace McpCad.Tests.SolidWorks;

/// <summary>
/// Structural tests for DocumentManager error paths (SolidWorks provider).
/// Written per CRITICAL 4 to exercise empty-catch → meaningful InventorComException propagation.
/// These compile only when SW interop is present (csproj condition); execution does not require live SW for the null-App case.
/// </summary>
public class DocumentManagerTests
{
    [Fact]
    public void DocNewPart_WithNullApp_ThrowsInventorComException()
    {
        // Arrange: fresh driver (never Connect()ed) → SwApp is null
        var driver = new SolidWorksDriver();
        var docMgr = new DocumentManager(driver);

        // Act + Assert
        var ex = Assert.Throws<InventorComException>(() => docMgr.DocNewPart());
        Assert.Contains("Failed to create part document", ex.Message);
        // The inner cause is either NRE on App.Documents or the explicit "Documents.Add failed" path,
        // both wrapped as InventorComException per the fixed error handling (no silent empty catch).
    }

    [Fact]
    public void DocNewAssembly_WithNullApp_ThrowsInventorComException()
    {
        var driver = new SolidWorksDriver();
        var docMgr = new DocumentManager(driver);

        var ex = Assert.Throws<InventorComException>(() => docMgr.DocNewAssembly());
        Assert.Contains("Failed to create assembly document", ex.Message);
    }
}
