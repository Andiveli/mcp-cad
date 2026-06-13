using McpCad.SolidWorks;
using McpCad.SolidWorks.Managers;
using McpCad.Core.Exceptions;

namespace McpCad.Tests.SolidWorks;

/// <summary>
/// Unit/structural tests for SketchManager (SolidWorks provider).
/// These require the SolidWorks interop to be present at build time (conditional via csproj).
/// Full execution requires a running SolidWorks instance (live COM); otherwise the test is skipped.
/// Written FIRST per strict TDD for CRITICAL 2 (SketchProfiles fake "1" entry).
/// </summary>
public class SketchManagerTests
{
    [Fact(Skip = "Requires live SolidWorks instance (COM). Run manually after 'Cad:Provider=SolidWorks' or with driver.Connect succeeding. Exercises the profiles contract: count==0 and empty list when no entities drawn yet.")]
    public void SketchProfiles_AfterSketchCreate_BeforeAnyEntities_ReturnsCountZero_AndEmptyProfilesList()
    {
        var driver = new SolidWorksDriver();
        bool connected = false;
        try
        {
            driver.Connect();
            connected = true;
        }
        catch (Exception ex) when (ex is InventorConnectionException || ex is InventorComException || ex is InvalidOperationException || ex is System.Runtime.InteropServices.COMException)
        {
            // No live SW or cannot connect in this environment — structural contract test cannot run.
            // The test documents the expected behavior per CRITICAL 2.
            // On machines with SW running + interop present, remove Skip or run explicitly.
            return;
        }

        if (!connected)
        {
            return;
        }

        var docMgr = new DocumentManager(driver);
        var sketchMgr = new SketchManager(driver);

        try
        {
            // Arrange: fresh part + sketch create, deliberately draw ZERO entities
            var docRes = docMgr.DocNewPart();
            Assert.True((bool)docRes["success"]!, "DocNewPart should succeed to reach sketch phase");

            var sketchRes = sketchMgr.SketchCreate("XY");
            Assert.True((bool)sketchRes["success"]!, "SketchCreate should succeed");

            // Act: call profiles immediately — no lines, circles, or closed contours yet
            var profilesRes = sketchMgr.SketchProfiles();

            // Assert per CRITICAL 2 contract: must NOT fabricate a fake "1" entry.
            // When segments.Length == 0, success remains true but count==0 and profiles is empty list.
            Assert.True((bool)profilesRes["success"]!, "SketchProfiles should still report success=true even with zero profiles");
            Assert.Equal(0, Convert.ToInt32(profilesRes["count"]));
            var profileList = profilesRes["profiles"] as System.Collections.IList;
            Assert.NotNull(profileList);
            Assert.Empty(profileList);
        }
        finally
        {
            // Best-effort cleanup (ignore errors in test teardown)
            try { driver.Disconnect(); } catch { }
        }
    }
}
