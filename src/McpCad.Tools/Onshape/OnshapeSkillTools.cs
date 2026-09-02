using System.ComponentModel;
using System.Text.Json;
using McpCad.Core;
using McpCad.Core.Exceptions;
using McpCad.Core.Models;
using McpCad.Onshape;
using McpCad.Onshape.Builders;
using ModelContextProtocol.Server;

namespace McpCad.Tools.Onshape;

/// <summary>
/// Composed Onshape MCP skills (multi-step cloud workflows).
/// </summary>
[McpServerToolType]
public partial class OnshapeSkillTools(ICadWebProvider provider)
{
    [McpServerTool, Description("Create a rectangle sketch and extrude it in one bounded Part Studio call. Coordinates and depth are in cm.")]
    public Dictionary<string, object?> skill_sketch_and_extrude(
        string address_json,
        double x1_cm,
        double y1_cm,
        double x2_cm,
        double y2_cm,
        double depth_cm,
        string? plane = null,
        string direction = "positive",
        string boolean_op = "new")
    {
        return Catch(() =>
        {
            var address = CadElementAddress.Deserialize(address_json);
            using var sketchPayload = OnshapeSketchBuilder.BuildRectangle(
                OnshapeUnitConversion.CmToMeters(x1_cm),
                OnshapeUnitConversion.CmToMeters(y1_cm),
                OnshapeUnitConversion.CmToMeters(x2_cm),
                OnshapeUnitConversion.CmToMeters(y2_cm),
                plane);
            var sketchRequest = JsonSerializer.Deserialize<Dictionary<string, object?>>(sketchPayload.RootElement.GetRawText())
                                ?? new Dictionary<string, object?>();
            var sketchResult = provider.SketchReplace(address, sketchRequest);
            if (!IsSuccessful(sketchResult) || !TryGetFeatureId(sketchResult, out var sketchFeatureId))
            {
                var error = IsSuccessful(sketchResult)
                    ? "Onshape sketch creation did not return a usable feature ID."
                    : GetError(sketchResult, "Onshape sketch creation failed.");
                return SkillResult(sketchResult, null, error);
            }

            using var extrudePayload = OnshapeFeatureTemplates.ExtrudeFromSketch(
                sketchFeatureId,
                OnshapeUnitConversion.CmToMeters(depth_cm),
                direction,
                boolean_op);
            var extrudeResult = provider.FeatureAdd(address, "extrude", null, extrudePayload.RootElement.GetRawText());
            return SkillResult(sketchResult, extrudeResult, null, sketchFeatureId);
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

    private static Dictionary<string, object?> SkillResult(
        Dictionary<string, object?> sketchResult,
        Dictionary<string, object?>? extrudeResult,
        string? error,
        string? sketchFeatureId = null)
    {
        var sketch = new Dictionary<string, object?>
        {
            ["success"] = IsSuccessful(sketchResult),
            ["featureId"] = sketchFeatureId,
            ["result"] = sketchResult,
        };
        var extrude = extrudeResult is null
            ? new Dictionary<string, object?>
            {
                ["success"] = false,
                ["featureId"] = null,
                ["skipped"] = true,
            }
            : new Dictionary<string, object?>
            {
                ["success"] = IsSuccessful(extrudeResult),
                ["featureId"] = TryGetFeatureId(extrudeResult, out var extrudeFeatureId) ? extrudeFeatureId : null,
                ["result"] = extrudeResult,
            };

        var result = new Dictionary<string, object?>
        {
            ["success"] = extrudeResult is not null && IsSuccessful(extrudeResult),
            ["sketch"] = sketch,
            ["extrude"] = extrude,
        };
        if (error is not null)
        {
            result["error"] = error;
        }

        return result;
    }

    private static bool IsSuccessful(IReadOnlyDictionary<string, object?> result) =>
        result.TryGetValue("success", out var success) && success is true;

    private static bool TryGetFeatureId(IReadOnlyDictionary<string, object?> result, out string featureId)
    {
        if (result.TryGetValue("featureId", out var value) && value is string topLevelFeatureId && !string.IsNullOrWhiteSpace(topLevelFeatureId))
        {
            featureId = topLevelFeatureId;
            return true;
        }

        if (result.TryGetValue("feature", out var feature) &&
            feature is IReadOnlyDictionary<string, object?> nestedFeature &&
            nestedFeature.TryGetValue("featureId", out var nestedValue) &&
            nestedValue is string nestedFeatureId &&
            !string.IsNullOrWhiteSpace(nestedFeatureId))
        {
            featureId = nestedFeatureId;
            return true;
        }

        featureId = string.Empty;
        return false;
    }

    private static string GetError(IReadOnlyDictionary<string, object?> result, string fallback) =>
        result.TryGetValue("error", out var error) && error is string message && !string.IsNullOrWhiteSpace(message)
            ? message
            : fallback;

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