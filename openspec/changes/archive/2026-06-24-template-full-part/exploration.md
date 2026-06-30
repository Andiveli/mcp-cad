# Exploration: template-full-part

## Current State

The codebase has a working template system (committed on `feature/macro-god-part`) that captures and replays the **base sketch + parameters only**, NOT the full part geometry.

### Template pipeline today

**Capture side** (`src/McpCad.Tools/TemplateTools.cs:30-75`):
- Calls `ReadSketchData(1)` → returns entities from sketch 1 only
- Calls `ParamList()` → captures model parameters (d0, d1, …)
- Wraps both into a `macro_config` block with `plane`, `sketch_entities`, `verify`
- Saves the JSON via `TemplateManager.Save(name, json)` to `./templates/<name>.json`

**Replay side** (`src/McpCad.Tools/TemplateTools.cs:97-180`):
- Loads JSON, extracts `parameters` defaults, applies overrides via `TemplateManager.Substitute`
- Calls `macro_god_part(...)` with the resolved config — passes `sketch_*` fields, `feature_type`, `feature_distance`, etc., but **only the FIRST feature** (the macro accepts one feature_type at a time)

### What already exists in the codebase

| Component | Location | Capability |
|---|---|---|
| `GetFeatureTree()` | `InspectionManager.cs:123-181` + `IMechanicalCadProvider.cs:163` | Walks `compDef.Features` recursively, returns `{name, type, suppressed, is_consumed, sub_features}` |
| `SketchReader.ReadSketchEntities(planarSketch)` | `Helpers/SketchReader.cs` | Typed COM traversal of lines/circles/arcs/points/ellipses/splines → macro_god_part JSON |
| `macro_god_part` (full schema) | `MacroTools.cs:465-1179` | All 6 3D create features (extrude/revolve/sweep/loft/coil/rib) + patterns (circular/rectangular/mirror) + modify (fillet/chamfer/shell/draft/thread/split/hole) + iProperties + verification |
| `TemplateManager.Substitute()` | `TemplateManager.cs:128-163` | Regex-based `${PARAM}` substitution on serialized JSON text — works for ANY value (numeric or string), already handles feature params |
| `IMechanicalCadProvider` | full provider | All 3D ops, ParamList/Get/Set, GetFeatureTree, ReadSketchData — every method the replay will need |

### What is missing

1. **`template_capture` does not walk the feature tree.** It calls `ReadSketchData(1)` + `ParamList()` only. The `GetFeatureTree()` call would need to traverse every top-level feature and serialize its type + capture parameters.
2. **`macro_god_part` is single-feature.** It accepts one `feature_type` + one `feature_*` set. Replaying an extrude → fillet → hole chain requires 3 sequential calls or a new "multi-feature" extension.
3. **No `FeatureReader` (analogous to SketchReader).** `InspectionManager.FeatureToDict` returns just `{name, type, suppressed, is_consumed, sub_features}` — it does NOT extract the typed descriptor (profile index, distance, axis, radius, direction, operation, etc.) needed to replay. A new helper must read each feature's discriminator + params.
4. **No sketch-to-feature linkage in capture.** A feature references its source sketch (`sketch_index`) and profile (`profile_index`) — without that linkage, replay cannot rebuild the same geometry. The template needs `sketches[]` (array of sketches by 1-based index, each with `sketch_entities` + `sketch_index`) and `features[]` (each referencing `sketch_ref`).
5. **No pattern/sub-feature nesting.** Circular patterns contain nested sub-features in `sub_features`. The replay needs to walk this hierarchy.
6. **`template_run` parameter mapping is incomplete.** Current run only forwards ~12 fields to `macro_god_part`. A multi-feature replay would need the whole god schema (sketch_modify, sketch_pattern, pattern_3d, modify_3d, part_number, description, material) plus a `features[]` array.
7. **No work-feature (work plane/axis/point) capture.** Custom work features used by sweeps/revolves/coils aren't captured. Templates won't replay those parts correctly.

### ConnectingRod template analysis (`templates/ConnectingRod.json`)

The captured file is **sketch-only**. It contains 28 lines, 4 circles, 2 arcs, 16 points — all from sketch 1. **No features, no extrude distance, no fillets, no holes.** A user running this template gets only the 2D sketch with `verify: true`. To recreate the actual rod, they would need to manually add `feature_type=extrude`, `feature_distance`, etc. — which is exactly the gap this change fills.

---

## Affected Areas

| File | Why affected |
|------|--------------|
| `src/McpCad.Tools/TemplateTools.cs` | **Modified** — `template_capture` must call `GetFeatureTree()` + new `FeatureReader` + multi-sketch loop; `template_run` must forward a `features[]` array to a multi-feature replay |
| `src/McpCad.Tools/TemplateManager.cs` | **Minor modified** — `Substitute()` already handles arbitrary JSON paths; only need to verify embedded expressions like `"${d0}/2"` survive multi-pass (currently one-pass, should be fine) |
| `src/McpCad.Inventor/Helpers/SketchReader.cs` | **Unchanged** — already per-sketch; capture loop will call it for each sketch index |
| `src/McpCad.Inventor/Helpers/FeatureReader.cs` | **NEW** — analogous to SketchReader but walks `compDef.Features` typed collections, extracts feature-specific params (profile, distance, axis, radius, faces, operation), emits macro_god_part-compatible JSON |
| `src/McpCad.Inventor/Managers/InspectionManager.cs` | **Modified** — `FeatureToDict` extended (or split out) to extract discriminators per Inventor feature type (ExtrudeFeature, RevolveFeature, FilletFeature, HoleFeature, PatternFeature, etc.); or `FeatureReader` owns its own traversal and replaces `FeatureToDict` |
| `src/McpCad.Tools/MacroTools.cs` | **Modified** — add a new top-level field `features[]` (array of feature descriptors) that iterates AFTER the existing single `feature_type`/`feature_*` block; each item has its own `feature_type` + params + optional `pattern_3d` + optional `modify_3d` scoped to that feature |
| `src/McpCad.Core/IMechanicalCadProvider.cs` | **Possibly modified** — if `FeatureReader` lives in `McpCad.Inventor` and is exposed through the provider, add `ReadFeatureData(int featureIndex = 1)` or keep feature capture entirely inside the provider implementation (no interface change needed if capture stays in Inventor layer) |
| `src/McpCad.Inventor/InventorProvider.cs` | **Possibly modified** — to expose `ReadFeatureData` if interface changes |
| `tests/McpCad.Tests/Tools/TemplateToolsTests.cs` | **Modified** — extend capture test to mock `GetFeatureTree` returning 2 extrusions + 1 fillet; verify feature descriptors in saved JSON |
| `tests/McpCad.Tests/Tools/MacroToolsTests.cs` | **Modified** — add tests for the new `features[]` array dispatch |
| `tests/McpCad.Tests/Mocks/MockInventorProvider.cs` | **Possibly extended** — add `SetReadFeatureDataResult` if `ReadFeatureData` becomes part of the interface |
| `openspec/specs/macro-god-part/spec.md` | **Modified (delta)** — add `features[]` requirement + multi-feature dispatch scenario |
| `openspec/specs/mcp-tool-registration/spec.md` | **Modified (delta)** — confirm `template_capture` / `template_run` updated behavior is registered |
| `templates/ConnectingRod.json` (re-captured) | **Modified** — regenerate after feature capture ships to demonstrate the new format |

---

## Approaches

### Approach 1: Extend `macro_god_part` with `features[]` array + new `FeatureReader` helper (recommended)

**Capture flow:**
```
template_capture(name):
  sketches = []
  for i = 1 to Sketches.Count:
    sketches.append({ index: i, entities: SketchReader.ReadSketchEntities(sketches[i]).entities })
  tree = GetFeatureTree()
  features = []
  for each feature in tree (in creation order):
    features.append(FeatureReader.ReadFeature(feature))   # emits {type, profile, distance, axis, operation, ...}
  template.macro_config = {
    plane: "YZ",    # default; user can override per-sketch
    sketches: [...],  # NEW: array of sketches (replaces single sketch_entities)
    features: [...],  # NEW: array of feature descriptors
    verify: true
  }
```

**Replay flow:**
```
template_run(name, overrides):
  load template → substitute ${PARAM}
  macro_god_part_replay(...):
    for each sketch in sketches:
      SketchCreate(plane), draw entities, then continue
    for each feature in features:
      dispatch on type → call existing Extrude/Revolve/Fillet/etc
      if feature.pattern_3d → apply
      if feature.modify_3d → apply
    verify
```

- **Pros**: Reuses every existing provider method (Extrude, Revolve, Fillet, Chamfer, Hole, Pattern, etc.). One replay engine. Sub-features (patterns, work features) naturally serialized. Preserves parameter ordering.
- **Pros**: Backward-compatible — `macro_god_part` keeps its existing single-feature signature; new array field is additive. Old templates without `features[]` still work.
- **Pros**: `TemplateManager.Substitute` already handles arbitrary JSON paths and embedded expressions — no changes to substitution.
- **Cons**: `macro_god_part` grows another field; the signature is already long. Mitigation: keep `feature_*` scalars for legacy single-feature path, add `features[]` for arrays.
- **Cons**: `FeatureReader` must handle ~10 distinct feature types (ExtrudeFeature, RevolveFeature, FilletFeature, ChamferFeature, HoleFeature, CircularPatternFeature, RectangularPatternFeature, MirrorFeature, ShellFeature, DraftFeature, ThreadFeature, SweepFeature, LoftFeature, CoilFeature, RibFeature, EmbossFeature, CombineFeature, SplitFeature). Each has different properties. Some types are uncommon (we may stub them with `unsupported_feature` warning).
- **Effort**: **High** — 1 new file (FeatureReader), 1 modified file (MacroTools for features[] dispatch), 1 modified (TemplateTools), 1 modified (InspectionManager or replace FeatureToDict), plus tests

### Approach 2: New dedicated `macro_replay_part` tool + `FeatureReader`

Create a separate `MacroReplayTools.cs` with a new `macro_replay_part(template_path, overrides)` that reads the JSON template (sketch array + features array) and walks them with its own dispatch loop. Existing `macro_god_part` stays untouched.

- **Pros**: Cleanest separation. New tool is purely additive. Existing macro_god_part test surface is undisturbed.
- **Pros**: Smaller blast radius — change lives in 1 new file + 1 modified (TemplateTools to call new tool instead of macro_god_part).
- **Pros**: Easier to ship incrementally — can add support for one feature type at a time without touching macro_god_part.
- **Cons**: Two macro tools doing similar things; possible confusion.
- **Cons**: Some code duplication (sketch creation loop already exists in macro_god_part).
- **Effort**: **Medium** — 1 new file (MacroReplayTools), 1 modified (TemplateTools to dispatch to it), 1 new (FeatureReader), plus tests. No macro_god_part changes.

### Approach 3: Pure-Python-style "replay API" — encode feature tree as native Inventor commands

Capture the tree, then replay by emitting each feature's native `Inventor.<X>Features.Add(...)` calls via dynamic COM. Don't go through the provider layer at all for replay.

- **Pros**: Maximum fidelity — can replay ANY feature type Inventor supports.
- **Cons**: Bypasses the entire provider/manager architecture. Loses error handling, logging, parameter substitution. Breaks the layered architecture the codebase deliberately built.
- **Cons**: Tests cannot mock the replay — would require real Inventor to verify.
- **Effort**: **High** with strong regression risk. **Not recommended.**

---

## Recommendation

**Approach 1** — extend `macro_god_part` with `features[]` + add `FeatureReader`.

**Why:**
- Matches the existing layered architecture (Capture → JSON → Replay → Provider → Manager).
- The provider interface already exposes every replay primitive; FeatureReader only needs to translate COM feature state into the same JSON shape `macro_god_part` already consumes.
- Template substitution (`TemplateManager.Substitute`) already works on arbitrary JSON paths — no changes needed.
- Backward-compatible with existing templates (the ConnectingRod.json template keeps working if `features[]` is empty or absent).
- Aligns with the user's explicit goal from session #310: "capture toda la pieza, todos los demás feature del 3D Model para que replique la pieza, solo cambiar ciertos parametros."
- The Topic key for this evolution already exists in engram: `template-system/full-part-capture`.

**Complexity budget:**
- `FeatureReader.cs` (new): ~600-800 lines (one switch case per feature type, mirroring SketchReader)
- `MacroTools.cs` modification: ~150-200 lines (new features[] dispatch loop, similar to existing feature_type switch)
- `TemplateTools.cs` modification: ~100-150 lines (capture loop calls FeatureReader per feature; run loop passes features[] to god)
- Tests: ~300-400 lines (mock tree with mixed features, verify capture output, verify dispatch calls)
- **Total**: roughly 1100-1550 lines changed/added. **Will exceed the 400-line PR budget.** Use chained PRs:
  - PR 1: `FeatureReader` + unit tests + capture-side changes (writes features[] to JSON, no replay yet)
  - PR 2: `macro_god_part features[]` dispatch + tests (replay engine, can be exercised with hand-written JSON before template-run wires up)
  - PR 3: `template_capture`/`template_run` integration with feature replay + end-to-end test

---

## Risks

- **Feature type coverage**: `FeatureReader` must handle every Inventor PartFeature subtype encountered in real parts. Coverage gap → silent data loss in capture. Mitigation: emit a structured warning list (like SketchReader does) when an unsupported feature type is encountered; capture still completes. Track which types are unsupported.
- **Sketch ↔ feature linkage breaks on replay**: A `CircularPattern` references its parent feature by name; the parent may have been renamed or absent. Mitigation: capture parent feature index in the descriptor; replay resolves by index; warn on resolution failure.
- **Profile index drift**: An extrude references `sketch.Profiles.Item(1)`. If the sketch replay produces different closed-profile ordering (e.g. order of loops/paths differs from Inventor default), the wrong profile gets extruded. Mitigation: capture `profile_index` AND a profile signature (centroid + area) at capture time; on replay, pick the closest matching profile. v1: just use index and warn on mismatch.
- **Work features (work planes, axes, points) used as feature inputs**: revolve axis, sweep path, coil axis may reference custom work features that aren't captured. Without work-feature capture, those features will fail on replay. Mitigation: capture work-feature descriptors in a parallel `work_features[]` block; replay creates them in order.
- **iProperty dependencies**: Part Number / Description / Material are applied at the END of macro_god_part; if a feature is suppressed or fails, the part number is still set. This is acceptable for v1 (matches existing macro_god_part behavior).
- **iFeature / Content Center features**: Not in scope. Capture will skip with warning.
- **Sketch constraints and dimensions**: Currently `template_capture` does not capture sketch_constraints or sketch_dimensions. Without them, replayed sketches may be over- or under-constrained. Mitigation: also extend `ReadSketchData` (or add a sibling) to capture `Sketch.Constraints` + `Sketch.Dimensions` (already exposed via existing Constraint/Dimension properties) and store them in the per-sketch block. **This is in scope but a smaller follow-up slice.**
- **${PARAM} substitution at capture time**: If `d0` is captured as a number (e.g. `42`), the user might want to override it later. But the substitution engine currently only replaces string tokens like `"${d0}"`. Numeric literals (no quotes) aren't substituted. Mitigation: at capture time, for each model parameter, emit `expression` (the formula string like `"42 mm"`) as the JSON value, so `${d0}` substitution works. The ParamList already returns both `value` and `expression`. v1: write `expression` if present and not just a literal, else `value`.
- **Backward compatibility of old templates**: A pre-features[] template run via new template_run must NOT break. Mitigation: if `features` field is absent/empty in the template, fall back to current behavior (single `feature_type` + scalar `feature_*`).
- **Multi-sketch features**: Sweeps and lofts use profiles from multiple sketches. Capture must store both sketches and let the feature descriptor reference both by index. Already handled by Approach 1's design (sweeps + lofts already support `path_sketch` + `profile_sketch` in macro_god_part's existing schema).

---

## Ready for Proposal

**Yes.**

All required primitives already exist in the codebase:
- Provider exposes `GetFeatureTree`, `ReadSketchData`, `ParamList`, every replay op, `GetBoundingBox`, `CaptureViewportImage`.
- `macro_god_part` already implements the full feature/pattern/modify schema; we only need to extend it to accept an array of features.
- `TemplateManager.Substitute` already handles arbitrary JSON paths.
- `SketchReader` already does the per-sketch traversal; `FeatureReader` follows the same pattern.
- The user's intent is captured in engram observation #309 (topic `template-system/full-part-capture`).

What the orchestrator should tell the user: "All building blocks for full-part templates exist. The codebase has the feature tree, sketch reader, full god-macro schema, and template substitution. The capture path needs a new `FeatureReader` helper that translates each Inventor feature into a JSON descriptor, and `macro_god_part` needs to accept a `features[]` array for multi-feature replay. Approach 1 (extend god with `features[]`) is recommended, with chained PRs to stay within the 400-line review budget. Ready for proposal."
