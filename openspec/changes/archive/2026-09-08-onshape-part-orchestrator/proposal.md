# Proposal: Onshape Part Orchestrator

**Change:** `onshape-part-orchestrator`  
**Status:** Proposed  
**Public tool:** `onshape_orchestrate_part`

## Intent

Introduce a bounded Onshape Part Studio orchestration tool that executes a strict, versioned, ordered plan of typed sketch, extrude, and checkpoint stages. The tool will compose existing Onshape builders and provider operations while preserving `skill_onshape_god_part` unchanged.

The first slice is intentionally a deterministic feature-plan executor, not a scripting language or general CAD workflow engine.

## Problem

The repository exposes useful Onshape sketch, feature, capture, and fixed-workflow primitives, but callers cannot submit a single validated plan for a multi-stage part. They must coordinate low-level operations themselves or use `skill_onshape_god_part`, whose fixed sequence and single latest-feature reference do not model arbitrary ordered sketch/extrude dependencies.

This creates avoidable coordination complexity, inconsistent failure reporting, and a risk of partially mutating a cloud Part Studio before invalid later plan steps are discovered. A bounded orchestrator is needed to validate the whole request before mutation, resolve stable plan-local references to returned Onshape feature IDs, and report partial completion clearly.

## Goals

- Add a new snake_case MCP tool named `onshape_orchestrate_part`.
- Accept a strict, explicitly versioned plan with an ordered, closed union of supported stage types.
- Validate the complete plan before the first modeling write.
- Support at most 16 total stages, including no more than 3 explicit intermediate checkpoint stages.
- Require unique plan-local stage IDs and permit references only to earlier stages.
- Resolve plan-local sketch IDs to native feature IDs during execution.
- Return explicit ordered outcomes for modeling stages and separate outcomes for captures.
- Stop modeling on the first modeling failure without attempting rollback.
- Make the fresh/known-Part-Studio and non-idempotent execution contract explicit.
- Reuse existing provider boundaries, sketch builders, feature templates, and capture behavior rather than issuing repeated MCP calls internally.

## First-slice capability

### Plan contract

The request identifies one Onshape Part Studio and declares a supported schema version plus an ordered stage list. The schema is exact and finite: unsupported versions, stage kinds, fields, operations, planes, geometry, values, or references are rejected during preflight.

The plan contains no more than 16 total stages. Every stage has a unique plan-local ID. Dependencies are backward-only, so execution follows declaration order and requires no DAG scheduler.

### Sketch stages

A sketch stage targets only a canonical `Top`, `Front`, or `Right` plane and creates exactly one closed profile using an existing supported geometry form:

- rectangle;
- circle;
- closed straight-segment polygon/profile; or
- validated closed mixed line/arc profile using the existing 1–8 arc capability.

Dimensions and geometry must satisfy the existing finite-value and profile validation rules.

### Extrude stages

An extrude stage references a previously declared sketch stage by its plan-local ID. It supports only:

- `new`;
- `add`; or
- `remove`.

Depth must be finite and nonzero, with supported direction semantics. `add` and `remove` operate under an explicit single-body-result assumption; the first slice does not introduce explicit target-body or boolean-scope selection.

### Checkpoints and results

A plan may include up to 3 explicit intermediate capture checkpoints using currently supported views. Capture outcomes are reported separately from modeling outcomes. A checkpoint capture failure does not stop later modeling.

Execution returns ordered stage results, native feature IDs where available, the last successful modeling feature ID, the first modeling failure when present, skipped/unattempted state for later modeling stages, and separate checkpoint capture results. Final established capture behavior may be retained where required by the tool contract, but does not turn capture failure into modeling failure.

## Scope and affected areas

Expected later implementation work is limited to:

- the `McpCad.Tools` Onshape skill-tool surface for the new MCP entry point;
- typed request, stage-union, validation, execution, and result models;
- orchestration over existing `ICadWebProvider` operations;
- reuse of `OnshapeSketchBuilder`, `OnshapeFeatureTemplates`, and current capture helpers;
- unit tests using provider recordings/mocks and REST conformance tests using WireMock or golden payloads, without live CAD calls.

The provider interface should not be expanded solely to support orchestration when its existing sketch and generic feature operations are sufficient.

## Constraints and business rules

- The complete static plan must pass preflight before any cloud mutation occurs.
- Schema versions and supported stage types are allowlisted in code.
- IDs are unique within the plan and are not user-supplied native Onshape topology IDs.
- References point only backward, and extrudes reference prior sketch stages.
- All stage and checkpoint limits are enforced before mutation.
- Modeling is sequential and stops at the first modeling failure.
- Capture failure is non-fatal to modeling and is represented independently.
- Successful writes remain in the Part Studio after a later failure; no transaction or compensating deletion is attempted.
- Execution is non-idempotent. Callers must use a fresh or otherwise known Part Studio and must not blindly retry a partially completed plan.
- Existing microversion-skew rejection remains authoritative; concurrent or manual edits may cause execution to stop and report partial completion.
- Images are verification evidence only and are not interpreted as dimensions or executable geometry.

## Non-goals

- Modifying, replacing, or widening `skill_onshape_god_part`.
- User scripts, FeatureScript, raw REST payloads, arbitrary feature names, reflection-based dispatch, loops, variables, conditionals, branching, or a general DAG scheduler.
- Sketches on generated faces, arbitrary faces, generated planes, or offset planes.
- Semantic topology selectors or stable plan-local naming for faces and edges.
- Multiple loops or selectable regions within one sketch.
- Explicit multi-body boolean scope, multi-body-result workflows, or combine.
- Resumability, reconciliation, automatic retries, rollback, or idempotent replay.
- Image-to-geometry inference or automatic reconstruction of the full motivating drawing.
- Additional feature families such as fillet, chamfer, shell, pattern, mirror, sweep, or revolve.

## User impact

Callers gain one coherent request for bounded multi-stage Onshape parts instead of coordinating individual sketch and extrude operations. Plan-local IDs make dependencies understandable without exposing native feature identifiers in the input, while full preflight catches invalid plans before mutation.

Users must understand that valid execution still performs incremental cloud writes. A runtime failure can leave a useful but partial feature tree, and replaying the same plan can duplicate features. The result therefore prioritizes transparent stage status and native IDs over an unsupported atomicity guarantee.

Existing users of `skill_onshape_god_part` experience no schema or behavior change.

## Compatibility

- This is an additive tool; no existing MCP tool is renamed or removed.
- `skill_onshape_god_part` remains unchanged.
- Existing provider contracts and typed payload builders remain the compatibility boundary.
- Unsupported future schema versions fail explicitly rather than being interpreted loosely.
- Existing dimension conversion, authentication, synchronous write, microversion, and error-propagation behavior remains in force.
- The first version deliberately accepts only the documented closed stage union; adding future stage kinds requires a new compatible schema evolution and implementation.

## Risks and mitigations

| Risk | Impact | Mitigation |
| --- | --- | --- |
| Partial cloud mutation | A failed plan leaves persistent features and a retry may duplicate them | Full preflight, stop on first modeling failure, explicit partial results, native IDs, and prominent non-idempotency documentation |
| Ambiguous `add`/`remove` scope | Operations may fail or affect an unintended body in multi-body states | Require and document the single-body-result assumption; reject claims of general multi-body support |
| Feature/topology identity confusion | Callers may expect plan-local IDs to select stable faces or edges | Limit references to typed feature dependencies and exclude semantic topology selection |
| Invalid late-stage input | Mutation could begin before a malformed dependency or unsupported combination is noticed | Validate the complete plan, limits, references, and values before the first write |
| Concurrent Part Studio edits | Microversion skew can interrupt execution | Preserve skew rejection, stop modeling, and report completed and failed stages |
| Capture instability or latency | Visual evidence may be missing or slow while modeling succeeded | Cap checkpoints at 3, report capture separately, and never treat capture failure as modeling failure |
| Schema expands into an interpreter | Security, validation, and maintenance complexity increase sharply | Fixed typed union, exact-field validation, explicit versions, and code changes for every new stage kind |
| First slice is mistaken for full drawing reconstruction | Users may expect topology-heavy or image-derived geometry | State the supported geometry and plane boundaries and keep the motivating drawing outside acceptance scope |

## Rollback and failure handling

There is no runtime rollback. If a modeling stage fails, the orchestrator stops further modeling and returns the completed feature IDs and failure details so the user can inspect or manually clean the Part Studio. Capture failures are recorded independently and do not trigger deletion or stop subsequent modeling.

Deployment rollback consists of removing or disabling the additive `onshape_orchestrate_part` tool in a later code change. That does not revert Part Studios already mutated by successful or partially successful executions. Because `skill_onshape_god_part` is unchanged, existing workflows retain their prior fallback path.

## Success criteria

The proposal is successful when the implemented first slice demonstrates that:

1. `onshape_orchestrate_part` accepts a supported versioned plan and rejects unsupported or malformed plans before any provider write.
2. Plans over 16 total stages, plans with more than 3 checkpoints, duplicate IDs, forward/self references, invalid stage-type references, unsupported planes or operations, non-finite dimensions, and invalid profiles are rejected during preflight.
3. Supported rectangle, circle, closed straight-polygon/profile, and validated mixed line/arc sketches can be created on `Top`, `Front`, or `Right`.
4. `new`, `add`, and `remove` extrudes resolve a prior sketch's plan-local ID to the returned native feature ID under the documented single-body-result assumption.
5. Execution follows declaration order and stops after the first modeling failure while accurately reporting completed, failed, and later unattempted/skipped modeling stages.
6. Up to 3 explicit checkpoint captures are attempted in plan order; capture failures are reported separately and do not stop modeling.
7. Results expose enough stage status and native feature information to diagnose partial completion without claiming rollback or safe replay.
8. Re-running a plan is explicitly documented as unsafe unless the caller has restored or selected a fresh/known Part Studio.
9. Existing `skill_onshape_god_part` behavior and schema remain unchanged.
10. Automated tests cover validation, ordering, reference resolution, stop/continue policies, payload integration, and compatibility without requiring live Onshape calls.
