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

## next_recommended

- User: issue for pi-subagents / gentle-pi tooling
- `sdd-verify` slice 1+2
- `sdd-apply` slice 3 (or parent apply)