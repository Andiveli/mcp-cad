using McpCad.Core;
using McpCad.Core.Exceptions;
using McpCad.Core.Models;
using ModelContextProtocol.Server;
using System.ComponentModel;

namespace McpCad.Tools.Onshape;

/// <summary>
/// Composed Onshape MCP skills (multi-step cloud workflows).
/// </summary>
[McpServerToolType]
public class OnshapeSkillTools(ICadWebProvider provider)
{
    [McpServerTool, Description("Sketch then add extrude on a Part Studio (composed skill).")]
    public Dictionary<string, object?> skill_sketch_and_extrude(
        string address_json,
        double distance_cm,
        string direction = "positive")
    {
        return Catch(() =>
        {
            var address = CadElementAddress.Deserialize(address_json);
            var sketch = provider.SketchReplace(address);
            if (sketch.TryGetValue("success", out var ok) && ok is false)
            {
                return sketch;
            }

            return provider.FeatureAdd(
                address,
                "extrude",
                new[]
                {
                    new Dictionary<string, object?>
                    {
                        ["distanceCm"] = distance_cm,
                        ["direction"] = direction,
                    },
                });
        });
    }

    [McpServerTool, Description("Create Part Studio element then export STEP.")]
    public Dictionary<string, object?> skill_create_and_export_step(
        string parent_address_json,
        string part_studio_name,
        string export_address_json)
    {
        return Catch(() =>
        {
            var parent = CadElementAddress.Deserialize(parent_address_json);
            var created = provider.ElementCreate(parent, part_studio_name, "PARTSTUDIO");
            if (created.TryGetValue("success", out var ok) && ok is false)
            {
                return created;
            }

            return provider.ExportStep(CadElementAddress.Deserialize(export_address_json));
        });
    }

    private static Dictionary<string, object?> Catch(Func<Dictionary<string, object?>> action)
    {
        try
        {
            return action();
        }
        catch (CadProviderException ex)
        {
            return ToolHelpers.Error(ex.Message);
        }
        catch (Exception ex)
        {
            return ToolHelpers.Error($"Unexpected error: {ex.Message}");
        }
    }
}