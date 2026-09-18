# Tasks: Onshape/Linux part modeling

## Scope reconciliation

The current change is complete for its Onshape/Linux deliverable. Units 4, 5, and 6B are transferred—not implemented or validated—to the future change `inventor-windows-part-modeling`, which requires a later native planning launch and a configured Windows/Inventor execution boundary. The three reconciled rows below are checked because the current change's transfer is complete; they do not represent completed Inventor work. All prior Onshape implementation and review evidence is retained in `apply-progress.md`.

## Review Workload Forecast

| Field | Value |
|-------|-------|
| Estimated changed lines | Unit 1A actual: 181; Unit 1B actual: 307; combined: 488 across two slices; mechanical move: +48/-42 = 90 |
| 400-line budget risk | Low |
| Chained PRs recommended | Yes |
| Suggested split | Unit 1A contracts + contract tests → Unit 1B validation + validation tests |
| Delivery strategy | auto-chain; Unit 6A is the explicitly authorized Linux-verifiable slice |
| Chain strategy | feature-branch-chain |

Decision needed before apply: No
Chained PRs recommended: Yes
Chain strategy: feature-branch-chain
400-line budget risk: Low

## Reconciled Unit 1 completion

The logical review slices are below the 400-line budget: Unit 1A is 133 contract lines plus 48 contract-test lines (181); Unit 1B is 162 validation lines plus 145 validation-test lines (307). Their 488-line total is not one delivery unit. The actual assertion-preserving mechanical move is +48/-42 (90), not its former 85-line projection. These logical slice measurements are distinct from native incremental accounting; the historical 523-line native-budget failure remains preserved in `apply-progress.md`.

- [x] **REORGANIZE (mechanical, not TDD) —** Created `tests/McpCad.Tests/Modeling/PartModelingContractsTests.cs` and moved only the two contract-shape assertion groups plus `PublicMembersAcceptingNativeData` from `tests/McpCad.Tests/Modeling/PartModelingValidationTests.cs`; assertion bodies were preserved and `src/McpCad.Core/Modeling/PartModelingContracts.cs` was not changed for the move. <!-- sdd-owner: implementation -->
- [x] **VERIFY — Unit 1A contracts —** Independent no-build diagnostic evidence covers the contract fixture within the combined focused result (18 passed, 0 failed/skipped) and full regression (583 passed, 7 skipped, 0 failed). This deliberate `--no-build` diagnostic variant does not assert that the original isolated Unit 1A command was rerun verbatim. <!-- sdd-owner: implementation -->
- [x] **VERIFY — Unit 1B validation and null points —** Three new null-`Point2` cases first failed with `NullReferenceException`; after the authorized `PartModelingValidation.TryConvertPoint` guard, the independent combined no-build focused result was 18 passed, 0 failed/skipped and the full regression was 583 passed, 7 skipped, 0 failed. This is RED→GREEN evidence for the null correction, not fabricated TDD for the mechanical move. <!-- sdd-owner: implementation -->
- [x] **RECORD BUDGET —** Recorded the actual Unit 1A (181) and Unit 1B (307) logical slice counts and the +48/-42 mechanical diff in `openspec/changes/provider-neutral-part-modeling/apply-progress.md`; neither slice exceeds 400 changed lines. <!-- sdd-owner: implementation -->

## Released Unit 2A reader/parser slice

- [x] **COMPLETE — Unit 2A:** Implement the internal, target-bound read-only evaluator frame reader and bounded parser in `src/McpCad.Onshape/Modeling/OnshapePartModelingFrameReader.cs` with `tests/McpCad.Tests/Onshape/OnshapePartModelingFrameReaderTests.cs`. Cover the measured Top/Front/Right frames, exact current and measured legacy aliases, strict finite values, notices/skew rejection, malformed/unsupported/absent results, and prove the reader itself makes no model writes or public-API changes. Use [sanitized evidence](evidence/onshape-unit2-frame-summary.md); do not add an adapter. <!-- sdd-owner: implementation -->
- [x] **COMPLETE — Unit 2B:** Implement only the internal pure Onshape canonical-frame geometry mapper in `src/McpCad.Onshape/Modeling/OnshapePartModelingGeometryMapper.cs` with `tests/McpCad.Tests/Onshape/OnshapePartModelingGeometryMapperTests.cs`. Map normalized meter rectangle/circle definitions and signed extrusion direction through a validated measured frame; preserve four ordered rectangle corners and perform no adapter, HTTP, builder, or CAD mutation work. <!-- sdd-owner: implementation -->
- [x] **RELEASED — Unit 2C preflight adapter:** Implement the smallest internal, address-bound Onshape preflight adapter that validates portable input, reads the canonical datum frame, and maps normalized geometry. It MUST perform only the reader's GET/evaluator POST and no sketch/feature/CAD mutation, builder, `IPartModelingContext`, registration, or Unit 3 WireMock conformance work. Do not mark Unit 2 complete from Units 2A–2C alone. <!-- sdd-owner: implementation -->

## Held implementation units

- [x] **COMPLETE — Unit 3:** Implemented the address-bound Onshape context and local WireMock conformance coverage in `src/McpCad.Onshape/Modeling/OnshapePartModelingAdapter.cs` and `tests/McpCad.Tests/Onshape/OnshapePartModelingWireMockTests.cs`. <!-- sdd-owner: implementation -->
- [x] **COMPLETE — Unit 3 cancellation correction slice:** `R3-write-cancellation-state` marks a dispatched sketch or extrusion cancellation as `Uncertain`/`Unknown`, preserves pre-write cancellation, and prevents its repeat write. <!-- sdd-owner: implementation -->
- [x] **COMPLETE — Unit 3 stopped-context correction slice:** `R3-stopped-context-can-still-write` prevents a failed out-of-order extrusion from starting later sketch preflight or transport. <!-- sdd-owner: implementation -->
- [x] **RECONCILED — Unit 4 deferred:** Transfer the future Inventor native-frame seam and fake-boundary tests in `src/McpCad.Inventor/Modeling/InventorPartModelingNative.cs` and `tests/McpCad.Tests/Inventor/InventorPartModelingNativeTests.cs` to `inventor-windows-part-modeling`. No Inventor implementation or Linux validation is claimed. <!-- sdd-owner: implementation -->
- [x] **RECONCILED — Unit 5 deferred:** Transfer the future Inventor context/transform conformance in `src/McpCad.Inventor/Modeling/InventorPartModelingAdapter.cs` and `tests/McpCad.Tests/Inventor/InventorPartModelingTests.cs` to `inventor-windows-part-modeling`, dependent on Unit 4 and a configured Windows/Inventor boundary. No Inventor implementation or Linux validation is claimed. <!-- sdd-owner: implementation -->
- [x] **COMPLETE — Unit 6A (Onshape-only):** After Unit 3, implemented the smallest additive configured Onshape adapter-factory registration and Linux-verifiable compatibility coverage in `src/McpCad.Server/ProviderBootstrap.cs` and `tests/McpCad.Tests/Modeling/PartModelingRegistrationTests.cs`, without changing public MCP tools or schemas and without Inventor assemblies, Windows, live CAD, credentials, or external network. <!-- sdd-owner: implementation -->
- [x] **RECONCILED — Unit 6B deferred:** Transfer future Inventor factory registration in `src/McpCad.Server/ProviderBootstrap.Inventor.cs` and compatible registration coverage to `inventor-windows-part-modeling`, dependent on Units 4–5 and without changing public MCP tools or schemas. No Inventor registration is claimed. <!-- sdd-owner: implementation -->

## Evidence and limits

- Evidence read directly: `openspec/changes/provider-neutral-part-modeling/proposal.md`, `specs/provider-neutral-part-modeling/spec.md`, `design.md`, `tasks.md`, `apply-progress.md`, `openspec/config.yaml`, and `/tmp/mcp-cad-vstest-diag.QLAtSRVR/summary.txt`.
- The historical 90/180-second VSTest timeouts remain unexplained. The later no-build results do not prove a permanent repair; the tool-owned overflow log outside the diagnostic directory prevents a logdir-only containment claim.
- Unit 1 is settled only at its scoped Core/contracts/validation boundary. The completed delivery is the Onshape/Linux scope recorded above; it does not claim live CAD certification, Windows execution, a verify report, archive/sync readiness, commit, or delivery action.
- This reconciliation ran no tests, builds, runtime attempts, reviews, commits, cleanup, or status refresh. The parent owns the native status refresh.

## Readback

`skill_resolution: paths-injected` (`cognitive-doc-design`, `work-unit-commits`).
