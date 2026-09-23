# Onshape orchestrator context bridge

## Objective
Connect the existing public `onshape_orchestrate_part` tool to the registered `IPartModelingContextFactory` for the smallest exact-overlap plan shape, without narrowing or changing the public MCP contract.

## Problem
The public orchestrator still constructs `OnshapePartPlanExecutor(provider)` directly. The new address-bound modeling factory is registered in DI but unused. A wholesale replacement would regress polygon/mixed profiles, add/remove extrusions, checkpoints, multi-stage plans, and native feature ID projection.

## Why
Using the reviewed context boundary for the portable baseline consolidates frame preflight, geometry mapping, mutation uncertainty, and stopped-state behavior while preserving the mature advanced orchestrator path.

## Scope
- Route only an adjacent two-stage modeling pair: rectangle or circle sketch followed immediately by `operation: "new"` extrusion referencing that sketch.
- Preserve exact `onshape_orchestrate_part(address_json, plan_json)` signature and v1 schema.
- Preserve non-null native feature IDs through an internal Onshape-specific execution receipt.
- Keep polygon, mixed, add/remove, checkpoints, non-adjacent sequences, multi-sketch/multi-extrusion plans, and non-workspace targets on the existing provider executor.
- Keep checkpoint behavior and image projection unchanged.

## Non-goals
- No new public MCP tool or schema version.
- No live Onshape calls, credentials, external network, Inventor work, retries, rollback, or transactional guarantees.
- Commits authorized by the user for this feature; no push or PR authorized.

## Constraints
- Strict TDD from `openspec/config.yaml`.
- Runner: `dotnet test tests/McpCad.Tests/McpCad.Tests.csproj --framework net8.0 --no-restore`.
- Preserve existing native IDs and public error/status projection.
- Keep each implementation task near or below 400 authored changed lines; delivery strategy remains `auto-chain` / `feature-branch-chain` if later committed.
- Exclude `.atl/**`, `.pi/**`, and `.pi-subagents/**`.

## Tasks
- [x] **BRIDGE-1 — Define internal receipt seam and injectable factory compatibility.** Add tests first for factory injection, native ID preservation, exact public signature, existing constructor compatibility, and no portable-contract native-ID leakage. Implement the smallest Onshape-internal receipt seam.
- [x] **BRIDGE-2 — Route only eligible adjacent baseline pairs.** Add tests first for rectangle/circle + adjacent `new` routing through one bound context, both directions/canonical planes as needed, stable result projection, failure/uncertainty stopping, and zero legacy modeling writes. Preserve provider fallback for every non-eligible plan.
  - [x] **BRIDGE-2A — Carry configured WVM mode through the Onshape factory seam.** The bootstrap-configured driver mode is exposed only by an Onshape-specific factory compatibility interface; no routing has changed.
  - [x] **BRIDGE-2B — Route eligible workspace baseline pairs.** Preserve legacy provider and missing-native-ID error codes; fail closed once routing begins; cover bind, receipt, sketch, and extrusion edges.
- [x] **BRIDGE-3 — Prove hybrid WireMock and DI behavior.** Add local-only tests for frame-read → sketch-write → extrusion-write ordering, factory resolution, workspace-only eligibility, and unchanged legacy ordering for advanced plans/checkpoints.
- [x] **BRIDGE-4 — Verify and review the bounded candidate.** Run focused and full net8 regression, record skipped/failing checks, run native review when enabled, and report delivery status without committing.
- [x] **BRIDGE-5 — Commit reviewable work units.** Staged only scoped bridge paths, checked each staged diff, committed three cohesive units and this progress document. No push or PR. Route: delegated read-only mapping (4-file rule); inline Git delivery; delegated independent final regression.

## Acceptance criteria
- Eligible adjacent rectangle/circle + new plans execute through `IPartModelingContextFactory`.
- Successful routed stages retain current `nativeFeatureId` values.
- Every unsupported or advanced plan follows the existing executor with unchanged behavior.
- Public MCP signature, schema, result structure, checkpoint images, and tool registration remain compatible.
- Context failures and uncertain writes prevent subsequent mutation without automatic retry.
- Focused bridge tests and full net8 regression pass, or exact environmental failures are recorded.

## Resolved decisions
- Native IDs use an internal Onshape-specific receipt, not the portable Core contract.
- Initial routing requires adjacent sketch + new-body extrusion; checkpoints are not routed through the context in this slice.
- WVM safety uses an internal Onshape compatibility seam configured from the provider's effective mode. Context routing is allowed only for workspace mode (`w`); version/microversion modes fail closed to the legacy executor without changing `address_json`.

## Progress
- Exploration complete: factory is registered but not consumed; wholesale replacement is unsafe.
- BRIDGE-1 complete: the tool accepts the registered factory without changing routes, and Onshape-native receipt IDs remain outside Core contracts.
- First BRIDGE-2 attempt stopped before edits because `CadElementAddress` does not encode WVM while the context hardcodes workspace paths and legacy routing uses configured `DefaultWvm`.
- BRIDGE-2A complete: `Onshape:DefaultWvm` now configures the existing driver during bootstrap, and the registered Onshape factory exposes the unnormalized effective mode through an Onshape-only compatibility interface. Routing remains unchanged.
- The user selected an internal WVM compatibility seam: BRIDGE-2B routes only when the configured effective mode is exactly `w`, otherwise preserving legacy routing.
- Independent BRIDGE-2B verification rejected the first candidate because context failures exposed `CONTEXT_MODELING_FAILED` instead of existing `PROVIDER_MODELING_FAILED` / `MISSING_NATIVE_FEATURE_ID`; receipt mismatch after bind and bind/extrusion/blank-receipt edges needed focused coverage.
- BRIDGE-2B remediation preserves legacy public error codes and message shape: unsuccessful, failed, uncertain, and thrown context operations yield `PROVIDER_MODELING_FAILED`; successful operations without nonblank native receipt IDs yield `MISSING_NATIVE_FEATURE_ID`. A factory that does not advertise native receipts stays on legacy before bind; an advertised factory returning a non-receipt context fails closed after bind without legacy writes.
- BRIDGE-3 complete: local WireMock tests resolve `OnshapeSkillTools` and `IPartModelingContextFactory` through `ProviderBootstrap`. Eligible workspace rectangle and circle plans perform exactly frame-read, frame preflight, sketch write, and extrusion write with native receipt IDs and two feature writes. Polygon/add and checkpoint plans preserve legacy execution, while configured version mode uses only version endpoints and does not call workspace context endpoints.

## Verification evidence
- BRIDGE-1 RED: `VSTEST_CONNECTION_TIMEOUT=120 dotnet test tests/McpCad.Tests/McpCad.Tests.csproj --framework net8.0 --no-restore --filter 'FullyQualifiedName~OnshapePartOrchestratorToolTests|FullyQualifiedName~PartModelingRegistrationTests'` failed at compilation because `OnshapePartModelingReceipt` and the two-argument `OnshapeSkillTools` constructor did not exist.
- BRIDGE-1 GREEN: the same focused command passed 9 tests after adding the factory constructor and Onshape-only receipt seam; one prior post-implementation invocation exceeded the harness 120-second timeout after discovery, then the rerun completed successfully.
- BRIDGE-1 triangulation: the focused tests confirm the exact public two-string MCP signature, preserve the provider-only constructor path and legacy executor route, keep the injected factory unused until BRIDGE-2, and keep native ID properties outside `IPartModelingContext`.
- BRIDGE-2A RED: the focused command failed to compile because `IOnshapePartModelingContextFactoryCompatibility` did not exist.
- BRIDGE-2A GREEN: the focused command passed 43 tests after adding the seam and bootstrap flow; the new theory covers `w`, `v`, `m`, and an unknown mode without normalizing their values.
- BRIDGE-2B RED: the focused command failed at compilation because `OnshapePartModelingContextFactory.IsWorkspaceCompatibleWvm` did not exist.
- BRIDGE-2B candidate GREEN: the focused command passed 61 tests after adding the exact `w` predicate and bounded context bridge; `git diff --check` passed.
- Independent verification repeated 61/61 and clean diff-check but rejected semantic acceptance due to public error-code drift and uncovered receipt/bind/extrusion failure edges.
- BRIDGE-2B remediation GREEN: `VSTEST_CONNECTION_TIMEOUT=120 dotnet test tests/McpCad.Tests/McpCad.Tests.csproj --framework net8.0 --no-restore --filter 'FullyQualifiedName~OnshapePartOrchestratorToolTests|FullyQualifiedName~OnshapePartOrchestratorResultTests|FullyQualifiedName~PartModelingRegistrationTests'` passed 53/53. New focused cases cover bind throw; sketch and extrusion failed, uncertain, thrown, and blank receipt IDs; pre-bind receipt-capability fallback; and nonconforming post-bind receipt mismatch, with no retry or legacy write after context routing starts.
- BRIDGE-3 RED: the requested focused command failed 1/33 after the new eligible-rectangle test asserted the required context flow but the existing direct provider construction made only two legacy feature writes.
- BRIDGE-3 GREEN: `VSTEST_CONNECTION_TIMEOUT=120 dotnet test tests/McpCad.Tests/McpCad.Tests.csproj --framework net8.0 --no-restore --filter 'FullyQualifiedName~OnshapePartOrchestratorWireMockTests|FullyQualifiedName~OnshapePartModelingWireMockTests|FullyQualifiedName~ProviderBootstrapTests|FullyQualifiedName~PartModelingRegistrationTests'` passed 36/36. Tests are local WireMock only and cover DI factory resolution, workspace rectangle/circle flows, no duplicate eligible writes, polygon/add legacy flow, checkpoint legacy capture flow, and version-mode legacy routing.

## Delivery evidence
- Full net8 regression independently passed 701 tests with 7 skipped and 0 failed; `git diff --check` passed.
- Native review lineage `review-891b269e41af1417` approved the frozen candidate and its acknowledgement completed. The frozen projection also contained two pre-existing excluded `.atl/**` files; they remain outside intended delivery.
- At BRIDGE-4 closure no commit, staging, push, or PR had been performed for this feature. The user subsequently authorized commits only.
- `8c20a9e` `feat(onshape): expose native context receipts and workspace compatibility` (123 changed lines): receipt, injected factory, configured WVM, registration tests.
- `519ffc1` `feat(onshape): route eligible orchestrator plans through bound context` (529 changed lines): bounded route, validator mapping, failure and fallback unit tests. This cohesive unit exceeds the 400-line PR review budget; do not put it in a PR without explicit `size:exception` or a separately approved review strategy. One honest slicing pass found its test fixtures/failure matrix coupled to the routing behavior.
- `1b544b6` `test(onshape): prove context routing and isolate root probes` (204 changed lines): local DI/WireMock end-to-end proof and narrow root-probe isolation.
- Each staged diff passed `git diff --cached --check`; no commit contains `.atl/**`, `.pi/**`, or `.pi-subagents/**`. Final independent current-HEAD net8 regression passed 701, failed 0, skipped 7; `git diff --check` clean. Intermediate commits were not independently built; current HEAD was tested. `.atl/**` modifications and `.pi/**` / `.pi-subagents/**` untracked files remain outside delivery.

## BRIDGE-4 isolation investigation
- The first full net8 regression observed 700 passed, 1 failed, and 7 skipped. `onshape_orchestrate_part_PreservesLegacyCheckpointCaptureOrderWithRegisteredFactory` expected its three scenario requests but the WireMock listener log also contained two `GET /` requests. Focused BRIDGE-3 had passed.
- The current full-suite reproduction did not reoccur (701 passed, 0 failed, 7 skipped), confirming the failure is timing-dependent. The test infrastructure has no collection-level parallelization opt-out and many Onshape test classes independently call `WireMockServer.Start()`; WireMock's listener-wide `LogEntries` has no scenario correlation, so incidental unqueried root probes are captured with scenario traffic.
- Strict-TDD RED deterministically injected the observed two local `GET /` probes: the legacy raw-list assertion failed 0/1 because the five-entry log exceeded the exact three-request scenario. The test now filters only an unqueried `GET /`; it retains every `/api/...` entry, every non-GET root request, and queried root access. Its exact scenario assertion still requires feature write → shaded-view capture → feature write, so extra or duplicate modeling/capture requests fail.
- GREEN: the exact test passed 1/1, the required BRIDGE-3 focused suite passed 36/36, and the full net8 suite passed 701/701 with 7 skips. No production behavior changed.
- Native review approved and was acknowledged after the passing full regression. Excluded pre-existing `.atl/**` tracked changes remain outside intended delivery.

## Next step
- The bridge commits are prepared on the feature branch. Before any PR, decide how to handle the cohesive 529-line route commit's review-budget exception; no push or PR is authorized.
