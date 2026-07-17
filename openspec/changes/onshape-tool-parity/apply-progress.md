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

## TDD Cycle Evidence (Strict TDD)

| Task | RED | GREEN | TRIANGULATE | REFACTOR |
|------|:---:|:-----:|:-----------:|:--------:|
| 1.1 Sketch URL path | N/A (config) | Path helper in OnshapeApiPaths.cs | — | XML doc added |
| 1.2 OnshapeSketchBuilder | BuildRectangle/Line/Circle tests | Builder creates BTMSketch entities | Zero-size, negative, single-point edge cases | Shared serialisation helpers extracted |
| 1.3 Golden fixtures | — | Fixture files created | — | — |
| 1.4 ICadWebProvider.SketchReplace | Conformance test with payload | Interface + Mock + Provider wired | — | — |
| 1.5 PartStudioManager.SketchReplace | WireMock POST body shape | Real HTTP POST with JSON body | 409 skew → MicroversionSkewException | — |
| 1.6 Remove stubs | — | SketchReplaceStub removed | — | — |
| 2.1 OnshapeFeatureTemplates (extrude) | ExtrudeFromSketch test | Template with depth/direction/boolean | Negative depth, reverse, joined op | BuildParameter helpers extracted |
| 2.2 FeatureAdd BT envelope | WireMock regression guard | `{"feature":{...}}` envelope format | Provider extrude envelope test | — |
| 2.3 onshape_sketch_rectangle | Mock-provider test | MCP tool with cm→m conversion | Invalid address JSON | — |
| 2.4 onshape_extrude | Mock-provider test | MCP tool with template envelope | Negative depth behaviour documented | — |
| 2.5 onshape_feature_list | Mock-provider test | MCP tool wired to FeatureList | — | — |
| 2.6 FeatureList on ICadWebProvider | — | Interface + mock + provider wiring | — | — |
| 2.7 Deprecate create_sketch/add_extrude | — | DEPRECATED descriptions + delegate | — | — |
| 2.8 P0 E2E WireMock | E2E sketch→list→extrude test | Full flow through provider/managers | — | — |
| 2.9 Collision tests | — | ToolRegistrationCollision asserts | — | — |
| 3.1 Revolve template | RevolveFromSketch test | Template with angle/direction | 0°, 360°, reverse direction | — |
| 3.2 Fillet template | Fillet test | Template with radius/edge validation | Multi-edge, zero-radius error | — |
| 3.3 Chamfer template | Chamfer test | Template with distance/chamferType | Equal-distance chamfer | — |
| 3.4 Hole template | Hole test | Template with center/diameter/depth | Blind vs through, threaded vs simple | — |
| 3.5 P1 MCP tools | Mock-provider × 4 + WireMock | onshape_revolve/fillet/chamfer/hole | WireMock envelope × 4 | Shared BuildFeatureEnvelope reused |
| 3.6 P1 collision tests | — | ToolRegistrationCollision asserts | — | — |
| 4.1 onshape_asm_list_instances | Mock-provider test | MCP tool wired | Empty assembly test | — |
| 4.2 onshape_asm_create_instance | Mock-provider test | MCP tool wired | — | — |
| 4.3 onshape_asm_delete_instance | Mock-provider test | MCP tool wired | Non-existent instance error | — |
| 4.4 Parity matrix | — | docs/onshape-parity-matrix.md | — | — |
| 4.5 Provider docs | — | docs/providers.md modeling section | — | — |
| 4.6 Assembly collision tests | — | ToolRegistrationCollision asserts | — | — |

## next_recommended

- `sdd-verify` slices 1–4

## Slice 4 — completed (feature/onshape-tool-parity/slice-4)

| Task area | Done |
|-----------|------|
| 4.1 | `onshape_asm_list_instances` and empty-assembly mock test |
| 4.2 | `onshape_asm_create_instance` and part-reference forwarding test |
| 4.3 | `onshape_asm_delete_instance` and provider-error handling test |
| 4.4 | `docs/onshape-parity-matrix.md` covering P0–P2, existing exports, and remaining catalog status |
| 4.5 | Onshape modeling, `address_json`, cm conversion, P0 flow, and matrix reference in `docs/providers.md` |
| 4.6 | Assembly MCP tool registration collision assertions |

### Verification

```text
RED: dotnet test tests/McpCad.Tests/ --filter "FullyQualifiedName~onshape_asm|ToolRegistrationCollision"
→ expected compilation failure: missing onshape_asm_* MCP methods

GREEN: dotnet test tests/McpCad.Tests/ --filter "FullyQualifiedName~onshape_asm|ToolRegistrationCollision"
→ 6 passed

Full suite: 95 passed, 7 skipped, 1 failed
→ PollTranslationAsync_Transient500ThenDone_Succeeds is the pre-existing failure recorded in Slices 2–3.
```
