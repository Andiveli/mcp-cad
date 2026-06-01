namespace McpCad.Tests.Integration;

/// <summary>
/// Integration tests for feature operations against a real Inventor instance.
/// These tests require Inventor and are skipped in CI.
/// </summary>
public class FeatureIntegrationTests
{
    [Fact(Skip = "Requires Inventor")]
    public void Extrude_Profile_CreatesSolid()
    {
        // TODO: Create sketch, draw rectangle, extrude to 5cm
        // Verify result contains "feature" key
    }

    [Fact(Skip = "Requires Inventor")]
    public void Revolve_WithAxisTag_CreatesRevolution()
    {
        // TODO: Create sketch, draw circle, tag axis line, revolve
    }

    [Fact(Skip = "Requires Inventor")]
    public void Fillet_Edges_CreatesConstantRadiusFillet()
    {
        // TODO: Create box, fillet edge 1 with radius 0.5
    }

    [Fact(Skip = "Requires Inventor")]
    public void Hole_AtPosition_CreatesDrilledHole()
    {
        // TODO: Create box, add hole at center
    }
}