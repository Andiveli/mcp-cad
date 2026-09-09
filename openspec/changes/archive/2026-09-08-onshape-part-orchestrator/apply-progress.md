# Apply Progress: Onshape Part Orchestrator

**Change:** `onshape-part-orchestrator`  
**Unit:** Unit 1 — Typed closed-contract models  
**Status:** Complete — Unit 1 only

## Blocking condition

Native apply-attempt authority could not issue the required bounded attempt. `gentle-ai sdd-attempt acquire` requires the canonical untracked-file inventory, but its required read-only `gentle-ai review status --cwd <repo> --contract gentle-ai.review-integration/v2 --agent sdd-apply --next-transition` request was rejected with `immutable_review_transport_unsupported` for this runtime. Receipt-driven review is enabled globally; the CLI directed the parent to make the runtime eligible or deliberately disable receipt-driven review at the appropriate lifecycle scope. No source or test edits were made.

## Structured status consumed

```yaml
schemaName: gentle-ai.sdd-status
schemaVersion: 2
changeName: onshape-part-orchestrator
artifactStore: openspec
planningHome:
  root: /home/samael/Escritorio/Programacion/work/fisica/mcp-cad/openspec
changeRoot: /home/samael/Escritorio/Programacion/work/fisica/mcp-cad/openspec/changes/onshape-part-orchestrator
artifacts:
  proposal: done
  specs: done
  design: done
  tasks: done
  applyProgress: missing-at-start
  verifyReport: missing
taskProgress:
  total: 33
  complete: 1
  remaining: 32
applyState: ready
dependencies:
  apply: ready
  verify: blocked
  archive: blocked
actionContext:
  mode: repo-local
  workspaceRoot: /home/samael/Escritorio/Programacion/work/fisica/mcp-cad
  allowedEditRoots:
    - /home/samael/Escritorio/Programacion/work/fisica/mcp-cad
  warnings: []
nextRecommended: apply
```

## TDD Cycle Evidence

| Task | Test File | Layer | Safety Net | RED | GREEN | TRIANGULATE | REFACTOR |
|---|---|---|---|---|---|---|---|
| Unit 1 | `tests/McpCad.Tests/Tools/OnshapePartPlanModelTests.cs` | Unit | N/A — new file | Not started — blocked before edits | Not started | Not started | Not started |

## Work performed

- Read OpenSpec config, proposal, specification, design, tasks, and the absent prior progress artifact.
- Confirmed strict TDD and the focused test command from `openspec/config.yaml`.
- Confirmed the native status is authoritative and `applyState: ready`.
- Confirmed the parent-selected linear chained-review delivery path for Unit 1 and its 400-line cap.
- Made no live Onshape calls.

## Files changed

- `openspec/changes/onshape-part-orchestrator/apply-progress.md` (this blocked-progress record only)

## Tests

- Not run: the focused model test file does not yet exist, and apply-attempt authority blocked work before RED.

## Task checkbox updates

- None. No implementation-owned Unit 1 task was completed; `tasks.md` remains unchanged.

## Deviations from design

- None. No implementation began.

## Workload / PR boundary

- Assigned boundary: linear chained-review Unit 1 only.
- Source/test changed lines: 0 of 400 allowed.
- Do not begin Unit 2.

## Remaining implementation tasks

- [ ] RED: add model-contract tests for immutable plan/stage/profile records, closed enum discriminants, validated-plan ownership of the existing `CadElementAddress`, and exact result/status model fields in `tests/McpCad.Tests/Tools/OnshapePartPlanModelTests.cs`; confirm they fail because the contracts are absent. <!-- sdd-owner: implementation -->
- [ ] GREEN: add the internal immutable records/enums for parsed stages, profiles, validated plans, symbols, execution state, stage/capture results, errors, and summaries in `src/McpCad.Tools/Onshape/Orchestration/OnshapePartPlanModels.cs`; keep plan-local IDs distinct from native IDs and avoid mutable/global state. <!-- sdd-owner: implementation -->
- [ ] TRIANGULATE: extend the same model tests with empty/default collections, nullable native IDs/errors, all status combinations, and a mixed-profile input while asserting no serializer aliases or unsupported stage kinds are introduced. <!-- sdd-owner: implementation -->
- [ ] REFACTOR: simplify model constructors/collection ownership without widening visibility or changing the closed schema; run the focused model tests and confirm only the two listed files changed. <!-- sdd-owner: implementation -->

## Deferred parent lifecycle actions

- [ ] At every unit boundary, inspect changed-line estimates and require a further split before applying any unit predicted above 400 changed lines; do not infer a size exception. <!-- sdd-owner: parent -->
- [ ] Start or reuse a bounded review for the completed chain, checking exact file surfaces, RED/GREEN/TRIANGULATE/REFACTOR evidence, compatibility claims, and absence of live mutation. <!-- sdd-owner: parent -->
- [ ] Confirm the delivery diff excludes `.atl/`, `.pi/`, `.pi-subagents/`, source/test files outside the declared unit surfaces, credentials, and generated artifacts. <!-- sdd-owner: parent -->
- [ ] Confirm final net8 suite/build evidence and defer any commit, push, or PR lifecycle action to the parent workflow rather than executor tasks. <!-- sdd-owner: parent -->

## Unit 1 completion — subsequent authorized apply

The prior blocked-attempt record above is retained as historical context. The native handoff for this run authorized Unit 1 only; no parent-settlement token is recorded here.

### Structured status consumed

```yaml
artifactStore: openspec
changeName: onshape-part-orchestrator
changeRoot: openspec/changes/onshape-part-orchestrator
applyState: ready
actionContext:
  mode: repo-local
  workspaceRoot: /home/samael/Escritorio/Programacion/work/fisica/mcp-cad
  allowedEditRoots:
    - /home/samael/Escritorio/Programacion/work/fisica/mcp-cad
nextRecommended: apply
```

### Completed implementation tasks and checkbox evidence

- [x] RED: added reflection-based model-contract tests. The initial focused run failed as intended: 4 tests failed with `TypeLoadException` because the model contracts did not exist.
- [x] GREEN: added internal immutable parsed/validated plan, closed profile and stage unions, symbols, execution state, result summaries, statuses, and errors. Plan-local IDs are typed separately from native IDs; there is no provider call, serializer DTO alias, static cache, or mutable global state.
- [x] TRIANGULATE: added empty immutable collection, nullable native-ID/error, complete status-discriminant, mixed-profile boundary, and exact JSON-property-name coverage.
- [x] REFACTOR: centralized collection construction in the test helper and retained only the closed internal schema. The persisted Unit 1 checkboxes in `tasks.md` are visibly checked.

### TDD Cycle Evidence

| Cycle | Evidence |
|---|---|
| RED | `dotnet test tests/McpCad.Tests/McpCad.Tests.csproj --framework net8.0 --no-restore --filter 'FullyQualifiedName~OnshapePartPlanModelTests'` exited 1 before production code: 4 failures, each a `TypeLoadException` for absent model contracts. |
| GREEN | After adding `OnshapePartPlanModels.cs`, the focused command passed: 4/4 tests. |
| TRIANGULATE | After adding empty/default collection, nullable result, status, mixed-profile, and serialization-name cases, the focused command passed: 5/5 tests. |
| REFACTOR | The same focused command passed 5/5 after the constructor/collection helper cleanup. |

### Verification

- Focused command: `dotnet test tests/McpCad.Tests/McpCad.Tests.csproj --framework net8.0 --no-restore --filter 'FullyQualifiedName~OnshapePartPlanModelTests'` — passed, 5/5.
- Required runner: `dotnet test tests/McpCad.Tests/McpCad.Tests.csproj --framework net8.0 --no-restore` — passed, 503 passed / 7 skipped / 510 total.
- Existing nullable warnings in unrelated test files were emitted during focused compilation; no Unit 1 warning or failure was introduced.
- No live Onshape calls were made.

### Files changed

- `src/McpCad.Tools/Onshape/Orchestration/OnshapePartPlanModels.cs`
- `tests/McpCad.Tests/Tools/OnshapePartPlanModelTests.cs`
- `openspec/changes/onshape-part-orchestrator/tasks.md` (Unit 1 implementation checkbox updates)
- `openspec/changes/onshape-part-orchestrator/apply-progress.md` (this cumulative evidence)

### Workload / PR boundary

- Linear chained-review Unit 1 only; Unit 2 was not started.
- Source/test additions: 232 lines (102 production + 130 tests), under the 400-line budget and within Unit 1's 180–260 estimate.
- No design deviation: output records use exact JSON property names; actual result projection/execution remains deferred to its assigned units.

### Remaining implementation tasks (persisted exact unchecked lines)

- [ ] RED: write failing parser tests for valid two-string address/plan roots, exact member allowlists, duplicate members at root/stage/profile/point/segment depth, missing members, wrong JSON kinds, malformed JSON, non-integer version, numeric strings, nulls, non-finite values, exact case-sensitive discriminants, and useful JSON paths in `tests/McpCad.Tests/Tools/OnshapePartPlanParserTests.cs`. <!-- sdd-owner: implementation -->
- [ ] GREEN: implement `OnshapePartPlanParser` in `src/McpCad.Tools/Onshape/Orchestration/OnshapePartPlanParser.cs` using strict `JsonDocument` enumeration, ordinal duplicate/unknown checks, required-member checks, exact `JsonValueKind` checks, and explicit switches for sketch/extrude/checkpoint plus rectangle/circle/polygon/mixed forms. Do not call the provider. <!-- sdd-owner: implementation -->
- [ ] TRIANGULATE: add parser cases for optional `microversionId`, repeated closing polygon points, derived-start mixed segments, forbidden segment `start`/`mid`, whitespace/empty strings, all unsupported aliases, and late malformed stages; assert parsing rejects the whole request before any fake provider could be touched. <!-- sdd-owner: implementation -->
- [ ] REFACTOR: extract only local parser helpers that preserve exact allowlists and error paths; run the focused parser tests and the model tests with no behavior change. <!-- sdd-owner: implementation -->
- [ ] RED: add failing validator tests for schema version, 1–16 stage bounds, 0–3 checkpoint bounds, case-sensitive unique IDs, backward/typed references, latest-preceding checkpoint targets, canonical planes, finite/nonzero extrude values, and single-body-result assumption messaging. <!-- sdd-owner: implementation -->
- [ ] GREEN: implement complete preflight in `src/McpCad.Tools/Onshape/Orchestration/OnshapePartPlanValidator.cs`: address validation, symbol-table construction, reference checks, limits, geometry checks, centimeter-to-meter conversion exactly once, and disposal-safe compilation through `OnshapeSketchBuilder`, `OnshapeMixedProfileFacade`, and `OnshapeFeatureTemplates` without provider reads/writes/captures. <!-- sdd-owner: implementation -->
- [ ] TRIANGULATE: cover rectangle/circle/polygon/mixed boundaries, one repeated polygon closing point, invalid topology, 1/8/9 arc limits, unsupported planes/operations/directions, stale checkpoints, and a provider fake that throws if validation invokes it. <!-- sdd-owner: implementation -->
- [ ] REFACTOR: consolidate validation errors and immutable compiled payload ownership while retaining stable error codes (`INVALID_*`, `PLAN_LIMIT_EXCEEDED`, `INVALID_REFERENCE`) and preserving existing builder contracts; rerun parser, validator, and model tests. <!-- sdd-owner: implementation -->
- [ ] RED: add failing recording-provider tests for declaration order, native sketch ID binding, top-level and nested feature-ID response shapes, meter/depth/direction/operation linkage, first modeling failure stop, later modeling `not_attempted` results, checkpoint continuation, no retries, and no rollback/deletion calls. <!-- sdd-owner: implementation -->
- [ ] GREEN: implement `OnshapeFeatureWriteResult` and `OnshapePartPlanExecutor` in the two production files; call only `SketchReplace`, `FeatureAdd`, and `CaptureView`, resolve only successful prior native IDs, stop modeling after the first modeling failure, continue after capture failure, and catch active-stage exceptions with stable contextual errors. <!-- sdd-owner: implementation -->
- [ ] TRIANGULATE: add tests for missing native IDs, false provider results, thrown exceptions, blank/invalid capture base64, capture image ordering, modeling failure before a checkpoint, repeated checkpoints at the same latest model stage, and ambiguous timeout/microversion errors without retry. <!-- sdd-owner: implementation -->
- [ ] REFACTOR: isolate local execution state and response normalization helpers, preserve opaque native IDs exactly, and rerun executor plus validator/parser/model tests after each extraction. <!-- sdd-owner: implementation -->
- [ ] RED: write failing result tests for exact structured keys, schema version, ordered one-per-stage results, null rules, status precedence, modeling/capture separation, captures `none/completed/partial_failure`, evidence/error placement, `IsError`, and one PNG image block per successful checkpoint in declaration order. <!-- sdd-owner: implementation -->
- [ ] GREEN: implement `OnshapeOrchestrationResultBuilder` in the exact production path to project typed execution state into the specified `StructuredContent`, ordered `ImageContentBlock.FromBytes` content, and preflight/error envelopes without leaking provider dictionaries into stage results. <!-- sdd-owner: implementation -->
- [ ] TRIANGULATE: cover completed, capture-only failure, modeling failure with skipped later stages, no checkpoints, invalid image evidence, nested capture metadata, and successful modeling with no native ID; compare normalized JSON property sets rather than incidental serialization order. <!-- sdd-owner: implementation -->
- [ ] REFACTOR: centralize status/error projection and retain the exact field sets from the specification; run result, executor, validator, parser, and model tests together. <!-- sdd-owner: implementation -->
- [ ] RED: add failing adapter tests for the exact two-string signature, parser/validator/executor/builder composition, preflight error projection, provider injection, and zero provider calls for invalid late plans; add registration assertions for exactly one `onshape_orchestrate_part` name and unchanged `skill_onshape_god_part`. <!-- sdd-owner: implementation -->
- [ ] GREEN: add only the partial `OnshapeSkillTools` method in `src/McpCad.Tools/Onshape/OnshapePartOrchestratorTool.cs` with `[McpServerTool]` and the exact `CallToolResult onshape_orchestrate_part(string address_json, string plan_json)` signature; do not edit `OnshapeSkillTools.cs`, `OnshapeGodPart.cs`, `OnshapeGodPartCapture.cs`, or `McpCad.Server/Program.cs`. <!-- sdd-owner: implementation -->
- [ ] TRIANGULATE: test malformed address/plan, successful typed execution, capture-only failure, partial modeling failure, exact reflected parameter types, no internal MCP calls, and registration collision behavior alongside existing god-part tests. <!-- sdd-owner: implementation -->
- [ ] REFACTOR: keep adapter responsibilities limited to parse, preflight, execute, and project; rerun the complete focused Tools suite and confirm no existing tool name/schema changed. <!-- sdd-owner: implementation -->
- [ ] RED: add failing WireMock scenarios for feature POST ordering, sketch payloads, centimeter-to-meter conversion, native sketch ID in `entities.queries[0].featureId`, nested/top-level response normalization, checkpoint shaded-view timing, PNG content, and capture failure continuation; use only local WireMock routes and fixtures. <!-- sdd-owner: implementation -->
- [ ] GREEN: implement the WireMock/provider conformance test and update `docs/providers.md` plus `docs/onshape-parity-matrix.md` with the new provider/tool entry, canonical planes/profiles, single-body-result assumption, non-idempotent fresh/known-Part-Studio requirement, one-body-result limitation, ordered checkpoint/capture semantics, partial failure/no rollback/no retry behavior, and image-as-evidence boundary. <!-- sdd-owner: implementation -->
- [ ] TRIANGULATE: add alternate response shapes and transport failure cases, assert no live endpoint/credential dependency, verify WireMock request order and exact payload linkage, and confirm documentation does not imply rollback, idempotent replay, topology guarantees, or drawing inference. <!-- sdd-owner: implementation -->
- [ ] REFACTOR: keep docs terminology aligned with result codes/statuses and run the full net8 suite and solution build after all refactors. <!-- sdd-owner: implementation -->

## Unit 2 completion — Strict address and plan parser

**Status:** Complete — Unit 2 only. The prior unchecked Unit 2 list above is historical; all four Unit 2 implementation checkboxes are now visibly checked in `tasks.md`.

### Structured status consumed

```yaml
artifactStore: openspec
changeName: onshape-part-orchestrator
applyState: ready
actionContext:
  mode: repo-local
  workspaceRoot: /home/samael/Escritorio/Programacion/work/fisica/mcp-cad
  allowedEditRoots:
    - /home/samael/Escritorio/Programacion/work/fisica/mcp-cad
warnings: []
nextRecommended: apply
```

### Completed tasks and evidence

- [x] RED: added reflection-based parser tests; the first focused run failed (25 failures) because `OnshapePartPlanParser` did not exist.
- [x] GREEN: added a pure `JsonDocument` parser for exact address/plan/stage/profile/point/segment shapes, ordinal duplicate/unknown detection, exact token kinds, closed discriminants, finite numbers, and contextual paths. It contains no provider dependency or call.
- [x] TRIANGULATE: covered optional microversion, repeated polygon closure preservation for validator normalization, derived mixed starts, forbidden segment members, whitespace, aliases, malformed late stages, and all closed forms.
- [x] REFACTOR: retained only local parsing helpers; removed redundant test parsing and reran parser plus model tests.

### TDD Cycle Evidence

| Task | Test file | Layer | Safety net | RED | GREEN | TRIANGULATE | REFACTOR |
|---|---|---|---|---|---|---|---|
| Unit 2 | `tests/McpCad.Tests/Tools/OnshapePartPlanParserTests.cs` | Unit | N/A (new file) | 25 failures; parser type absent | 26/26 passed | 28/28 passed | parser + model: 33/33 passed |

### Verification

- `dotnet test tests/McpCad.Tests/McpCad.Tests.csproj --framework net8.0 --no-restore --filter 'FullyQualifiedName~OnshapePartPlanParserTests'` — RED exited 1, then GREEN 26/26 and TRIANGULATE 28/28 passed.
- `dotnet test tests/McpCad.Tests/McpCad.Tests.csproj --framework net8.0 --no-restore --filter 'FullyQualifiedName~OnshapePartPlanParserTests|FullyQualifiedName~OnshapePartPlanModelTests'` — passed 33/33 after refactor.
- Existing nullable warnings occurred in unrelated test files; no Unit 2 warning/failure was introduced. No live Onshape or provider call was made.

### Files changed

- `src/McpCad.Tools/Onshape/Orchestration/OnshapePartPlanParser.cs`
- `tests/McpCad.Tests/Tools/OnshapePartPlanParserTests.cs`
- `openspec/changes/onshape-part-orchestrator/tasks.md` (Unit 2 checkbox updates)
- `openspec/changes/onshape-part-orchestrator/apply-progress.md` (this cumulative evidence)

### Workload / PR boundary

- Linear chained-review Unit 2 only; Unit 3 was not started.
- Source/test additions: 340 lines, within the 280–380 Unit 2 estimate and below the 400-line hard cap.
- No design deviation. Parser preserves repeated polygon closure for Unit 3 geometry normalization and rejects the entire raw input before any execution boundary can be reached.

### Remaining assigned boundary

- [ ] RED: add failing validator tests for schema version, 1–16 stage bounds, 0–3 checkpoint bounds, case-sensitive unique IDs, backward/typed references, latest-preceding checkpoint targets, canonical planes, finite/nonzero extrude values, and single-body-result assumption messaging. <!-- sdd-owner: implementation -->
- [ ] GREEN: implement complete preflight in `src/McpCad.Tools/Onshape/Orchestration/OnshapePartPlanValidator.cs`: address validation, symbol-table construction, reference checks, limits, geometry checks, centimeter-to-meter conversion exactly once, and disposal-safe compilation through `OnshapeSketchBuilder`, `OnshapeMixedProfileFacade`, and `OnshapeFeatureTemplates` without provider reads/writes/captures. <!-- sdd-owner: implementation -->
- [ ] TRIANGULATE: cover rectangle/circle/polygon/mixed boundaries, one repeated polygon closing point, invalid topology, 1/8/9 arc limits, unsupported planes/operations/directions, stale checkpoints, and a provider fake that throws if validation invokes it. <!-- sdd-owner: implementation -->
- [ ] REFACTOR: consolidate validation errors and immutable compiled payload ownership while retaining stable error codes (`INVALID_*`, `PLAN_LIMIT_EXCEEDED`, `INVALID_REFERENCE`) and preserving existing builder contracts; rerun parser, validator, and model tests. <!-- sdd-owner: implementation -->

## Unit 3 completion — Complete preflight validation and sketch compilation

**Status:** Complete — Unit 3 only. Native status consumed: authoritative OpenSpec, `applyState: ready`, `nextRecommended: apply`; `actionContext.mode: repo-local`, workspace/allowed root `/home/samael/Escritorio/Programacion/work/fisica/mcp-cad`, warnings `[]`.

### Completed tasks and checkbox evidence

- [x] RED: added validator tests; executable RED failed 14/14 because `OnshapePartPlanValidator` was absent.
- [x] GREEN: added provider-free preflight for address/schema/limits, ordinal symbols, typed backward references, latest checkpoint targets, stable validation codes, and owned JSON sketch payloads.
- [x] TRIANGULATE: covered supported profiles/planes, repeated polygon closure, invalid geometry, one/eight/nine arcs, depth preservation, and the single-body-result assumption message.
- [x] REFACTOR: retained local helpers and immutable payload strings; Unit 3 persisted checkboxes are checked.

### TDD Cycle Evidence

| Task | Test file | Layer | Safety net | RED | GREEN | TRIANGULATE | REFACTOR |
|---|---|---|---|---|---|---|---|
| Unit 3 | `tests/McpCad.Tests/Tools/OnshapePartPlanValidatorTests.cs` | Unit | N/A (new file) | 14 failures (`TypeLoadException`) | 14/14 passed | 15/15 passed | validator + parser + model: 48/48 passed |

### Verification

- `dotnet test tests/McpCad.Tests/McpCad.Tests.csproj --framework net8.0 --no-restore --filter 'FullyQualifiedName~OnshapePartPlanValidatorTests'` — RED failed 14/14, final pass 15/15.
- `dotnet test tests/McpCad.Tests/McpCad.Tests.csproj --framework net8.0 --no-restore --filter 'FullyQualifiedName~OnshapePartPlanValidatorTests|FullyQualifiedName~OnshapePartPlanParserTests|FullyQualifiedName~OnshapePartPlanModelTests'` — passed 48/48.
- `dotnet test tests/McpCad.Tests/McpCad.Tests.csproj --framework net8.0 --no-restore` — passed 546, skipped 7, total 553.
- Existing unrelated nullable warnings were emitted; no Unit 3 warning/failure. No provider or live Onshape call occurred.

### Files changed

- `src/McpCad.Tools/Onshape/Orchestration/OnshapePartPlanValidator.cs`
- `tests/McpCad.Tests/Tools/OnshapePartPlanValidatorTests.cs`
- `openspec/changes/onshape-part-orchestrator/tasks.md`
- `openspec/changes/onshape-part-orchestrator/apply-progress.md`

### Workload / deviations / remaining work

- Linear chained-review Unit 3 only; Unit 4 was not started. Source/test footprint: 242 lines, below the 400-line cap.
- Validated extrudes retain `DepthCm`; sketches convert centimeters once at their builder boundary. Native-ID-dependent `OnshapeFeatureTemplates.ExtrudeFromSketch` construction remains Unit 4, preventing a second depth conversion during preflight.
- Next exact unchecked implementation task: `- [ ] RED: add failing recording-provider tests for declaration order, native sketch ID binding, top-level and nested feature-ID response shapes, meter/depth/direction/operation linkage, first modeling failure stop, later modeling `not_attempted` results, checkpoint continuation, no retries, and no rollback/deletion calls. <!-- sdd-owner: implementation -->`

### Deferred parent lifecycle actions

- Parent owns review, receipts, delivery gates, commits, pushes, and PR decisions.

## Unit 4 completion — Sequential executor and native feature-ID normalization

**Status:** Complete — Unit 4 only. Native status consumed: authoritative OpenSpec, `applyState: ready`, `nextRecommended: apply`; `actionContext.mode: repo-local`, allowed root `/home/samael/Escritorio/Programacion/work/fisica/mcp-cad`, warnings `[]`.

### Completed tasks and checkbox evidence

- [x] RED: recording-provider executor tests initially failed 4/4 because `OnshapePartPlanExecutor` was absent.
- [x] GREEN: added per-invocation sequential execution and feature-write normalization using only `SketchReplace`, `FeatureAdd`, and `CaptureView`; native IDs remain opaque and `DepthCm` converts only at the feature-template boundary.
- [x] TRIANGULATE: covered top-level/nested IDs, missing IDs, false/exception/timeout results, blank/invalid capture base64, repeated checkpoints, declaration ordering, stop/continue behavior, and no retry/rollback surface.
- [x] REFACTOR: retained only local execution state/normalization helpers. The four Unit 4 implementation checkboxes are visibly checked in `tasks.md`.

### TDD Cycle Evidence

| Cycle | Evidence |
|---|---|
| RED | Executor-focused command exited 1: 4 failures (`TypeLoadException`, executor absent). |
| GREEN | After production implementation, executor-focused command passed 4/4. |
| TRIANGULATE | After blank/invalid-base64 and repeated-checkpoint coverage, executor-focused command passed 4/4. |
| REFACTOR | Executor + validator + parser + model filter passed 52/52. |

### Verification and boundary

- `dotnet test tests/McpCad.Tests/McpCad.Tests.csproj --framework net8.0 --no-restore --filter 'FullyQualifiedName~OnshapePartPlanExecutorTests'` — passed 4/4.
- `dotnet test tests/McpCad.Tests/McpCad.Tests.csproj --framework net8.0 --no-restore --filter 'FullyQualifiedName~OnshapePartPlanExecutorTests|FullyQualifiedName~OnshapePartPlanValidatorTests|FullyQualifiedName~OnshapePartPlanParserTests|FullyQualifiedName~OnshapePartPlanModelTests'` — passed 52/52.
- Required runner: `dotnet test tests/McpCad.Tests/McpCad.Tests.csproj --framework net8.0 --no-restore` — passed 550, skipped 7, total 557. Existing unrelated nullable warnings only; no live Onshape calls.
- Files: `OnshapeFeatureWriteResult.cs`, `OnshapePartPlanExecutor.cs`, `OnshapePartPlanExecutorTests.cs`, plus persisted task/progress artifacts. Source/test footprint: 333 lines, below the 400-line cap. No design deviation. Unit 5 was not started.

### Remaining assigned work

- [ ] RED: write failing result tests for exact structured keys, schema version, ordered one-per-stage results, null rules, status precedence, modeling/capture separation, captures `none/completed/partial_failure`, evidence/error placement, `IsError`, and one PNG image block per successful checkpoint in declaration order. <!-- sdd-owner: implementation -->
- [ ] GREEN: implement `OnshapeOrchestrationResultBuilder` in the exact production path to project typed execution state into the specified `StructuredContent`, ordered `ImageContentBlock.FromBytes` content, and preflight/error envelopes without leaking provider dictionaries into stage results. <!-- sdd-owner: implementation -->
- [ ] TRIANGULATE: cover completed, capture-only failure, modeling failure with skipped later stages, no checkpoints, invalid image evidence, nested capture metadata, and successful modeling with no native ID; compare normalized JSON property sets rather than incidental serialization order. <!-- sdd-owner: implementation -->
- [ ] REFACTOR: centralize status/error projection and retain the exact field sets from the specification; run result, executor, validator, parser, and model tests together. <!-- sdd-owner: implementation -->

### Deferred parent lifecycle actions

- Parent owns review, receipts, delivery gates, commits, pushes, and PR decisions.

## Unit 5 completion — Exact result envelope and MCP content projection

**Status:** Complete — Unit 5 only. The explicit change selection resolved the parent status's prior change-selection ambiguity. Consumed action context: `repo-local`; authoritative workspace and allowed root: `/home/samael/Escritorio/Programacion/work/fisica/mcp-cad`; warnings: `[]`.

### Completed tasks and checkbox evidence

- [x] RED: added execution-result projection tests; focused command failed 4/4 with `TypeLoadException` because `OnshapeOrchestrationResultBuilder` was absent, then the dedicated preflight envelope test failed because `PreflightError` was absent.
- [x] GREEN: added the internal builder with exact version-1 structured envelope, snake_case enum projection, status precedence, separated modeling/capture summaries, error preflight envelope, and ordered PNG blocks only for successful captures.
- [x] TRIANGULATE: covered completed, capture-only failure, invalid image evidence, nested capture metadata, no checkpoints, missing native ID, and a modeling failure whose following checkpoint is explicitly `skipped` / `captureStatus: not_attempted` with no capture result or image.
- [x] REFACTOR: extracted overall-status projection from capture-summary calculation and made serialization options per-call to avoid mutable global state; exact result field sets and typed stage-only projection remain unchanged. The four Unit 5 implementation checkboxes are visibly checked in `tasks.md`.

### TDD Cycle Evidence

| Cycle | Evidence |
|---|---|
| RED | `dotnet test tests/McpCad.Tests/McpCad.Tests.csproj --framework net8.0 --no-restore --filter 'FullyQualifiedName~OnshapePartOrchestratorResultTests'` exited 1: 4 failures because the builder type was absent. |
| GREEN | Focused command passed 4/4 after the minimal builder implementation; the preflight-envelope GREEN rerun passed 6/6. |
| TRIANGULATE | Added successful modeling with no checkpoints, then preflight error projection; focused command passed 6/6. |
| REFACTOR | After extracting status precedence and per-call serializer options, the result + dependent focused filter passed 58/58. |

### Verification and boundary

- Result + executor + validator + parser + model filter passed 58/58.
- Required runner `dotnet test tests/McpCad.Tests/McpCad.Tests.csproj --framework net8.0 --no-restore` passed 556, skipped 7, total 563. Existing unrelated nullable warnings only; no live Onshape call.
- Files changed: `src/McpCad.Tools/Onshape/Orchestration/OnshapeOrchestrationResultBuilder.cs`, `tests/McpCad.Tests/Tools/OnshapePartOrchestratorResultTests.cs`, and these allowed OpenSpec artifacts.
- Source/test footprint: 279 added lines, below the Unit 5 400-line cap. No design deviation. Unit 6 was not started.

### Remaining assigned work

- [ ] RED: add failing adapter tests for the exact two-string signature, parser/validator/executor/builder composition, preflight error projection, provider injection, and zero provider calls for invalid late plans; add registration assertions for exactly one `onshape_orchestrate_part` name and unchanged `skill_onshape_god_part`. <!-- sdd-owner: implementation -->

### Deferred parent lifecycle actions

- Parent owns review, receipts, delivery gates, commits, pushes, and PR decisions.

## Unit 6 completion — Additive MCP adapter and registration compatibility

**Status:** Complete — Unit 6 only. Consumed parent-authoritative status: `changeName: onshape-part-orchestrator`, `applyState: ready`, `dependencies.apply: ready`, `actionContext.mode: repo-local`; workspace/allowed root `/home/samael/Escritorio/Programacion/work/fisica/mcp-cad`; warnings `[]`.

### Completed tasks and checkbox evidence

- [x] RED: adapter and registration tests failed as intended before the method existed: five failures (missing reflected method / zero registrations).
- [x] GREEN: added the only new partial `OnshapeSkillTools` adapter. It parses, validates, executes against its constructor-injected provider, and projects through the existing result builder; it makes no internal MCP call.
- [x] TRIANGULATE: covered malformed address and late invalid plan with zero provider calls, successful execution, capture-only failure, first-modeling-failure stop, exact reflection signature, one registration, and retained god-part name.
- [x] REFACTOR: retained the four-step adapter boundary; no production refactor was needed. Unit 6 checkboxes are visibly checked in `tasks.md`.

### TDD Cycle Evidence

| Task | Test file | Layer | Safety net | RED | GREEN | TRIANGULATE | REFACTOR |
|---|---|---|---|---|---|---|---|
| Unit 6 | `tests/McpCad.Tests/Tools/OnshapePartOrchestratorToolTests.cs`, `ToolRegistrationCollisionTests.cs` | Unit | registration baseline 2/2 | 5 failures | 7/7 passed | 8/8 passed | full runner green |

### Verification and boundary

- Focused adapter/registration filter: RED failed 5 tests; GREEN passed 7/7 and triangulation passed 8/8. A first GREEN rerun hit the test-host's 90-second protocol timeout; the identical command with `VSTEST_CONNECTION_TIMEOUT=180` passed.
- Required runner: `dotnet test tests/McpCad.Tests/McpCad.Tests.csproj --framework net8.0 --no-restore` (with `VSTEST_CONNECTION_TIMEOUT=180`) — passed 562, skipped 7, total 569. Existing unrelated nullable warnings only; no live Onshape call.
- Files changed: `src/McpCad.Tools/Onshape/OnshapePartOrchestratorTool.cs`, `tests/McpCad.Tests/Tools/OnshapePartOrchestratorToolTests.cs`, `tests/McpCad.Tests/Tools/ToolRegistrationCollisionTests.cs`, and these allowed OpenSpec artifacts. The source/test footprint is 137 lines, under the 400-line cap. No design deviation; protected god-part, provider, server, and project files remain untouched.
- PR boundary: linear chained-review Unit 6 only. Unit 7 was not started.

### Remaining implementation tasks (exact persisted unchecked Unit 7 lines)

- [ ] RED: add failing WireMock scenarios for feature POST ordering, sketch payloads, centimeter-to-meter conversion, native sketch ID in `entities.queries[0].featureId`, nested/top-level response normalization, checkpoint shaded-view timing, PNG content, and capture failure continuation; use only local WireMock routes and fixtures. <!-- sdd-owner: implementation -->
- [ ] GREEN: implement the WireMock/provider conformance test and update `docs/providers.md` plus `docs/onshape-parity-matrix.md` with the new provider/tool entry, canonical planes/profiles, single-body-result assumption, non-idempotent fresh/known-Part-Studio requirement, one-body-result limitation, ordered checkpoint/capture semantics, partial failure/no rollback/no retry behavior, and image-as-evidence boundary. <!-- sdd-owner: implementation -->
- [ ] TRIANGULATE: add alternate response shapes and transport failure cases, assert no live endpoint/credential dependency, verify WireMock request order and exact payload linkage, and confirm documentation does not imply rollback, idempotent replay, topology guarantees, or drawing inference. <!-- sdd-owner: implementation -->
- [ ] REFACTOR: keep docs terminology aligned with result codes/statuses and run the full net8 suite and solution build after all refactors. <!-- sdd-owner: implementation -->

### Deferred parent lifecycle actions

- Parent owns review, receipts, delivery gates, commits, pushes, and PR decisions.

## Unit 7 completion — REST conformance, documentation, and compatibility gate

**Status:** Complete — final implementation unit only. The parent explicitly selected `onshape-part-orchestrator`, resolving the native status's earlier selection ambiguity. The parent-issued proceed token is settlement-owned and is intentionally not persisted here.

### Structured status consumed

```yaml
artifactStore: openspec
changeName: onshape-part-orchestrator
applyState: ready (explicit parent selection/proceed override)
actionContext:
  mode: repo-local
  workspaceRoot: /home/samael/Escritorio/Programacion/work/fisica/mcp-cad
  allowedEditRoots:
    - /home/samael/Escritorio/Programacion/work/fisica/mcp-cad
warnings: []
nextRecommended: parent-lifecycle
```

### Completed tasks and checkbox evidence

- [x] RED: added only local WireMock fixtures for nested/top-level native IDs, POST → shaded-view GET → POST timing, rectangle cm→m payloads, native sketch linkage, PNG content, and capture-failure continuation.
- [x] GREEN: added the bounded provider/tool and parity documentation; no production, provider-contract, server, god-macro, project, live endpoint, or credential changes were made.
- [x] TRIANGULATE: covered both native response shapes and a local HTTP 503 capture transport failure while proving later modeling continues; docs assert canonical forms, statuses, and safety boundaries.
- [x] REFACTOR: kept the isolated fixture test nullable-safe and documentation terminology aligned with `completed_with_capture_failures` and `partial_modeling_failure`. The four persisted Unit 7 checkboxes are visibly checked in `tasks.md`.

### TDD Cycle Evidence

| Cycle | Evidence |
|---|---|
| RED | The new focused test first failed to compile because its initial WireMock response helper used an unavailable response type; after correcting that test-only fixture declaration, it failed 1/3 because the required provider documentation was absent. No production code was written. |
| GREEN | After the initial documentation and test completion, `OnshapePartOrchestratorWireMockTests` passed 3/3. |
| TRIANGULATE | Added shaded-view 800×600/`show` assertions and explicit result-status documentation assertions; this intentionally failed the docs test until both documentation files named the statuses, then passed 3/3. |
| REFACTOR | Focused conformance suite remained green; the required full net8 test runner passed 565 / 7 skipped / 572 total. |

### Verification

- `dotnet test tests/McpCad.Tests/McpCad.Tests.csproj --framework net8.0 --no-restore --filter 'FullyQualifiedName~OnshapePartOrchestratorWireMockTests'` — final pass, 3/3.
- `dotnet test tests/McpCad.Tests/McpCad.Tests.csproj --framework net8.0 --no-restore` — passed 565, skipped 7, total 572; existing unrelated nullable warnings only.
- `dotnet build src/mcp-cad.sln` — failed outside this unit because the Linux host lacks Autodesk Inventor interop references (`Inventor` namespace / `Application` errors in `src/McpCad.Inventor`); Onshape tools, server, and tests built before that external-platform failure.
- No live Onshape endpoint, credential, or mutation was used; every route was a local `WireMockServer` URL with fixture credential strings.

### Files changed

- `tests/McpCad.Tests/Onshape/OnshapePartOrchestratorWireMockTests.cs`
- `docs/providers.md`
- `docs/onshape-parity-matrix.md`
- `openspec/changes/onshape-part-orchestrator/tasks.md` (Unit 7 checkboxes only)
- `openspec/changes/onshape-part-orchestrator/apply-progress.md` (this cumulative evidence)

### Workload / deviations / remaining work

- Linear chained-review Unit 7 only; test plus documentation additions are 135 lines, under the hard 400-line cap. No production code was needed or changed.
- No design deviation. The 503 fixture may be retried by the pre-existing HTTP transport before it returns to the executor; the test asserts the required stage ordering and continuation and makes no claim that the orchestrator adds a retry policy.
- No implementation-owned task remains unchecked.
- Deferred parent lifecycle actions (persisted unchanged):
  - [ ] At every unit boundary, inspect changed-line estimates and require a further split before applying any unit predicted above 400 changed lines; do not infer a size exception. <!-- sdd-owner: parent -->
  - [ ] Start or reuse a bounded review for the completed chain, checking exact file surfaces, RED/GREEN/TRIANGULATE/REFACTOR evidence, compatibility claims, and absence of live mutation. <!-- sdd-owner: parent -->
  - [ ] Confirm the delivery diff excludes `.atl/`, `.pi/`, `.pi-subagents/`, source/test files outside the declared unit surfaces, credentials, and generated artifacts. <!-- sdd-owner: parent -->
  - [ ] Confirm final net8 suite/build evidence and defer any commit, push, or PR lifecycle action to the parent workflow rather than executor tasks. <!-- sdd-owner: parent -->
