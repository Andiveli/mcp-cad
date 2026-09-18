# Proposal: Onshape/Linux part modeling

## Scope reconciliation — authoritative current delivery

This change is the completed **Onshape/Linux** deliverable: additive typed portable geometry contracts, the Onshape canonical-frame reader/mapper/preflight/context, and internal Onshape factory registration. Existing public MCP tools, schemas, and provider selection behavior remain unchanged. The completed work is evidenced by the focused Unit 3 result (**8 passed**) and the net8.0 regression (**657 passed, 7 skipped, 0 failed**); these are Linux/offline test results, not live CAD certification.

Inventor Units 4, 5, and 6B are explicitly **deferred**, not completed, discarded, or validated on Linux. Their future planning handoff is recorded in `apply-progress.md` under `inventor-windows-part-modeling`; it requires a later native planning launch and a configured Windows/Inventor boundary. This reconciliation preserves the historical planning text below as provenance only. Where it differs from this section, it is superseded and makes no current two-provider delivery claim.

Introduce additive, typed shared contracts and Onshape adapter support for canonical-plane rectangle/circle sketches and new-body extrusion, without changing existing public MCP tools, schemas, or behavior.

## Intent and problem

Provider inheritance does not currently make part modeling portable. `src/McpCad.Core/ICadWebProvider.cs` accepts cloud addresses and generic feature requests; `src/McpCad.Core/IMechanicalCadProvider.cs` exposes active-document sketch and extrusion operations. Both return dictionaries, but their targets, feature references, and lifecycle semantics differ. The existing `src/McpCad.Tools/Onshape/Orchestration/OnshapePartPlanExecutor.cs` directly executes against the web interface.

Maintainers need a small, testable modeling boundary that represents shared geometry without treating desktop COM state as a cloud document. This change establishes that boundary for internal consumers; it does not deliver a new user-facing modeling workflow or autonomous orchestration.

## Authority and confirmed gate

- Active change: `provider-neutral-part-modeling`; execution selection: AUTO; artifact backend: OpenSpec.
- [preproposal.md](preproposal.md) records explicit product confirmation and resolves the question round. No further interview or inferred consent is needed.
- [explore.md](explore.md) supplies repository evidence. External research is explicitly unselected.
- The completed adapter boundary is Onshape/Linux only. Any earlier mandatory multi-provider scope is historical provenance and is superseded by the explicit deferred follow-up split.
- `openspec/config.yaml` supplies C#/.NET conventions, additive interface boundaries, strict TDD, and mock/WireMock validation. Its historical `onshape-part-orchestrator` change name does not override the explicitly selected change.
- Supplied native status recommends `propose`; blocked planning dependencies indicate missing artifacts, not a proposal launch block. This assignment ends with this proposal.

## Scope

| Boundary | Required outcome |
| --- | --- |
| Shared contracts | Typed portable geometry, capability declarations, operation results/failures, and provider-scoped opaque references. Explicit canonical plane, unit, and extrusion-direction semantics must prevent silent native-default substitution. |
| Portable feature floor | Closed rectangle and circle sketches on canonical Top/Front/Right planes, followed by new-body extrusion, implemented by the Onshape adapter. Capability gating must not make this completed floor optional for that adapter. |
| Onshape adapter | Translate the floor through existing web sketch/feature operations; keep cloud addresses, native feature IDs, and microversion handling adapter-owned. |
| Inventor follow-up | Deferred to `inventor-windows-part-modeling`; it is outside this completed delivery and remains subject to a Windows/Inventor planning and execution boundary. |
| Selection and targets | Preserve configuration-based provider selection in `src/McpCad.Server/ProviderBootstrap.cs`; no provider argument or universal cloud-shaped target in public schemas. |
| Optional capabilities | Evidence/capture and revision metadata are optional and provider-specific. Unavailable evidence is explicit; capture failure does not turn successful geometry into modeling failure. No equivalent revision or concurrency guarantee is implied. |
| Failure boundary | Validate known unsupported/invalid requests before mutation. Stop subsequent modeling work on modeling failure, report partial/uncertain outcomes honestly, and never automatically retry or roll back. |
| Compatibility | Add internal contracts/adapters without replacing broad provider interfaces or narrowing current public functionality to the portable floor. |

The contract may describe operation ordering and sketch-to-extrusion references needed for adapter conformance. It must not grow into a new orchestration engine. Exact types, placement, plane mappings, and adapter integration mechanics belong to later design.

## Affected areas and repository evidence

Paths below are verified existing anchors; new filenames are not prescribed at proposal time.

| Verified anchor | Expected impact |
| --- | --- |
| `src/McpCad.Core/ICadWebProvider.cs`, `src/McpCad.Core/IMechanicalCadProvider.cs` | Add shared contracts alongside these incompatible provider boundaries; preserve their existing public behavior. |
| `src/McpCad.Inventor/InventorProvider.cs` | Adapter integration with existing manager-backed rectangle/circle and document-state operations; no REST emulation. |
| `src/McpCad.Tools/Onshape/Orchestration/OnshapePartPlanExecutor.cs` | Compatibility boundary for existing sketch/extrude linkage, unit conversion, stop behavior, and independent capture outcomes. Avoid a wholesale executor rewrite. |
| `src/McpCad.Server/ProviderBootstrap.cs` | Bind the internal adapter consistently with existing single-provider configuration and platform restrictions; preserve public tool-family registration. |
| `tests/McpCad.Tests/Tools/OnshapePartPlanExecutorTests.cs` | Preserve ordering, opaque native-ID linkage, conversion, capture continuation, and no-retry/no-rollback regression evidence. |
| `tests/McpCad.Tests/Onshape/OnshapePartOrchestratorWireMockTests.cs` | Preserve public response and local REST request-order compatibility; add corresponding adapter conformance coverage under `tests/McpCad.Tests/`. |

## Success criteria

- [x] The Onshape adapter implements and passes the completed rectangle/circle + canonical-plane + new-body extrusion floor under Linux/offline tests.
- [x] Onshape conformance tests verify plane/orientation and unit/depth translation, sketch-to-extrusion reference binding, and new-body intent without relying on provider defaults.
- [x] Shared geometry contains no mandatory `CadElementAddress`, active COM document object, cloud revision, image payload, or cross-provider topology identity. Targets and native handles stay adapter-owned and provider-scoped.
- [x] Invalid geometry, unresolved references, unsupported capabilities, and detectable invalid targets fail before the corresponding mutation; failed modeling stops subsequent modeling calls, including after ambiguous provider errors.
- [x] Tests verify no automatic retry, compensating delete, or rollback; results distinguish successful prior work, failure/uncertainty, and work not attempted.
- [x] Evidence and revision metadata are capability-gated; absent capture/revision support is not falsely advertised or required for baseline modeling success.
- [x] Existing public tool names, input schemas, outputs, provider selection, and behavior remain unchanged, including Onshape capabilities beyond the portable floor and existing capture-failure semantics.
- [x] Strict-TDD unit and local WireMock integration evidence covers the completed Onshape delivery; no live CAD calls or credentials were used by its automated validation.

## Out of scope

- New public provider-neutral MCP tools or changes to existing tool registration semantics in `src/McpCad.Server/ProviderBootstrap.cs`.
- SolidWorks implementation or parity claims; the verified bootstrap supports only Inventor and Onshape.
- Future orchestrator/worker/verifier loops, scheduling, autonomous planning, verifier decisions, worker prompts, or a replacement for the existing Onshape executor.
- Extending the portable floor to polygon/mixed profiles, joins/cuts, topology-driven or face-mounted sketches, generated planes, multi-body selection, assemblies, or semantic topology identity. Existing provider-specific functionality remains available unchanged.
- Universal document addresses, transactional execution, automatic retry/resume/rollback, cloud/desktop revision equivalence, or concurrency guarantees.
- External research, live CAD mutation, credentials, source implementation, commits, pushes, reviews, or execution of another SDD phase in this assignment.

## Risks and mitigations

| Risk | Mitigation |
| --- | --- |
| Similar API names mask different geometry or profile semantics | Require explicit mappings and shared adapter conformance cases; capability claims alone are insufficient. |
| Deferred Windows native-state work may produce uncertain mutations | Carry the constraint into `inventor-windows-part-modeling`; do not claim a Linux result or transactional recovery. |
| Portable subset accidentally restricts existing Onshape behavior | Keep additive boundaries and preserve existing executor/WireMock regressions, including operations outside the shared floor. |
| Evidence/revision metadata suggests false parity | Keep capabilities optional and metadata opaque/provider-specific; separate evidence failures from modeling failures. |
| Two adapters plus contracts and tests exceed review budget | Treat this as a credible sizing risk, not a measured forecast. At task forecast, apply `ask-on-risk` against 400 changed lines and pause for a delivery decision if needed. No chain, split, or size exception is authorized; never remove Inventor to fit. |

## Rollback

For a later implementation regression, revert the additive adapter/contracts integration and its registration changes together, restoring the existing provider/tool paths while preserving existing public contracts. This is a software rollback strategy, not permission to undo CAD mutations. Previously created geometry may remain after failure and requires explicit operator assessment; no automatic model cleanup is proposed.

## Validation and handoff

Proposal validation is repository-only: confirmed gate and configuration read directly; affected anchors checked through targeted reads. CodeGraph and shell execution are unavailable in this executor, so no graph analysis, Git diff, build, or test execution is claimed.

Later implementation must follow configured strict TDD and validate with `dotnet test tests/McpCad.Tests/McpCad.Tests.csproj --framework net8.0 --no-restore` and `dotnet build src/mcp-cad.sln`, respecting Inventor's platform boundary. Mock conformance is not a claim of live CAD certification.

Next recommended planning phase: specifications, in a separate authorized invocation. This proposal neither starts that phase nor authorizes a delivery strategy beyond the confirmed `ask-on-risk` policy.
