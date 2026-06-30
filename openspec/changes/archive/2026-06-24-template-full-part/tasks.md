# Tasks: Full Part Template Capture

## Review Workload Forecast

| Field | Value |
|-------|-------|
| Estimated changed lines | ~1,300 across 3 PRs |
| 400-line budget risk | Medium |
| Chained PRs recommended | Yes |
| Suggested split | PR1 → PR2 → PR3 |
| Delivery strategy | force-chained |
| Chain strategy | feature-branch-chain |

Decision needed before apply: No
Chained PRs recommended: Yes
Chain strategy: feature-branch-chain
400-line budget risk: Medium

### Suggested Work Units

| Unit | Goal | Likely PR | Notes |
|------|------|-----------|-------|
| 1 | FeatureReader + capture infra | PR 1 | base=feat/template-full-part; tests |
| 2 | features[] dispatch in macro_god_part | PR 2 | base=PR1 branch; tests |
| 3 | Template integration + e2e | PR 3 | base=PR2 branch; tests |

## Phase 1: FeatureReader + Capture Infrastructure (PR1)

- [x] 1.1 RED: Write FeatureReader unit tests — each PartFeature subtype yields correct descriptor shape
- [x] 1.2 Create `src/McpCad.Inventor/Helpers/FeatureReader.cs` — typed COM walker for ~20 PartFeature subtypes
- [x] 1.3 Add `Dictionary<string, object?> ReadFeatureData()` to `IMechanicalCadProvider` interface
- [x] 1.4 Implement `ReadFeatureData()` in `InventorProvider` — calls `FeatureReader.ReadFeatures(compDef)`
- [x] 1.5 Add `SetReadFeatureDataResult(...)` to `MockInventorProvider`
- [x] 1.6 GREEN: Pass FeatureReader tests — creational order, typed params, unsupported-type warnings

## Phase 2: features[] Dispatch (PR2)

- [x] 2.1 RED: Write MacroTools tests — dispatch order, empty fallback, per-entry isolation
- [x] 2.2 Create `FeatureDescriptor` DTO record with `[JsonPropertyName("snake_case")]` per existing convention
- [x] 2.3 Add `string? features` param to macro_god_part; parse JSON before single-feature switch
- [x] 2.4 Implement dispatch loop — iterate features[], call provider method per entry (extrude, fillet, hole, etc.)
- [x] 2.5 Skip global pattern_3d/modify_3d when features[] present; use per-entry scoped variants
- [x] 2.6 GREEN: Pass dispatch tests — mixed types, empty fallback, entry failure isolation

## Phase 3: Template Integration + E2E (PR3)

- [x] 3.1 RED: Write TemplateTools tests — capture produces features[], old template backward compat
- [x] 3.2 Wire `template_capture` — call ReadFeatureData() + GetFeatureTree(), emit features[] in macro_config
- [x] 3.3 Wire `template_run` — forward features[] to macro_god_part; overrides via Substitute
- [x] 3.4 RED: Write e2e test — capture 5-feature part (extrude+fillet+hole+pattern+chamfer), replay matches
- [x] 3.5 GREEN: Pass e2e — bounding box and feature count match between original and replayed
- [x] 3.6 GREEN: Old template without features[] produces identical pre-change output
