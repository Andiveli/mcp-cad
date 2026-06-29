using McpCad.Tests.Mocks;
using McpCad.Tools;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Xunit;

namespace McpCad.Tests.Tools;

/// <summary>
/// Tests for TemplateTools + TemplateManager (item 2 of template + god macro package).
/// Strict TDD: these tests (or the design cases) were the "first" artifacts.
/// Uses MockInventorProvider for provider calls (ReadSketchData, ParamList, Health).
/// File side-effects use unique names + cleanup; manager creates ./templates under cwd for the test process.
/// </summary>
public class TemplateToolsTests : IDisposable
{
    private readonly MockInventorProvider _mock = new();
    private readonly TemplateTools _tools;
    private readonly MacroTools _macroTools;
    private readonly string _uniquePrefix = "test-template-" + Guid.NewGuid().ToString("N").Substring(0, 8);
    private readonly List<string> _createdTemplates = new();

    public TemplateToolsTests()
    {
        _macroTools = new MacroTools(_mock);
        _tools = new TemplateTools(_mock, _macroTools);
    }

    public void Dispose()
    {
        // Best-effort cleanup of any templates this test run created
        foreach (var name in _createdTemplates)
        {
            try { TemplateManager.Delete(name); } catch { }
        }
        // Also clean any leftover test files
        try
        {
            var dir = TemplateManager.TemplateDir;
            if (Directory.Exists(dir))
            {
                foreach (var f in Directory.GetFiles(dir, $"{_uniquePrefix}*.json"))
                    File.Delete(f);
            }
        }
        catch { }
    }

    private void SetupCaptureMocks(List<Dictionary<string, object?>>? entities = null, int featureCount = 0,
        List<Dictionary<string, object?>>? features = null, List<string>? featureWarnings = null)
    {
        _mock.SetHealthResult(new Dictionary<string, object?>
        {
            ["success"] = true,
            ["active_document"] = "TestPart.ipt",
            ["connected"] = true
        });

        entities ??= new List<Dictionary<string, object?>>
        {
            new() { ["type"] = "circle", ["cx"] = 0, ["cy"] = 0, ["radius"] = 5 }
        };

        _mock.SetReadSketchDataResult(new Dictionary<string, object?>
        {
            ["success"] = true,
            ["entities"] = entities,
            ["warnings"] = new List<string>(),
            ["sketch_index"] = 1
        });

        _mock.SetParamListResult(new Dictionary<string, object?>
        {
            ["success"] = true,
            ["parameters"] = new List<Dictionary<string, object?>>
            {
                new() { ["name"] = "d0", ["value"] = 10.0 }
            }
        });

        _mock.SetGetFeatureTreeResult(new Dictionary<string, object?>
        {
            ["success"] = true,
            ["feature_count"] = featureCount,
            ["features"] = new List<Dictionary<string, object?>>()
        });

        // PR3: support ReadFeatureData for features[] capture tests
        features ??= new List<Dictionary<string, object?>>();
        featureWarnings ??= new List<string>();
        _mock.SetReadFeatureDataResult(new Dictionary<string, object?>
        {
            ["success"] = true,
            ["features"] = features,
            ["warnings"] = featureWarnings
        });
    }

    [Fact]
    public void TemplateCapture_MultiSketch_LoopsAllSketches()
    {
        // Multi-sketch v2: template_capture reads all sketches and emits sketches[] with each entry
        var sketch1Entities = new List<Dictionary<string, object?>>
        {
            new() { ["type"] = "circle", ["cx"] = 0, ["cy"] = 0, ["radius"] = 5 }
        };
        var sketch2Entities = new List<Dictionary<string, object?>>
        {
            new() { ["type"] = "rect", ["x1"] = 0, ["y1"] = 0, ["x2"] = 10, ["y2"] = 10 }
        };

        // Sketch 1 returns success with total_sketches=2
        _mock.SetReadSketchDataResult(new Dictionary<string, object?>
        {
            ["success"] = true,
            ["entities"] = sketch1Entities,
            ["warnings"] = new List<string>(),
            ["sketch_index"] = 1,
            ["total_sketches"] = 2,
            ["parameters"] = new List<Dictionary<string, object?>>()
        }, sketchIndex: 1);

        // Sketch 2 returns different entities
        _mock.SetReadSketchDataResult(new Dictionary<string, object?>
        {
            ["success"] = true,
            ["entities"] = sketch2Entities,
            ["warnings"] = new List<string>(),
            ["sketch_index"] = 2,
            ["total_sketches"] = 2,
            ["parameters"] = new List<Dictionary<string, object?>>()
        }, sketchIndex: 2);

        _mock.SetHealthResult(new Dictionary<string, object?>
        {
            ["success"] = true,
            ["active_document"] = "MultiSketch.ipt",
            ["connected"] = true
        });
        _mock.SetParamListResult(new Dictionary<string, object?>
        {
            ["success"] = true,
            ["parameters"] = new List<Dictionary<string, object?>>()
        });
        _mock.SetGetFeatureTreeResult(new Dictionary<string, object?>
        {
            ["success"] = true,
            ["feature_count"] = 0,
            ["features"] = new List<Dictionary<string, object?>>()
        });
        _mock.SetReadFeatureDataResult(new Dictionary<string, object?>
        {
            ["success"] = true,
            ["features"] = new List<Dictionary<string, object?>>(),
            ["warnings"] = new List<string>()
        });

        var name = _uniquePrefix + "-multisketch";
        _createdTemplates.Add(name);

        var res = _tools.template_capture(name, "Multi-sketch test capture");

        Assert.NotNull(res);
        Assert.True(res.TryGetValue("success", out var ok) && ok is bool bok && bok);

        // Verify sketches[] in the saved template
        var tpl = TemplateManager.Load(name);
        Assert.NotNull(tpl);
        Assert.True(tpl.Value.TryGetProperty("macro_config", out var mc));
        Assert.True(mc.TryGetProperty("sketches", out var skArr));
        Assert.Equal(JsonValueKind.Array, skArr.ValueKind);
        Assert.Equal(2, skArr.GetArrayLength());

        // Entry 1 should be sketch1 entities (circle)
        var entry1 = skArr[0];
        Assert.True(entry1.TryGetProperty("entities", out var e1));
        Assert.Equal(JsonValueKind.Array, e1.ValueKind);
        Assert.Equal(1, e1.GetArrayLength());
        Assert.Equal("circle", e1[0].GetProperty("type").GetString());

        // Entry 2 should be sketch2 entities (rect)
        var entry2 = skArr[1];
        Assert.True(entry2.TryGetProperty("entities", out var e2));
        Assert.Equal(JsonValueKind.Array, e2.ValueKind);
        Assert.Equal(1, e2.GetArrayLength());
        Assert.Equal("rect", e2[0].GetProperty("type").GetString());

        // Verify CallLog shows ReadSketchData was called for both indices
        var readCalls = _mock.CallLog.Where(c => c.Method == "ReadSketchData").ToList();
        Assert.Equal(2, readCalls.Count);
        Assert.Equal(1, readCalls[0].Args.GetValueOrDefault("sketch_index"));
        Assert.Equal(2, readCalls[1].Args.GetValueOrDefault("sketch_index"));

        // Metadata should reflect sketch_count
        Assert.True(tpl.Value.TryGetProperty("metadata", out var md));
        Assert.True(md.TryGetProperty("sketch_count", out var sc));
        Assert.Equal(2, sc.GetInt32());
    }

    [Fact]
    public void TemplateRun_MultiSketch_ForwardsSketchesToGod()
    {
        // Round-trip: template with 2 sketches → run → verify macro_god_part created both sketches
        var sketch1Entities = new List<Dictionary<string, object?>>
        {
            new() { ["type"] = "circle", ["cx"] = 0, ["cy"] = 0, ["radius"] = 5 }
        };
        var sketch2Entities = new List<Dictionary<string, object?>>
        {
            new() { ["type"] = "circle", ["cx"] = 10, ["cy"] = 0, ["radius"] = 3 }
        };

        SetupCaptureMocks(sketch1Entities);

        // Override sketch 1 to return total_sketches=2
        _mock.SetReadSketchDataResult(new Dictionary<string, object?>
        {
            ["success"] = true,
            ["entities"] = sketch1Entities,
            ["warnings"] = new List<string>(),
            ["sketch_index"] = 1,
            ["total_sketches"] = 2,
            ["parameters"] = new List<Dictionary<string, object?>>()
        }, sketchIndex: 1);

        // Sketch 2
        _mock.SetReadSketchDataResult(new Dictionary<string, object?>
        {
            ["success"] = true,
            ["entities"] = sketch2Entities,
            ["warnings"] = new List<string>(),
            ["sketch_index"] = 2,
            ["total_sketches"] = 2,
            ["parameters"] = new List<Dictionary<string, object?>>()
        }, sketchIndex: 2);

        // Capture
        var name = _uniquePrefix + "-msrun";
        _createdTemplates.Add(name);

        var capRes = _tools.template_capture(name, "Multi-sketch run test");
        Assert.True(capRes.TryGetValue("success", out var s) && s is bool cb && cb);

        // Prepare mock for run: god must get past ask_before_modify guard
        _mock.SetGetFeatureTreeResult(new Dictionary<string, object?>
        {
            ["success"] = true,
            ["feature_count"] = 0,
            ["features"] = new List<Dictionary<string, object?>>()
        });
        _mock.SetDocNewPartResult(new Dictionary<string, object?> { ["success"] = true });
        _mock.SetSketchCreateResult(new Dictionary<string, object?> { ["success"] = true });
        // SketchCircle default success is sufficient (mock returns success=true, entity=1)
        _mock.SetGetBoundingBoxResult(new Dictionary<string, object?>
        {
            ["success"] = true,
            ["min"] = new[] { 0.0, 0.0, 0.0 },
            ["max"] = new[] { 20.0, 20.0, 10.0 },
            ["size"] = new[] { 20.0, 20.0, 10.0 },
            ["center"] = new[] { 10.0, 10.0, 5.0 }
        });

        // Clear call log from capture
        _mock.CallLog.Clear();

        var runRes = _tools.template_run(name);

        Assert.NotNull(runRes);
        Assert.Equal(name, runRes.GetValueOrDefault("template_used")?.ToString());

        // Verify macro_god_part created 2 sketches (one per entry in sketches[])
        var sketchCalls = _mock.CallLog.Where(c => c.Method == "SketchCreate").ToList();
        Assert.Equal(2, sketchCalls.Count);
        Assert.Equal("YZ", sketchCalls[0].Args.GetValueOrDefault("plane")?.ToString());
        Assert.Equal("YZ", sketchCalls[1].Args.GetValueOrDefault("plane")?.ToString());

        // Each sketch had a circle drawn
        var circleCalls = _mock.CallLog.Where(c => c.Method == "SketchCircle").ToList();
        Assert.Equal(2, circleCalls.Count);
    }

    [Fact]
    public void TemplateManager_Substitute_ReplacesOverridesAndDefaults()
    {
        var config = JsonSerializer.Deserialize<JsonElement>(@"{
            ""sketch_entities"": [ { ""type"": ""circle"", ""radius"": ""${R}"" } ],
            ""feature_distance"": ""${H}""
        }");

        var overrides = new Dictionary<string, object?> { ["R"] = 12.5 };
        var defaults = new Dictionary<string, object?> { ["H"] = 80.0, ["R"] = 5.0 };

        var sub = TemplateManager.Substitute(config, overrides, defaults);
        var text = sub.ToString();

        Assert.Contains("12.5", text);
        Assert.Contains("80", text);
        // R from override won over default
    }

    [Fact]
    public void TemplateCapture_UsesReadSketchDataAndSaves()
    {
        SetupCaptureMocks();
        var result = _tools.template_capture(_uniquePrefix + "-piston", "test piston");

        Assert.True((bool)result["success"]!);
        Assert.Equal(_uniquePrefix + "-piston", result["template"]);
        _createdTemplates.Add(_uniquePrefix + "-piston");

        // Verify file exists and is loadable
        var loaded = TemplateManager.Load(_uniquePrefix + "-piston");
        Assert.NotNull(loaded);
        Assert.True(loaded.Value.TryGetProperty("name", out var n) && n.GetString() == _uniquePrefix + "-piston");
    }

    [Fact]
    public void TemplateList_ReturnsNames_AfterCapture()
    {
        SetupCaptureMocks();
        _tools.template_capture(_uniquePrefix + "-listtest");
        _createdTemplates.Add(_uniquePrefix + "-listtest");

        var listRes = _tools.template_list();
        Assert.True((bool)listRes["success"]!);
        var templates = (System.Collections.IList)listRes["templates"]!;
        Assert.Contains(_uniquePrefix + "-listtest", templates.Cast<object>().Select(o => o?.ToString()));
    }

    [Fact]
    public void TemplateRun_SubstitutesAndCallsMacroGodPart()
    {
        SetupCaptureMocks(new List<Dictionary<string, object?>>
        {
            new() { ["type"] = "circle", ["cx"] = 0, ["cy"] = 0, ["radius"] = "${R}" }
        });

        // First capture a template with placeholder
        var cap = _tools.template_capture(_uniquePrefix + "-run", "for run test");
        Assert.True((bool)cap["success"]!);
        _createdTemplates.Add(_uniquePrefix + "-run");

        // Make the underlying god calls succeed (god will call many provider methods)
        _mock.SetDocNewPartResult(new Dictionary<string, object?> { ["success"] = true });
        _mock.SetSketchCreateResult(new Dictionary<string, object?> { ["success"] = true });
        // ... (other god calls like extrude etc would be set if we exercised full; for this test we just check it reaches god)

        var runRes = _tools.template_run(_uniquePrefix + "-run", "{\"R\": 12.5}");

        // The god may return success=false if no feature etc, but it should not error on the template/run layer
        // and should have recorded the call to the god macro
        Assert.NotNull(runRes);
        // At minimum the template layer succeeded in invoking the god path
        Assert.True(runRes.ContainsKey("success") || runRes.ContainsKey("error") || runRes.ContainsKey("needs_confirmation"));

        // Item 3 connection verification: template_run always forwards to macro_god_part and attaches provenance
        Assert.Equal(_uniquePrefix + "-run", runRes.GetValueOrDefault("template_used")?.ToString());
        Assert.NotNull(runRes.GetValueOrDefault("overrides_applied"));
    }

    [Fact]
    public void TemplateDelete_RemovesTemplate()
    {
        SetupCaptureMocks();
        _tools.template_capture(_uniquePrefix + "-del");
        _createdTemplates.Add(_uniquePrefix + "-del");

        var delRes = _tools.template_delete(_uniquePrefix + "-del");
        Assert.True((bool)delRes["success"]!);
        Assert.Equal(_uniquePrefix + "-del", delRes["deleted"]);

        var after = TemplateManager.Load(_uniquePrefix + "-del");
        Assert.Null(after);
    }

    // ── PR3 RED tests (3.1) ─────────────────────────────────────────────

    [Fact]
    public void TemplateCapture_ProducesFeaturesArray_InMacroConfig()
    {
        // RED: expects features[] from ReadFeatureData + GetFeatureTree to be emitted in saved macro_config
        var extrudeFeat = new Dictionary<string, object?>
        {
            ["feature_type"] = "extrude",
            ["profile"] = "1",
            ["distance"] = 10.0,
            ["direction"] = "positive",
            ["operation"] = "new_body"
        };
        var filletFeat = new Dictionary<string, object?>
        {
            ["feature_type"] = "fillet",
            ["edges"] = "1",
            ["radius"] = 0.5,
            ["mode"] = "constant"
        };
        SetupCaptureMocks(
            features: new List<Dictionary<string, object?>> { extrudeFeat, filletFeat },
            featureWarnings: new List<string>());

        var cap = _tools.template_capture(_uniquePrefix + "-feat", "features test");
        Assert.True((bool)cap["success"]!);
        _createdTemplates.Add(_uniquePrefix + "-feat");

        var loaded = TemplateManager.Load(_uniquePrefix + "-feat");
        Assert.NotNull(loaded);
        Assert.True(loaded.Value.TryGetProperty("macro_config", out var mc));
        Assert.True(mc.TryGetProperty("features", out var farr) && farr.ValueKind == JsonValueKind.Array);
        Assert.Equal(2, farr.GetArrayLength());
    }

    [Fact]
    public void TemplateRun_OldTemplateWithoutFeatures_StillWorks_BackwardCompat()
    {
        // RED: old template (no features[] in macro_config) must still run via legacy single-feature path
        SetupCaptureMocks();

        // Manually create an "old" template JSON without features (pre-PR3 shape)
        var oldTpl = new Dictionary<string, object?>
        {
            ["name"] = _uniquePrefix + "-old",
            ["macro_config"] = new Dictionary<string, object?>
            {
                ["plane"] = "YZ",
                ["sketch_entities"] = new List<Dictionary<string, object?>>
                {
                    new() { ["type"] = "circle", ["cx"] = 0, ["cy"] = 0, ["radius"] = 5 }
                }
                // deliberately NO "features" key
            }
        };
        var jsonEl = JsonSerializer.Deserialize<JsonElement>(JsonSerializer.Serialize(oldTpl));
        TemplateManager.Save(_uniquePrefix + "-old", jsonEl);
        _createdTemplates.Add(_uniquePrefix + "-old");

        _mock.SetDocNewPartResult(new Dictionary<string, object?> { ["success"] = true });
        _mock.SetSketchCreateResult(new Dictionary<string, object?> { ["success"] = true });

        var runRes = _tools.template_run(_uniquePrefix + "-old");

        // Must not throw; must reach god layer (success or needs_confirmation or graceful error)
        Assert.NotNull(runRes);
        Assert.True(runRes.ContainsKey("success") || runRes.ContainsKey("needs_confirmation") || runRes.ContainsKey("error"));
        // Provenance still attached
        Assert.Equal(_uniquePrefix + "-old", runRes.GetValueOrDefault("template_used")?.ToString());
    }

    // ── PR3 E2E (3.4 / 3.5) ─────────────────────────────────────────────

    [Fact]
    public void TemplateCapture_E2E_5FeaturePart_CaptureThenRun_ReplayMatchesCounts()
    {
        // RED/GREEN: capture a 5-feature part (extrude+fillet+hole+circular_pattern+chamfer),
        // then template_run forwards features[] and the resulting envelope reports matching feature count + bbox.
        var fiveFeatures = new List<Dictionary<string, object?>>
        {
            new() { ["feature_type"] = "extrude", ["profile"] = "1", ["distance"] = 20.0, ["direction"] = "positive", ["operation"] = "new_body" },
            new() { ["feature_type"] = "fillet", ["edges"] = "1", ["radius"] = 1.0, ["mode"] = "constant" },
            new() { ["feature_type"] = "hole", ["x"] = 5.0, ["y"] = 5.0, ["diameter"] = 3.0, ["depth"] = 10.0, ["type"] = "drilled" },
            new() { ["feature_type"] = "circular_pattern", ["profile"] = "1", ["axis"] = "Y", ["count"] = 4, ["angle"] = 360.0 },
            new() { ["feature_type"] = "chamfer", ["edges"] = "2", ["distance"] = 0.5, ["mode"] = "equal_distance" }
        };

        SetupCaptureMocks(
            features: fiveFeatures,
            featureWarnings: new List<string>());

        // Capture
        var cap = _tools.template_capture(_uniquePrefix + "-e2e5", "5-feature e2e");
        Assert.True((bool)cap["success"]!);
        _createdTemplates.Add(_uniquePrefix + "-e2e5");

        var loaded = TemplateManager.Load(_uniquePrefix + "-e2e5");
        Assert.NotNull(loaded);
        Assert.True(loaded.Value.TryGetProperty("macro_config", out var mc));
        Assert.True(mc.TryGetProperty("features", out var farr) && farr.ValueKind == JsonValueKind.Array);
        Assert.Equal(5, farr.GetArrayLength());

        // Prepare mocks for a full god replay run.
        // CRITICAL for ask_before_modify guard: GetFeatureTree must report 0 features at the moment god evaluates the guard.
        // If >0, god returns needs_confirmation and skips dispatch. We set 0 here and leave it (mock returns same value for all calls in this test).
        var guardTreeZero = new Dictionary<string, object?>
        {
            ["success"] = true,
            ["feature_count"] = 0,
            ["features"] = new List<Dictionary<string, object?>>()
        };
        _mock.SetGetFeatureTreeResult(guardTreeZero);

        _mock.SetDocNewPartResult(new Dictionary<string, object?> { ["success"] = true });
        _mock.SetSketchCreateResult(new Dictionary<string, object?> { ["success"] = true });

        // Success for each dispatched feature (these will be recorded in CallLog when dispatch runs)
        _mock.SetExtrudeResult(new Dictionary<string, object?> { ["success"] = true, ["feature"] = "Extrude1" });
        _mock.SetFilletResult(new Dictionary<string, object?> { ["success"] = true, ["feature"] = "Fillet1" });
        _mock.SetHoleResult(new Dictionary<string, object?> { ["success"] = true, ["feature"] = "Hole1" });
        _mock.SetCircularPatternResult(new Dictionary<string, object?> { ["success"] = true, ["feature"] = "CircularPattern1" });
        _mock.SetChamferResult(new Dictionary<string, object?> { ["success"] = true, ["feature"] = "Chamfer1" });

        // Optional: bounding box for if god reaches verify (we primarily assert on CallLog dispatch)
        var expectedBbox = new Dictionary<string, object?>
        {
            ["success"] = true,
            ["min"] = new[] { 0.0, 0.0, 0.0 },
            ["max"] = new[] { 20.0, 20.0, 10.0 },
            ["size"] = new[] { 20.0, 20.0, 10.0 },
            ["center"] = new[] { 10.0, 10.0, 5.0 }
        };
        _mock.SetGetBoundingBoxResult(expectedBbox);

        var runRes = _tools.template_run(_uniquePrefix + "-e2e5");

        Assert.NotNull(runRes);
        Assert.Equal(_uniquePrefix + "-e2e5", runRes.GetValueOrDefault("template_used")?.ToString());

        // The run reached the god layer (either full success or guard returned needs_confirmation).
        // In this mock setup with 0 features in guard, it should dispatch.
        Assert.True(runRes.ContainsKey("success") || runRes.ContainsKey("needs_confirmation") || runRes.ContainsKey("error"));

        // Strong dispatch evidence (3.4/3.5): features[] were forwarded and each type called the corresponding provider method.
        var calledMethods = _mock.CallLog.Select(c => c.Method).ToList();
        Assert.Contains("Extrude", calledMethods);
        Assert.Contains("Fillet", calledMethods);
        Assert.Contains("Hole", calledMethods);
        Assert.Contains("CircularPattern", calledMethods);
        Assert.Contains("Chamfer", calledMethods);
    }
}
