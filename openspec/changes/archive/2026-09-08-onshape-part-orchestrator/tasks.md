# Implementation Tasks: Onshape Part Orchestrator

## Review Workload Forecast

| Field | Value |
|-------|-------|
| Estimated changed lines | 1,650–2,250 total, delivered as 7 units of approximately 180–380 lines each |
| 400-line budget risk | High |
| Chained PRs recommended | Yes |
| Suggested split | Unit 1 → Unit 2 → Unit 3 → Unit 4 → Unit 5 → Unit 6 → Unit 7 |
| Delivery strategy | ask-on-risk (resolved: chained review units) |
| Chain strategy | linear, Unit 1 → Unit 2 → Unit 3 → Unit 4 → Unit 5 → Unit 6 → Unit 7 |

Decision needed before apply: No — resolved by the user
Chained PRs recommended: Yes
Chain strategy: linear review units
400-line budget risk: High, bounded per unit

The 1,650–2,250-line forecast exceeds the 400-line review budget. The user selected chained review units. The change remains split by behavior into 7 bounded linear units; no unit may exceed 400 changed lines. Tests remain with the implementation they verify. Commit, push, and PR actions still require separate explicit authorization.

## Parent-owned pre-apply delivery decision

- [x] Selected policy: `chained review units`, in linear Unit 1 → Unit 7 order. No `size:exception` is authorized. <!-- sdd-owner: parent -->

## Global execution guardrails

- All implementation and verification uses xUnit, Moq/provider recordings, golden payloads, or WireMock.Net; no live Onshape mutation is permitted without separate explicit authorization.
- Preserve `skill_onshape_god_part`, `OnshapeGodPartCapture`, `ICadWebProvider`, provider authentication, synchronous writes, unit conversion, microversion-skew rejection, and existing builder/template contracts.
- Do not add or deliver `.atl/`, `.pi/`, or `.pi-subagents/` metadata.
- Every unit must leave its covered behavior compilable, testable, and reasonably revertible without unrelated source changes.

## Dependency-ordered implementation units

### Unit 1 — Typed closed-contract models

**Depends on:** none. **Changed-line cap:** 180–260.

**Exact file surface:**
- `src/McpCad.Tools/Onshape/Orchestration/OnshapePartPlanModels.cs`
- `tests/McpCad.Tests/Tools/OnshapePartPlanModelTests.cs`

**Start:** Existing Onshape tools remain untouched; no orchestrator model types exist.

**Tasks (strict RED → GREEN → TRIANGULATE → REFACTOR):**

- [x] RED: add model-contract tests for immutable plan/stage/profile records, closed enum discriminants, validated-plan ownership of the existing `CadElementAddress`, and exact result/status model fields in `tests/McpCad.Tests/Tools/OnshapePartPlanModelTests.cs`; confirm they fail because the contracts are absent. <!-- sdd-owner: implementation -->
- [x] GREEN: add the internal immutable records/enums for parsed stages, profiles, validated plans, symbols, execution state, stage/capture results, errors, and summaries in `src/McpCad.Tools/Onshape/Orchestration/OnshapePartPlanModels.cs`; keep plan-local IDs distinct from native IDs and avoid mutable/global state. <!-- sdd-owner: implementation -->
- [x] TRIANGULATE: extend the same model tests with empty/default collections, nullable native IDs/errors, all status combinations, and a mixed-profile input while asserting no serializer aliases or unsupported stage kinds are introduced. <!-- sdd-owner: implementation -->
- [x] REFACTOR: simplify model constructors/collection ownership without widening visibility or changing the closed schema; run the focused model tests and confirm only the two listed files changed. <!-- sdd-owner: implementation -->

**Acceptance evidence:** focused model test output is green; the model file contains no provider calls, JSON permissiveness, static cache, or god-macro edits; the test diff demonstrates the immutable/closed-contract boundary.

**Rollback boundary:** remove the two unit files; no existing provider, builder, tool, or test behavior is reverted.

### Unit 2 — Strict address and plan parser

**Depends on:** Unit 1. **Changed-line cap:** 280–380.

**Exact file surface:**
- `src/McpCad.Tools/Onshape/Orchestration/OnshapePartPlanParser.cs`
- `tests/McpCad.Tests/Tools/OnshapePartPlanParserTests.cs`

**Start:** Unit 1 model contracts compile; no parser is callable.

**Tasks (strict RED → GREEN → TRIANGULATE → REFACTOR):**

- [x] RED: write failing parser tests for valid two-string address/plan roots, exact member allowlists, duplicate members at root/stage/profile/point/segment depth, missing members, wrong JSON kinds, malformed JSON, non-integer version, numeric strings, nulls, non-finite values, exact case-sensitive discriminants, and useful JSON paths in `tests/McpCad.Tests/Tools/OnshapePartPlanParserTests.cs`. <!-- sdd-owner: implementation -->
- [x] GREEN: implement `OnshapePartPlanParser` in `src/McpCad.Tools/Onshape/Orchestration/OnshapePartPlanParser.cs` using strict `JsonDocument` enumeration, ordinal duplicate/unknown checks, required-member checks, exact `JsonValueKind` checks, and explicit switches for sketch/extrude/checkpoint plus rectangle/circle/polygon/mixed forms. Do not call the provider. <!-- sdd-owner: implementation -->
- [x] TRIANGULATE: add parser cases for optional `microversionId`, repeated closing polygon points, derived-start mixed segments, forbidden segment `start`/`mid`, whitespace/empty strings, all unsupported aliases, and late malformed stages; assert parsing rejects the whole request before any fake provider could be touched. <!-- sdd-owner: implementation -->
- [x] REFACTOR: extract only local parser helpers that preserve exact allowlists and error paths; run the focused parser tests and the model tests with no behavior change. <!-- sdd-owner: implementation -->

**Acceptance evidence:** parser tests prove fail-closed exact JSON behavior at every object level and prove a late invalid stage remains provider-free; no `JsonSerializer` permissive polymorphic path, reflection dispatch, or source changes outside the two listed files.

**Rollback boundary:** remove parser implementation/tests while retaining the model contracts; no cloud call is made by this unit.

### Unit 3 — Complete preflight validation and sketch compilation

**Depends on:** Unit 2. **Changed-line cap:** 300–390.

**Exact file surface:**
- `src/McpCad.Tools/Onshape/Orchestration/OnshapePartPlanValidator.cs`
- `tests/McpCad.Tests/Tools/OnshapePartPlanValidatorTests.cs`

**Start:** Strict parser produces typed plans; provider boundary remains uncalled during validation.

**Tasks (strict RED → GREEN → TRIANGULATE → REFACTOR):**

- [x] RED: add failing validator tests for schema version, 1–16 stage bounds, 0–3 checkpoint bounds, case-sensitive unique IDs, backward/typed references, latest-preceding checkpoint targets, canonical planes, finite/nonzero extrude values, and single-body-result assumption messaging. <!-- sdd-owner: implementation -->
- [x] GREEN: implement complete preflight in `src/McpCad.Tools/Onshape/Orchestration/OnshapePartPlanValidator.cs`: address validation, symbol-table construction, reference checks, limits, geometry checks, centimeter-to-meter conversion exactly once, and disposal-safe compilation through `OnshapeSketchBuilder`, `OnshapeMixedProfileFacade`, and `OnshapeFeatureTemplates` without provider reads/writes/captures. <!-- sdd-owner: implementation -->
- [x] TRIANGULATE: cover rectangle/circle/polygon/mixed boundaries, one repeated polygon closing point, invalid topology, 1/8/9 arc limits, unsupported planes/operations/directions, stale checkpoints, and a provider fake that throws if validation invokes it. <!-- sdd-owner: implementation -->
- [x] REFACTOR: consolidate validation errors and immutable compiled payload ownership while retaining stable error codes (`INVALID_*`, `PLAN_LIMIT_EXCEEDED`, `INVALID_REFERENCE`) and preserving existing builder contracts; rerun parser, validator, and model tests. <!-- sdd-owner: implementation -->

**Acceptance evidence:** validator tests show every statically knowable error is rejected before the first provider call; compiled sketch payloads are owned/disposable-safe; `skill_onshape_god_part` and all existing builders remain unmodified.

**Rollback boundary:** revert only the validator and its tests; the parser/model contract remains independently reviewable and no provider mutation is involved.

### Unit 4 — Sequential executor and native feature-ID normalization

**Depends on:** Unit 3. **Changed-line cap:** 300–390.

**Exact file surface:**
- `src/McpCad.Tools/Onshape/Orchestration/OnshapeFeatureWriteResult.cs`
- `src/McpCad.Tools/Onshape/Orchestration/OnshapePartPlanExecutor.cs`
- `tests/McpCad.Tests/Tools/OnshapePartPlanExecutorTests.cs`

**Start:** Only `ValidatedOnshapePartPlan` is accepted by the new executor; no adapter projection exists yet.

**Tasks (strict RED → GREEN → TRIANGULATE → REFACTOR):**

- [x] RED: add failing recording-provider tests for declaration order, native sketch ID binding, top-level and nested feature-ID response shapes, meter/depth/direction/operation linkage, first modeling failure stop, later modeling `not_attempted` results, checkpoint continuation, no retries, and no rollback/deletion calls. <!-- sdd-owner: implementation -->
- [x] GREEN: implement `OnshapeFeatureWriteResult` and `OnshapePartPlanExecutor` in the two production files; call only `SketchReplace`, `FeatureAdd`, and `CaptureView`, resolve only successful prior native IDs, stop modeling after the first modeling failure, continue after capture failure, and catch active-stage exceptions with stable contextual errors. <!-- sdd-owner: implementation -->
- [x] TRIANGULATE: add tests for missing native IDs, false provider results, thrown exceptions, blank/invalid capture base64, capture image ordering, modeling failure before a checkpoint, repeated checkpoints at the same latest model stage, and ambiguous timeout/microversion errors without retry. <!-- sdd-owner: implementation -->
- [x] REFACTOR: isolate local execution state and response normalization helpers, preserve opaque native IDs exactly, and rerun executor plus validator/parser/model tests after each extraction. <!-- sdd-owner: implementation -->

**Acceptance evidence:** provider recordings show exact sequential calls and zero calls after modeling stop; successful writes remain represented by native IDs; no `FeatureList`, topology inference, rollback, retry, static map, or multi-body guarantee is introduced.

**Rollback boundary:** remove the normalizer/executor and focused tests; validated plans remain inert and existing provider contracts are untouched.

### Unit 5 — Exact result envelope and MCP content projection

**Depends on:** Unit 4. **Changed-line cap:** 240–340.

**Exact file surface:**
- `src/McpCad.Tools/Onshape/Orchestration/OnshapeOrchestrationResultBuilder.cs`
- `tests/McpCad.Tests/Tools/OnshapePartOrchestratorResultTests.cs`

**Start:** Executor returns typed execution state; no public `CallToolResult` projection is available.

**Tasks (strict RED → GREEN → TRIANGULATE → REFACTOR):**

- [x] RED: write failing result tests for exact structured keys, schema version, ordered one-per-stage results, null rules, status precedence, modeling/capture separation, captures `none/completed/partial_failure`, evidence/error placement, `IsError`, and one PNG image block per successful checkpoint in declaration order. <!-- sdd-owner: implementation -->
- [x] GREEN: implement `OnshapeOrchestrationResultBuilder` in the exact production path to project typed execution state into the specified `StructuredContent`, ordered `ImageContentBlock.FromBytes` content, and preflight/error envelopes without leaking provider dictionaries into stage results. <!-- sdd-owner: implementation -->
- [x] TRIANGULATE: cover completed, capture-only failure, modeling failure with skipped later stages, no checkpoints, invalid image evidence, nested capture metadata, and successful modeling with no native ID; compare normalized JSON property sets rather than incidental serialization order. <!-- sdd-owner: implementation -->
- [x] REFACTOR: centralize status/error projection and retain the exact field sets from the specification; run result, executor, validator, parser, and model tests together. <!-- sdd-owner: implementation -->

**Acceptance evidence:** result tests prove the exact envelope and content/image rules; capture failure never changes `modeling.status`; partial results expose prior native IDs without claiming atomicity, rollback, or safe replay.

**Rollback boundary:** remove only the result builder and result tests; executor behavior remains independently testable.

### Unit 6 — Additive MCP adapter and registration compatibility

**Depends on:** Units 4 and 5. **Changed-line cap:** 160–260.

**Exact file surface:**
- `src/McpCad.Tools/Onshape/OnshapePartOrchestratorTool.cs`
- `tests/McpCad.Tests/Tools/ToolRegistrationCollisionTests.cs`
- `tests/McpCad.Tests/Tools/OnshapePartOrchestratorToolTests.cs`

**Start:** Parser, validator, executor, and result builder are independently tested; `OnshapeSkillTools` has no orchestrator method.

**Tasks (strict RED → GREEN → TRIANGULATE → REFACTOR):**

- [x] RED: add failing adapter tests for the exact two-string signature, parser/validator/executor/builder composition, preflight error projection, provider injection, and zero provider calls for invalid late plans; add registration assertions for exactly one `onshape_orchestrate_part` name and unchanged `skill_onshape_god_part`. <!-- sdd-owner: implementation -->
- [x] GREEN: add only the partial `OnshapeSkillTools` method in `src/McpCad.Tools/Onshape/OnshapePartOrchestratorTool.cs` with `[McpServerTool]` and the exact `CallToolResult onshape_orchestrate_part(string address_json, string plan_json)` signature; do not edit `OnshapeSkillTools.cs`, `OnshapeGodPart.cs`, `OnshapeGodPartCapture.cs`, or `McpCad.Server/Program.cs`. <!-- sdd-owner: implementation -->
- [x] TRIANGULATE: test malformed address/plan, successful typed execution, capture-only failure, partial modeling failure, exact reflected parameter types, no internal MCP calls, and registration collision behavior alongside existing god-part tests. <!-- sdd-owner: implementation -->
- [x] REFACTOR: keep adapter responsibilities limited to parse, preflight, execute, and project; rerun the complete focused Tools suite and confirm no existing tool name/schema changed. <!-- sdd-owner: implementation -->

**Acceptance evidence:** reflection finds exactly two serialized string parameters and one public new tool; existing `skill_onshape_god_part` tests/schema observations remain green; invalid input produces no provider operation or capture.

**Rollback boundary:** remove the new adapter and its added tests/assertions; existing `OnshapeSkillTools` partials and server registration remain unchanged.

### Unit 7 — REST conformance, provider/parity documentation, and final compatibility gate

**Depends on:** Unit 6. **Changed-line cap:** 260–380.

**Exact file surface:**
- `tests/McpCad.Tests/Onshape/OnshapePartOrchestratorWireMockTests.cs`
- `docs/providers.md`
- `docs/onshape-parity-matrix.md`

**Tasks (strict RED → GREEN → TRIANGULATE → REFACTOR):**

- [x] RED: add failing WireMock scenarios for feature POST ordering, sketch payloads, centimeter-to-meter conversion, native sketch ID in `entities.queries[0].featureId`, nested/top-level response normalization, checkpoint shaded-view timing, PNG content, and capture failure continuation; use only local WireMock routes and fixtures. <!-- sdd-owner: implementation -->
- [x] GREEN: implement the WireMock/provider conformance test and update `docs/providers.md` plus `docs/onshape-parity-matrix.md` with the new provider/tool entry, canonical planes/profiles, single-body-result assumption, non-idempotent fresh/known-Part-Studio requirement, one-body-result limitation, ordered checkpoint/capture semantics, partial failure/no rollback/no retry behavior, and image-as-evidence boundary. <!-- sdd-owner: implementation -->
- [x] TRIANGULATE: add alternate response shapes and transport failure cases, assert no live endpoint/credential dependency, verify WireMock request order and exact payload linkage, and confirm documentation does not imply rollback, idempotent replay, topology guarantees, or drawing inference. <!-- sdd-owner: implementation -->
- [x] REFACTOR: keep docs terminology aligned with result codes/statuses and run the full net8 suite and solution build after all refactors. <!-- sdd-owner: implementation -->

**Acceptance evidence:** WireMock tests prove request/response integration without live Onshape mutation; both docs files describe provider parity and the user-facing non-idempotency, single-body, ordered single-body-result, checkpoint, and capture semantics; final commands are:

```text
dotnet test tests/McpCad.Tests/McpCad.Tests.csproj --framework net8.0 --no-restore
dotnet build src/mcp-cad.sln
```

Compatibility evidence must include unchanged god-macro tests/schema, tool registration collision checks, and provider/builder contract tests. Any live Onshape run remains out of scope unless separately authorized.

**Rollback boundary:** remove only the new WireMock test and documentation additions; do not roll back existing provider docs, provider contracts, or cloud state.

## Parent-only post-apply review and lifecycle gates

- [x] At every unit boundary, inspect changed-line estimates and require a further split before applying any unit predicted above 400 changed lines; do not infer a size exception. <!-- sdd-owner: parent -->
- [x] Start or reuse a bounded review for the completed chain, checking exact file surfaces, RED/GREEN/TRIANGULATE/REFACTOR evidence, compatibility claims, and absence of live mutation. <!-- sdd-owner: parent -->
- [x] Confirm the delivery diff excludes `.atl/`, `.pi/`, `.pi-subagents/`, source/test files outside the declared unit surfaces, credentials, and generated artifacts. <!-- sdd-owner: parent -->
- [x] Confirm final net8 suite/build evidence and defer any commit, push, or PR lifecycle action to the parent workflow rather than executor tasks. <!-- sdd-owner: parent -->
