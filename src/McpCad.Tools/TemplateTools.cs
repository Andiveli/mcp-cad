using McpCad.Core;
using ModelContextProtocol.Server;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text.Json;

namespace McpCad.Tools;

/// <summary>
/// Template tools: capture current sketch+params as reusable JSON template,
/// list/delete, and run (substitute + delegate to macro_god_part).
/// Follows the template-system design (engram #269) and existing tool patterns (MacroTools/AtomicTools).
/// </summary>
[McpServerToolType]
public class TemplateTools(IMechanicalCadProvider provider, MacroTools macroTools)
{
    private readonly IMechanicalCadProvider _provider = provider;
    private readonly MacroTools _macro = macroTools;

    private static Dictionary<string, object?> Catch(Func<Dictionary<string, object?>> action)
    {
        try { return action(); }
        catch (Exception ex) { return ToolHelpers.Error(ex.Message); }
    }

    // ── template_capture ───────────────────────────────────────────────

    [McpServerTool, Description("Capture the active sketch (via ReadSketchData) + current parameters as a named reusable template JSON compatible with macro_god_part. The template stores entities, a parameters section with defaults, and a macro_config that can contain ${PARAM} placeholders for later substitution in template_run. Use for templating common parts.")]
    public Dictionary<string, object?> template_capture(
        [Description("Unique name for the template (used as filename).")] string name,
        [Description("Optional human description stored in the template.")] string? description = null)
    {
        return Catch(() =>
        {
            if (string.IsNullOrWhiteSpace(name))
                return ToolHelpers.Error("Template name is required.");

            var health = _provider.Health();
            var sketchData = _provider.ReadSketchData(1);
            var paramData = _provider.ParamList();

            // PR3: capture full part — features + tree (in addition to sketch for v1 compat)
            var featureData = _provider.ReadFeatureData();
            var featureTree = _provider.GetFeatureTree();

            var featuresList = featureData.TryGetValue("features", out var feats) && feats is System.Collections.IList fList
                ? fList
                : new List<object>();

            var featureWarnings = featureData.TryGetValue("warnings", out var fw) && fw is System.Collections.IList wList
                ? wList
                : new List<string>();

            var template = new Dictionary<string, object?>
            {
                ["name"] = name,
                ["description"] = description ?? $"Captured template from active document at {DateTime.UtcNow:O}",
                ["parameters"] = paramData.TryGetValue("parameters", out var pars) ? pars : new List<object>(),
                ["macro_config"] = new Dictionary<string, object?>
                {
                    ["plane"] = "YZ",
                    // Keep sketch_entities for immediate god compat + introduce sketches[] for future multi-sketch
                    ["sketch_entities"] = sketchData.TryGetValue("entities", out var ents) ? ents : null,
                    ["sketches"] = new List<object?>
                    {
                        new Dictionary<string, object?>
                        {
                            ["sketch_index"] = 1,
                            ["entities"] = sketchData.TryGetValue("entities", out var e2) ? e2 : null
                        }
                    },
                    ["features"] = featuresList,
                    ["verify"] = true
                    // Other god fields (constraints, feature_*, etc.) can be added/edited by user in the saved JSON
                },
                ["metadata"] = new Dictionary<string, object?>
                {
                    ["captured_at"] = DateTime.UtcNow.ToString("O"),
                    ["success_sketch"] = sketchData.TryGetValue("success", out var s) && s is bool sb && sb,
                    ["feature_count"] = featuresList is System.Collections.IList fl ? fl.Count : 0,
                    ["feature_reader_warnings"] = featureWarnings
                }
            };

            var jsonEl = JsonSerializer.Deserialize<JsonElement>(JsonSerializer.Serialize(template));
            bool saved = TemplateManager.Save(name, jsonEl);

            if (!saved)
                return ToolHelpers.Error("Failed to save template file.");

            return ToolHelpers.Ok(new Dictionary<string, object?>
            {
                ["template"] = name,
                ["path"] = System.IO.Path.Combine(TemplateManager.TemplateDir, name + ".json"),
                ["entities_count"] = (sketchData.TryGetValue("entities", out var e) && e is System.Collections.IList l) ? l.Count : 0,
                ["feature_count"] = featuresList is System.Collections.IList fl2 ? fl2.Count : 0,
                ["feature_reader_warnings"] = featureWarnings
            });
        });
    }

    // ── template_list ──────────────────────────────────────────────────

    [McpServerTool, Description("List all available saved templates (names only). Templates live as .json under ./templates/.")]
    public Dictionary<string, object?> template_list()
    {
        return Catch(() =>
        {
            var names = TemplateManager.List();
            return ToolHelpers.Ok(new Dictionary<string, object?>
            {
                ["templates"] = names,
                ["count"] = names.Length,
                ["dir"] = TemplateManager.TemplateDir
            });
        });
    }

    // ── template_run ───────────────────────────────────────────────────

    [McpServerTool, Description("Load a saved template by name, apply parameter overrides (e.g. overrides JSON '{\"OD\":60,\"H\":70}' or individual via substitution), then execute by calling macro_god_part with the resolved macro_config. This is the primary way to instantiate a captured template.")]
    public Dictionary<string, object?> template_run(
        [Description("Name of the previously captured template.")] string name,
        [Description("Optional JSON object string with override values for ${PARAM} placeholders, e.g. '{\"OD\": 60.0, \"H\": 75}'. Overrides take precedence over template defaults.")] string? overrides = null)
    {
        return Catch(() =>
        {
            if (string.IsNullOrWhiteSpace(name))
                return ToolHelpers.Error("Template name is required.");

            var tpl = TemplateManager.Load(name);
            if (tpl is null || tpl.Value.ValueKind == JsonValueKind.Null)
                return ToolHelpers.Error($"Template '{name}' not found.");

            // Parse overrides if provided
            Dictionary<string, object?> ov = new();
            if (!string.IsNullOrWhiteSpace(overrides))
            {
                try
                {
                    var ovEl = JsonSerializer.Deserialize<JsonElement>(overrides);
                    if (ovEl.ValueKind == JsonValueKind.Object)
                    {
                        foreach (var p in ovEl.EnumerateObject())
                            ov[p.Name] = p.Value.ValueKind == JsonValueKind.Number ? p.Value.GetDouble() : p.Value.ToString();
                    }
                }
                catch (Exception ex)
                {
                    return ToolHelpers.Error($"Invalid overrides JSON: {ex.Message}");
                }
            }

            // Extract defaults from template "parameters" if present (simple key->default)
            Dictionary<string, object?> defs = new();
            if (tpl.Value.TryGetProperty("parameters", out var paramsEl) && paramsEl.ValueKind == JsonValueKind.Object)
            {
                foreach (var p in paramsEl.EnumerateObject())
                {
                    if (p.Value.TryGetProperty("default", out var defEl))
                        defs[p.Name] = defEl.ValueKind == JsonValueKind.Number ? defEl.GetDouble() : defEl.ToString();
                }
            }

            // Get the macro_config section and substitute
            if (!tpl.Value.TryGetProperty("macro_config", out var macroCfgEl))
                macroCfgEl = tpl.Value; // fallback if flat

            var substituted = TemplateManager.Substitute(macroCfgEl, ov, defs);

            // Map substituted config back to macro_god_part call args (god expects string? JSON for sketch_* blocks)
            string? GetStr(string prop) => substituted.TryGetProperty(prop, out var v) && v.ValueKind != JsonValueKind.Null ? v.ToString() : null;
            double? GetDbl(string prop) => substituted.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetDouble() : null;
            string? GetStrFromObj(string prop) => substituted.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

            // Call the real god macro with resolved values (this connects template to god)
            // PR3: forward features[] when present in the (substituted) macro_config.
            // Backward compat: if absent, god falls back to legacy single-feature path automatically.
            // Substitute already handled ${PARAM} in feature values.
            var result = _macro.macro_god_part(
                ask_before_modify: true,
                plane: GetStr("plane") ?? "YZ",
                force_new: null,
                sketch_entities: GetStr("sketch_entities"),
                sketch_constraints: GetStr("sketch_constraints"),
                sketch_dimensions: GetStr("sketch_dimensions"),
                sketch_modify: GetStr("sketch_modify"),
                sketch_pattern: GetStr("sketch_pattern"),
                feature_type: GetStr("feature_type"),
                feature_profile: GetStr("feature_profile"),
                feature_distance: GetDbl("feature_distance"),
                feature_axis: GetStr("feature_axis"),
                feature_path: GetStr("feature_path"),
                feature_profiles: GetStr("feature_profiles"),
                feature_pitch: GetDbl("feature_pitch"),
                features: GetStr("features"),   // PR3: the key addition for full-part replay
                part_number: GetStrFromObj("part_number") ?? GetStr("part_number"),
                description: GetStrFromObj("description") ?? GetStr("description"),
                material: GetStrFromObj("material") ?? GetStr("material")
            );

            // Always attach template provenance (connection evidence) even on god errors/confirmation
            result["template_used"] = name;
            result["overrides_applied"] = ov.Count > 0 ? ov : null;

            return result;
        });
    }

    // ── template_delete ────────────────────────────────────────────────

    [McpServerTool, Description("Delete a saved template by name. Irreversible.")]
    public Dictionary<string, object?> template_delete(
        [Description("Name of the template to remove.")] string name)
    {
        return Catch(() =>
        {
            if (string.IsNullOrWhiteSpace(name))
                return ToolHelpers.Error("Template name is required.");

            bool deleted = TemplateManager.Delete(name);
            return deleted
                ? ToolHelpers.Ok(new Dictionary<string, object?> { ["deleted"] = name })
                : ToolHelpers.Error($"Template '{name}' not found or could not be deleted.");
        });
    }
}
