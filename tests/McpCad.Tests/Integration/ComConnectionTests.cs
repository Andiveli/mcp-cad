namespace McpCad.Tests.Integration;

/// <summary>
/// Integration tests for COM connection to Inventor.
/// These tests require a running Inventor instance and are
/// skipped in CI environments.
/// </summary>
public class ComConnectionTests
{
    [Fact(Skip = "Requires Inventor")]
    public void Connect_ToRunningInventor_ReturnsSuccess()
    {
        // TODO: Test real Inventor COM connection
        // var driver = new InventorDriver();
        // var result = driver.Connect();
        // Assert.True((bool)result["success"]!);
    }

    [Fact(Skip = "Requires Inventor")]
    public void Health_WithActiveDocument_ReturnsVersion()
    {
        // TODO: Test health check with active document
    }

    [Fact(Skip = "Requires Inventor")]
    public void Disconnect_ReleasesCOM_Resources()
    {
        // TODO: Test that disconnect releases COM objects
    }
}