# Design: Full Part Template Capture

## Technical Approach

Three chained PRs: (1) **FeatureReader** — COM traversal of ~20 Inventor PartFeature subtypes → typed JSON descriptors (capture-side only), (2) **features[] dispatch** in macro_god_part — multi-feature replay loop alongside existing single-feature path, (3) **template capture/run integration** — wire capture pipeline into template_capture and forward features[] through template_run.

## Architecture Decisions

| Decision | Options | Chosen | Rationale |
|----------|---------|--------|-----------|
| FeatureReader location | Internal helper vs provider method | `ReadFeatureData()` on `IMechanicalCadProvider` | Mirrors exact `ReadSketchData()` → `SketchReader` pattern. TemplateTools needs typed descriptors without COM access. |
| Multi-feature dispatch | Replace feature_type vs coexist | Optional `features[]` param; fall back to single-feature when absent | Old ConnectingRod.json has no features[]. Backward compat is non-negotiable. |
| Profile linkage | Capture index + centroid vs index-only | Index + centroid signature; warn on replay mismatch | Profile index drift is real (VCS reorder on param change). Centroid gives a cross-check. |

## Data Flow

```
Capture (template_capture):
  _provider.ReadSketchData(1..N)     → sketches[]
  _provider.GetFeatureTree()          → tree (name, type)
  _provider.ReadFeatureData()         → features[] (typed descriptors)
      └── FeatureReader.ReadFeature(feature) → {type, profile, distance, ...}
  TemplateManager.Save(name, {macro_config: {sketches[], features[], ...}})

Replay (template_run):
  TemplateManager.Load → Substitute(macro_config, overrides)
  _macro.macro_god_part(features: "...")
    └── if features[] absent → current single-feature path
    └── if features[] present → iterate, dispatch each:
         switch(feature_type):
           extrude        → provider.Extrude(...)
           revolve        → provider.Revolve(...)
           fillet         → provider.Fillet(...)
           hole           → provider.Hole(...)
           chamfer, shell, draft, thread, split, sweep, loft, coil, rib,
           circular_pattern, rectangular_pattern, mirror_feature,
           emboss, derive, thicken, combine
         per-entry: optional pattern_3d + modify_3d scoped to that feature
```

## File Changes

| File | Action | Description |
|------|--------|-------------|
| `src/McpCad.Inventor/Helpers/FeatureReader.cs` | **Create** | Static helper: typed COM traversal of ~20 `PartFeature` subtypes (ExtrudeFeatures, FilletFeatures, HoleFeatures, PatternFeatures, etc.). Each collection: 1-based Item loop, try/catch, emit typed dict or warning. Follows SketchReader.cs structure identically. |
| `src/McpCad.Core/IMechanicalCadProvider.cs` | Modify | Add `Dictionary<string, object?> ReadFeatureData()` to interface (line 42 area). |
| `src/McpCad.Inventor/InventorProvider.cs` | Modify | Implement `ReadFeatureData()` → calls `FeatureReader.ReadFeatures(compDef)`. |
| `src/McpCad.Tools/MacroTools.cs` | Modify | Add `string? features` param. New `FeatureDescriptor` DTO record. Before existing `feature_type` switch (line 898), insert features[] loop: parse JSON → dispatch each entry via provider methods. Same per-feature try/catch pattern as sketch phase. `FeatureDescriptor` param list covers all ~20 feature types from spec. |
| `src/McpCad.Tools/TemplateTools.cs` | Modify | `template_capture` (line 40-60): call `ReadFeatureData()` + `GetFeatureTree()` alongside `ReadSketchData()`, write `features[]` into `macro_config`. `template_run` (line 153-172): forward `features` to `macro_god_part` alongside existing scalar params. |
| `tests/McpCad.Tests/Mocks/MockInventorProvider.cs` | Modify | Add `SetReadFeatureDataResult(...)`. |
| `tests/McpCad.Tests/Tools/TemplateToolsTests.cs` | Modify | Capture produces features[] in JSON; old template backward compat. |
| `tests/McpCad.Tests/Tools/MacroToolsTests.cs` | Modify | features[] dispatch order; mixed-types; empty fallback; per-entry failure isolation. |

## Interfaces / Contracts

```csharp
// IMechanicalCadProvider addition
Dictionary<string, object?> ReadFeatureData();

// MacroTools new param
string? features = null

// FeatureDescriptor DTO
public record FeatureDescriptor(
    string FeatureType,
    string? Profile = null, double? Distance = null,
    string? Direction = null, double? Taper = null,
    string? Operation = null, string? Axis = null,
    double? Angle = null, double? Radius = null,
    string? Edges = null, string? Mode = null,
    string? Path = null, string? Profiles = null,
    double? Pitch = null, double? Revolutions = null,
    double? Thickness = null, double? Offset = null,
    string? Specification = null, string? Face = null,
    string? Faces = null, string? PullDirection = null,
    string? FixedEntity = null, double? X = null,
    double? Y = null, double? Diameter = null,
    double? Depth = null, string? HoleType = null,
    string? SplitTool = null, string? TargetBody = null,
    string? RemoveSide = null, string? BaseBody = null,
    string? ToolBodies = null, string? SourcePath = null,
    string? EmbossType = null, int? Count = null,
    string? MirrorPlane = null, int? SketchRef = null,
    int? ParentFeatureIndex = null,
    string? Pattern3d = null, string? Modify3d = null
);
```

## Testing Strategy

| Layer | What to Test | Approach |
|-------|-------------|----------|
| Unit (FeatureReader) | Each PartFeature subtype → correct descriptor shape | Mock COM objects; verify dict keys per type; verify warnings for unsupported types |
| Unit (features[] dispatch) | Dispatch order, fallback, per-entry isolation | Call macro_god_part with hand-crafted features[] JSON; assert provider methods called in exact sequence; empty features[] falls back to single-feature |
| Integration (capture) | template_capture writes features[] into saved JSON | Mock provider returns ReadFeatureData + GetFeatureTree; verify saved file contains features[] array |
| E2E | Full capture → replay of 5-feature part | Capture real part with extrude+fillet+hole+pattern+chamfer; run template; compare bounding box and feature count |

## Chained PR Plan

| PR | Focus | Files | Est. Lines | Risk |
|----|-------|-------|------------|------|
| **PR1** | FeatureReader + capture side | FeatureReader (new 400-500), InventorProvider (+20), IMechanicalCadProvider (+1), Mock (+20). Tests: FeatureReader unit tests. | ~550 | Feature type coverage gap |
| **PR2** | features[] dispatch in macro_god_part | MacroTools.cs (+250), FeatureDescriptor DTO, tests. No capture changes. | ~350 | Per-entry pattern_3d/modify_3d scoping edge cases |
| **PR3** | Template integration + e2e | TemplateTools.cs (capture + run wiring), test files, ConnectingRod re-capture | ~400 | Multi-sketch capture completeness |

**400-line budget risk**: Split across 3 PRs removes the risk. Each PR is under 550 lines, with PR2 well under 400.

## Migration / Rollout

No migration. Old templates without `features[]` replay identically. Feature flag unnecessary — absence of the array IS the flag.

## Open Questions

- [ ] Profile centroid signature: capture both index + centroid area, or index only in v1? Recommend index + centroid for mismatch detection.
- [ ] Multi-sketch: template_capture currently captures sketch 1 only. Does v1 loop all sketches (`Sketches.Count`) or stay single? Recommend single-sketch v1, multi-sketch follow-up.
