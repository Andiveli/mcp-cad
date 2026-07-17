# Apply progress: onshape-tool-parity

## Slice 1 — completed (parent)

Sketch builder, real `SketchReplace`, WireMock tests. See prior revision.

## Slice 2 — completed (parent)

**Executor:** parent session (`sdd-apply` blocked again: no write/edit/bash).

| Task area | Done |
|-----------|------|
| 2.1 | `OnshapeFeatureTemplates.ExtrudeFromSketch`, `OnshapeFeatureTemplatesTests` |
| 2.2 | `FeatureManager.FeatureAdd` BT envelope + optional `featurePostBodyJson`; WireMock regression |
| 2.3–2.5 | `onshape_sketch_rectangle`, `onshape_extrude`, `onshape_feature_list` |
| 2.6 | `ICadWebProvider.FeatureList` + mock + provider |
| 2.7 | `create_sketch` / `add_extrude` deprecated + delegate |
| 2.8 | `P0EndToEnd_MockProvider_SketchListExtrude` |
| 2.9 | Collision test asserts for new tool names |

### Verification

```text
dotnet test -f net8.0 --filter "FeatureAdd|OnshapeFeature|OnshapeAtomicToolsModeling|P0EndToEnd"
→ 9 passed

Full suite: 69 passed, 1 failed (PollTranslationAsync_Transient500ThenDone — pre-existing)
```

## Harness note

`subagent_run` agent `sdd-apply` still exposes read-only tools. Issue draft pending user direction.

## Slice 3 — completed (feature/onshape-tool-parity/slice-3)

| Task area | Done |
|-----------|------|
| 3.1 | `RevolveFromSketch` template with angle/direction/operation parameters and edge-case tests |
| 3.2 | `Fillet` template with non-zero radius validation and multi-edge tests |
| 3.3 | `Chamfer` template with equal-distance parameter and multi-edge selection |
| 3.4 | `Hole` template with center point, diameter, depth, and hole type parameters |
| 3.5 | `onshape_revolve`, `onshape_fillet`, `onshape_chamfer`, and `onshape_hole` tools; mock and WireMock structural tests |
| 3.6 | P1 tool-name collision registration assertions |

### Verification

```text
RED: dotnet test tests/McpCad.Tests/ --filter "P1"
→ expected compilation failure: missing P1 template and MCP tool methods

GREEN: dotnet test tests/McpCad.Tests/ --filter "P1"
→ 17 passed

dotnet test tests/McpCad.Tests/ --filter "ToolRegistrationCollision"
→ 2 passed

Full suite: 91 passed, 7 skipped, 1 failed
→ PollTranslationAsync_Transient500ThenDone_Succeeds is the pre-existing failure recorded in Slice 2.
```

## next_recommended

- User: issue for pi-subagents / gentle-pi tooling
- `sdd-verify` slice 1+2
- `sdd-verify` slices 1–3
- `sdd-apply` slice 4
