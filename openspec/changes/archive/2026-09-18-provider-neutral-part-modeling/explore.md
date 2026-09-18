# Exploration: Provider-neutral part modeling

**Change:** `provider-neutral-part-modeling`
**Phase:** explore only
**Artifact store:** OpenSpec

## Executive summary

The repository has provider *abstractions*, but not a portable part-modeling contract. `ICadWebProvider` and `IMechanicalCadProvider` are sibling, shape-incompatible interfaces which return untyped dictionaries and use fundamentally different addressing/lifecycle models. The existing `onshape_orchestrate_part` is a proven Onshape-only adapter over `CadElementAddress`, Onshape JSON builders, `SketchReplace`, `FeatureAdd`, and `CaptureView`; it is not reusable by Inventor merely because both implementations inherit `ICadProvider`.

The smallest safe migration is additive: introduce a small shared, typed contract for the portable subset (canonical-plane closed sketch + extrude, plan-local feature references, typed stage/result/outcome records), plus a capability declaration and a provider-neutral executor seam. Keep the current public MCP tools—including `onshape_orchestrate_part`—as compatibility adapters. Do **not** make cloud addresses, images, topology IDs, microversion/revision fields, or desktop session/document state part of the portable geometry model.

The future orchestrator/worker/verifier loop is explicitly out of scope. This change should establish its data and capability seam only, not scheduling, autonomous retry, mutation recovery, or verification decisions.

## Native SDD status

The active change was explicitly supplied as `provider-neutral-part-modeling`; no artifact directory existed at exploration start. The native `sdd-status` runtime/engine was not callable from this executor's available tool surface, so an authoritative native status and `actionContext` cannot be reported without guessing. No historical status was substituted: the archived `onshape-part-orchestrator` records are not status for this change.

- **Native status:** unavailable (runtime command unavailable to this executor)
- **Native actionContext:** unavailable (not inferred)
- **Observed OpenSpec state:** new change directory created only to persist this exploration

## Current architecture and evidence

| Area | Evidence | Finding |
| --- | --- | --- |
| Base contract | `src/McpCad.Core/ICadProvider.cs` | Connection, local-document, and path-export methods all return `Dictionary<string, object?>`; no typed model, capability, revision, or session identity contract exists. |
| Web provider | `src/McpCad.Core/ICadWebProvider.cs` | Cloud-specific sibling seam: `CadElementAddress`, authentication, document/workspace/version/microversion APIs, generic feature REST writes, topology reads, and PNG capture. |
| Mechanical provider | `src/McpCad.Core/IMechanicalCadProvider.cs` | Desktop-specific sibling seam: mutable active-document sketch/feature operations and string/tag references; no address argument, revision token, or capture API. |
| Onshape implementation | `src/McpCad.Onshape/OnshapeProvider.cs`; `src/McpCad.Onshape/Managers/PartStudioManager.cs` | REST writes are synchronous, update driver microversion, use document/workspace-or-version/element addresses, and capture only `iso`/`top` PNG views. |
| Inventor implementation | `src/McpCad.Inventor/InventorProvider.cs` | Actual desktop provider with manager-backed COM calls against active state. It implements the large mechanical interface, not web operations. |
| Registration | `src/McpCad.Server/ProviderBootstrap.cs`; `src/McpCad.Server/Program.cs` | Exactly one provider/tool family is registered per process. Onshape mode supplies `NullMechanicalCadProvider`; Inventor mode supplies only desktop tools. |
| Existing Onshape orchestrator | `src/McpCad.Tools/Onshape/OnshapePartOrchestratorTool.cs`; `src/McpCad.Tools/Onshape/Orchestration/OnshapePartPlan*.cs` | A closed sketch/extrude/checkpoint plan is parsed/validated before writes, resolved through opaque returned feature IDs, and executed directly against `ICadWebProvider`. |
| Current evidence/tests | `tests/McpCad.Tests/Tools/OnshapePartPlanExecutorTests.cs`; `tests/McpCad.Tests/Onshape/OnshapePartOrchestratorWireMockTests.cs` | Tests prove ordering, ID linkage, stop/no-retry behavior, capture continuation, and local WireMock REST conformance—not portability across providers. |
| SolidWorks | `README.md`; `src/` implementation search | Planned only. No `SolidWorksProvider`, registration case, or tests exist. It must not be treated as an available provider capability. |

## Portable versus provider-specific boundary

### Portable geometry and execution semantics

These are reasonable shared-contract candidates because current Onshape plans and Inventor operations can represent them, subject to capability negotiation:

- closed plan stages with unique plan-local IDs and ordered execution;
- canonical datum planes (`Top`, `Front`, `Right`), with an explicit provider mapping rather than native strings leaking into plans;
- typed closed profiles (at minimum rectangle and circle; polygon/mixed profiles only when a provider adapter supports their exact semantics);
- extrude depth, direction, and a deliberately normalized boolean intent;
- opaque provider-returned feature handles bound only inside one invocation;
- preflight-before-mutation, ordered per-stage outcome, first-modeling-failure stop, and an explicit non-idempotency/ambiguous-write outcome.

### Cloud-only addressing and lifecycle

`CadElementAddress` (`DocumentId`, `WorkspaceOrVersionId`, optional `MicroversionId`, `ElementId`) is an Onshape resource locator, not a universal model target. Its workspace/version and microversion semantics do not map to an active Inventor document. Onshape also needs HMAC authentication and may reject writes due to microversion skew. These belong in an Onshape execution target/session adapter and provider-specific evidence, not in shared stage geometry.

### Desktop-only addressing and lifecycle

Inventor currently targets the provider's active COM document and resolves sketch/feature entities through strings/tags. It has no comparable `CadElementAddress`, cloud revision, or `CaptureView` contract. A portable target must therefore be an opaque provider session/model reference owned by the adapter, not a forced common string address.

### Topology, capture, and revision

- Onshape `InspectEdges`/`InspectFaces` return opaque, version-sensitive topology IDs; Inventor exposes only `InspectEdges()` in its mechanical contract and uses different reference semantics. No semantic/stable shared topology selection exists.
- Onshape captures `iso`/`top` shaded PNG images. The desktop interface has no equivalent capture capability. Capture must be an optional evidence capability with a neutral evidence result, not a mandatory part-model stage.
- Cloud microversion and desktop document/session state must be reported as provider-specific revision/lifecycle metadata. A common `revision` field, if adopted, must be opaque and only useful for reporting/concurrency policy—not assumed comparable or interchangeable.

## Smallest safe migration boundary

1. **Additive shared model package in `McpCad.Core`:** immutable portable plan/stage/profile records, normalized result/error records, opaque `ModelTarget`/`ProviderFeatureHandle` values, and capability descriptors. Do not reuse `CadElementAddress` as the shared target.
2. **Narrow provider-neutral seam:** an interface for portable plan execution or for the few typed operations needed by a shared executor. Its methods should return typed results, not `Dictionary<string, object?>`, and should accept cancellation only if all participating adapters can honor it. Keep existing broad provider interfaces untouched initially.
3. **Onshape adapter:** translate shared geometry to the existing builders/templates and `ICadWebProvider`, retain exact address parsing, microversion behavior, native IDs, and optional checkpoint image evidence. The existing public tool stays as an adapter with its current schema/response behavior.
4. **Inventor adapter:** map only capabilities genuinely implemented against the active COM document (likely canonical plane, rectangle/circle, and new-body extrude first). It must explicitly decline unsupported profiles, operations, revision guarantees, and capture—not pretend parity because `IMechanicalCadProvider` exists.
5. **No tool-routing replacement yet:** retain `OnshapeAtomicTools`, `OnshapeSkillTools`, `AtomicTools`, and `SkillTools`; later public-neutral tooling is a separate product decision.

This avoids a breaking rewrite of the existing public MCP contract while creating a testable seam for a later multi-provider orchestrator.

## Product decisions for proposal

1. **Public surface:** Should this change introduce a new provider-neutral MCP tool now, or only shared contracts/adapters behind current public tools? Recommendation: contracts/adapters only; a public tool multiplies compatibility and capability-policy decisions.
2. **Portable v1 feature floor:** Is the required cross-provider floor rectangle/circle + `new` extrude only, or are polygon/mixed profiles and `add`/`remove` required? Recommendation: require an adapter capability per feature and make the initial guaranteed floor deliberately small.
3. **Execution target:** Should callers supply provider-specific target JSON through a tagged union, or should server configuration select a provider and the target remain provider-specific at the outer adapter? Recommendation: configuration-selected provider plus opaque adapter target initially; avoid exposing a false universal document address.
4. **Evidence policy:** Is visual capture required for success, optional review evidence, or unavailable on desktop providers? Recommendation: optional capability whose failure never changes modeled-geometry success unless a future caller explicitly requires evidence.
5. **Revision/concurrency policy:** Should portable execution expose only opaque before/after revision metadata, or require optimistic concurrency tokens? Recommendation: report opaque metadata first; do not claim equivalent cloud/desktop concurrency semantics.
6. **Failure policy:** Preserve first-write-failure stop and no retry/rollback for the portable subset? Recommendation: yes; remote and COM mutations can both be non-idempotent and ambiguous after transport/process failures.

## Risks and review sizing

| Risk | Mitigation |
| --- | --- |
| Treating inheritance from `ICadProvider` as modeling parity | Capability descriptors and adapter conformance tests; do not cast/reuse sibling interfaces. |
| Leaking `CadElementAddress` into a universal API | Make target/session provider-owned and opaque in core; keep cloud resource locators in Onshape. |
| Translating native feature/topology IDs across providers | Restrict shared handles to an invocation and provider; exclude topology-driven stages. |
| Breaking current MCP schemas and Onshape behavior | Add adapters only; regression-test current Onshape tool results and WireMock request order. |
| False capture/revision guarantees | Model both as optional/provider-specific evidence and metadata. |
| Oversized first implementation | Keep models/capabilities + one shared executor seam + Onshape adapter as one review unit; defer Inventor adapter or public tool if line forecast exceeds 400. |

A full core contract, two adapters, public neutral tool, docs, and conformance tests likely exceeds the 400-line review budget. Under `ask-on-risk`, proposal should split it: (1) shared contracts/capability matrix plus Onshape compatibility adapter; (2) Inventor adapter and conformance tests; (3) optional public neutral tool and later loop work. Do not select a chain strategy until that risk is confirmed in proposal/design.

## Explicitly out of scope

- Future orchestrator/worker/verifier loop, scheduling, worker prompts, verifier decisions, graph execution, and autonomous recovery.
- Live CAD calls, credentials, model mutations, commits, pushes, and external research.
- SolidWorks implementation or claims of SolidWorks parity.
- Semantic topology selection, face-mounted sketches, generated-plane targeting, multi-body targeting, stable topology identity, rollback, retry/resume, or a universal transactional/revision guarantee.

## Recommended next phase

Proceed to **proposal** only after the six product decisions above are answered or explicitly accepted as assumptions. The proposal should preserve all current public MCP tools and define provider-neutral contracts as capability-gated rather than provider-interface reuse.
