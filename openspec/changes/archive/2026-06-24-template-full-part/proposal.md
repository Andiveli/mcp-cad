# Proposal: Full Part Template Capture

## Intent

Current templates capture only sketch entities + model parameters — 3D features (extrusions, fillets, holes, patterns) are lost. Extend the template system so it captures and replays complete part geometry with parameter variation.

## Scope

### In Scope
- `FeatureReader` helper: COM traversal of feature tree → typed JSON descriptors
- `features[]` dispatch in `macro_god_part`: multi-feature replay loop
- Multi-sketch support: capture all sketches, link features by `sketch_ref`
- Template `capture`/`run` integration with feature capture pipeline
- Chained PRs: PR1 (FeatureReader + capture), PR2 (features[] dispatch), PR3 (integration)

### Out of Scope
- Template editing/modification of existing templates
- Sketch constraints and dimensions capture (deferred follow-up)
- Work feature capture (planes, axes, points)
- iFeature / Content Center features
- Template versioning or migration

## Capabilities

### New Capabilities
- `feature-reader`: Walks each feature node in creation order, emits typed descriptor (type + distance/axis/radius/faces/operation + pattern_3d/modify_3d). Analogous to `SketchReader` for features.

### Modified Capabilities
- `macro-god-part`: Add `features[]` array parameter for multi-feature replay alongside existing single-feature path. Each entry has `feature_type` + typed params + optional `pattern_3d`/`modify_3d`.
- `mcp-tool-registration`: Update `template_capture`/`template_run` tool docs for full-part behavior.

## Approach

**Approach 1** — extend `macro_god_part` with `features[]` + new `FeatureReader`.

**Capture**: `template_capture` loops all sketches via `SketchReader`, walks feature tree via `GetFeatureTree()` + new `FeatureReader`, emits `sketches[]` + `features[]` in template JSON.

**Replay**: `template_run` passes resolved JSON to `macro_god_part`, which iterates `features[]` dispatching each descriptor to the existing provider method (Extrude, Revolve, Fillet, Hole, etc.).

Backward-compatible: old templates without `features[]` still work. Unsupported feature types emit structured warnings (capture completes).

## Affected Areas

| Area | Impact | Description |
|------|--------|-------------|
| `src/McpCad.Inventor/Helpers/FeatureReader.cs` | New | Feature tree traversal → JSON |
| `src/McpCad.Tools/MacroTools.cs` | Modified | Add `features[]` dispatch |
| `src/McpCad.Tools/TemplateTools.cs` | Modified | Capture/run integration |
| `src/McpCad.Inventor/Managers/InspectionManager.cs` | Minor | Extend/replace FeatureToDict |
| `tests/McpCad.Tests/Tools/TemplateToolsTests.cs` | Modified | Capture tests |
| `tests/McpCad.Tests/Tools/MacroToolsTests.cs` | Modified | Dispatch tests |
| `openspec/specs/macro-god-part/spec.md` | Delta | Add features[] reqs |

## Risks

| Risk | Likelihood | Mitigation |
|------|------------|------------|
| Unhandled feature types | Med | Structured warning list; capture continues |
| Profile index drift on replay | Low | Capture index + centroid signature; warn on mismatch |
| Sketch→feature linkage | Med | Capture sketch_ref index; warn on resolution failure |
| Old template backward compat | Low | Absent `features[]` → current behavior unchanged |

## Rollback Plan

Feature flag (`FeaturesEnabled`) in template config. If off or `features[]` absent, falls back to current single-sketch + single-feature flow. FeatureReader code toggles at manager level.

## Dependencies

- `GetFeatureTree()` already exists in `InspectionManager`
- All provider replay methods already exist (extrude, fillet, hole, etc.)
- `TemplateManager.Substitute` already handles arbitrary JSON paths — no changes

## Success Criteria

- [ ] Capture a part with 5+ mixed features (extrude + fillet + hole + circular pattern + chamfer) and replay matches original geometry
- [ ] Old templates without `features[]` produce identical output to current code
- [ ] FeatureReader emits structured warnings for unsupported types without aborting capture
- [ ] All existing tests pass with zero assertion changes
