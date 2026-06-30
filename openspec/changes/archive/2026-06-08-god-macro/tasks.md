# Tasks: God Macro — Full-Workflow Single-Call Macro

## Review Workload Forecast

| Field | Value |
|-------|-------|
| Estimated changed lines | ~1000-1300 |
| 400-line budget risk | High |
| Chained PRs recommended | Yes |
| Suggested split | PR 1 (Foundation) → PR 2 (Core) → PR 3 (Tests) |
| Delivery strategy | force-chained |
| Chain strategy | pending |

Decision needed before apply: Yes
Chained PRs recommended: Yes
Chain strategy: pending
400-line budget risk: High

### Suggested Work Units

| Unit | Goal | Likely PR | Notes |
|------|------|-----------|-------|
| 1 | JSON models + parsing + ToolHelpers | PR 1 | Base for PR 2; unit-testable alone |
| 2 | Main `macro_god_part` method + all phases | PR 2 | Depends on PR 1; the heavy lift |
| 3 | Tests for JSON, phase composition, partial failure | PR 3 | Depends on PR 2 |

## Phase 1: Foundation — Models & Parsing (PR 1 candidate)

- [x] 1.1 Add `SketchEntity`, `SketchConstraint`, `SketchDimension` DTOs to MacroTools.cs
- [x] 1.2 Add `MacroPhaseStatus` envelope types (per-phase result dicts)
- [x] 1.3 Add `Success()` and `Merge()` helpers to ToolHelpers.cs
- [x] 1.4 Implement `ParseSketchJson()` with System.Text.Json and validation messages
- [x] 1.5 Implement polygon line-generation helper (N-sided from cx,cy,radius)

## Phase 2: Core Macro — All Phase Composition (PR 2 candidate)

- [x] 2.1 Declare `macro_god_part` with `[McpServerTool]` and 25-30 `[Description]` params
- [x] 2.2 Implement `ask_before_modify`: Health() + GetFeatureTree() → confirmation envelope
- [x] 2.3 Sketch phase: create plane sketch → iterate entities → constraints → dimensions + sketch_modify + sketch_pattern
- [x] 2.4 Feature phase: type dispatch (extrude/revolve/sweep/loft/coil/rib) with operation
- [x] 2.5 Pattern phase: circular/rectangular/mirror delegation (sketch + 3D)
- [x] 2.6 Modify phase: fillet/chamfer/shell/draft/thread/split delegation (sketch + 3D)
- [x] 2.7 Verify phase: GetFeatureTree() + GetBoundingBox() + params + viewport capture (Iso+Top)
- [x] 2.8 Compose envelope: per-phase try-catch via Catch/IsSuccess → phase_status + geometry_created + warnings + iProperties

## Phase 3: Registration (in PR 2)

- [x] 3.1 `[McpServerToolType]` on class (pre-existing from PR 1), `[McpServerTool]` on method (self-registering)
- [x] 3.2 All 25+ params + method have rich `[Description]` attributes; build clean

## Phase 4: Testing (PR 3 candidate)

- [x] 4.1 Test valid/malformed JSON parsing returns correct phase_status
- [x] 4.2 Test ask_before_modify with 0/1/5 features returns confirmation envelope
- [x] 4.3 Test per-phase partial failure: sketch OK → feature fails → envelope shows partial
- [x] 4.4 Test full pipeline: rect+circle → extrude → fillet → verify tree+bbox+image
- [x] 4.5 Test polygon generates correct N-sided line set
