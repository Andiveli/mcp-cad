using McpCad.Core.Exceptions;
using McpCad.Tools;
using ModelContextProtocol.Server;
using System.ComponentModel;

namespace McpCad.Tests.Tools;

public class ToolRegistrationCollisionTests
{
    [Fact]
    public void EnsureNoCollisions_Throws_When_create_sketch_Duplicated()
    {
        var ex = Assert.Throws<ToolRegistrationException>(() =>
            ToolRegistrationValidator.EnsureNoCollisions(
                typeof(DuplicateCreateSketchProviderA),
                typeof(DuplicateCreateSketchProviderB)));

        Assert.Equal("create_sketch", ex.ToolName);
        Assert.Equal(2, ex.ProviderNames.Count);
        Assert.Contains("create_sketch", ex.Message);
        Assert.Contains("DuplicateCreateSketchProviderA", ex.Message);
        Assert.Contains("DuplicateCreateSketchProviderB", ex.Message);
    }

#if MCP_CAD_WINDOWS
    [Fact]
    public void Inventor_And_Onshape_Tools_Do_Not_Collide_When_Registered_Separately()
    {
        ToolRegistrationValidator.EnsureNoCollisions(typeof(AtomicTools), typeof(SkillTools));
        ToolRegistrationValidator.EnsureNoCollisions(
            typeof(McpCad.Tools.Onshape.OnshapeAtomicTools),
            typeof(McpCad.Tools.Onshape.OnshapeSkillTools));
    }

    [Fact]
    public void AtomicTools_And_OnshapeAtomicTools_Register_Together_Without_Collision()
    {
        var exception = Record.Exception(() =>
            ToolRegistrationValidator.EnsureNoCollisions(
                typeof(AtomicTools),
                typeof(McpCad.Tools.Onshape.OnshapeAtomicTools)));

        Assert.Null(exception);
    }
#endif

    [Fact]
    public void GetToolMethodNames_OnshapeAtomicTools_Uses_Onshape_Export_Prefix_Not_Bare_Export_On_Onshape_Type()
    {
        var names = ToolRegistrationValidator.GetToolMethodNames(typeof(McpCad.Tools.Onshape.OnshapeAtomicTools));

        Assert.Contains("onshape_export_step", names);
        Assert.Contains("onshape_export_stl", names);
        Assert.Contains("onshape_sketch_rectangle", names);
        Assert.Contains("onshape_extrude", names);
        Assert.Contains("onshape_feature_list", names);
        Assert.DoesNotContain("export_step", names);
        Assert.DoesNotContain("export_stl", names);
    }

    [McpServerToolType]
    private sealed class DuplicateCreateSketchProviderA
    {
        [McpServerTool, Description("a")]
        public Dictionary<string, object?> create_sketch() => new();
    }

    [McpServerToolType]
    private sealed class DuplicateCreateSketchProviderB
    {
        [McpServerTool, Description("b")]
        public Dictionary<string, object?> create_sketch() => new();
    }
}