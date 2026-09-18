# Apply progress: Provider-neutral part modeling

## Unit 2A evidence reconciliation

A sanitized durable receipt is recorded in [Onshape Unit 2 frame evidence](evidence/onshape-unit2-frame-summary.md). It verifies the tested default frames and positive asymmetric-prism experiment, plus the bounded evaluator response/parser facts needed for the reader. It releases only unchecked Unit 2A reader/parser implementation; no source implementation, test execution, adapter integration, model write, review approval, commit, or delivery occurred in this documentation reconciliation.

The prior Unit 2 HOLD is therefore stale only for the isolated reader/parser slice. The Unit 2 remainder, Unit 3 adapter work, Units 4–6 Inventor/registration work, and full provider-neutral outcome remain HOLD. `proposal_ready: false` in research is retained as historical gate status rather than recast as a current Unit 2A blocker.

## Current task reconciliation

The Unit 1 task ledger is settled from existing independent evidence: Unit 1A is 181 logical lines (133 contracts + 48 contract tests), Unit 1B is 307 (162 validation + 145 validation tests), and their 488-line total is two authorized slices, not one delivery. The actual mechanical move is +48/-42 = 90 lines; the historical 523-line native incremental-budget failure remains preserved below and is not rewritten as a logical-size measurement.

The deliberate no-build diagnostic variant passed the combined contract/validation focus (18/18) and full regression (583 passed, 7 skipped, 0 failed). It does not claim that the original isolated commands were rerun verbatim. Three null-`Point2` cases first produced `NullReferenceException`; the authorized guard then reached the 18/18 GREEN result. The mechanical move has no fabricated TDD claim.

The historical 90/180-second VSTest timeouts remain unexplained; the passing diagnostics do not prove a permanent fix. The tool-owned overflow log outside the diagnostic directory remains preserved, so logdir-only containment is not claimed. Units 2–6 remain HOLD; this does not claim provider completion, full-solution Inventor/Linux green, verify-report/archive readiness, review, commit, or delivery.

## Latest diagnostic regression result

This current result supersedes the infrastructure-blocked verification below, which remains historical evidence. Independent no-build diagnostics now passed: discovery exit 0 (5 seconds), focused 18/18 exit 0 (4 seconds), full regression **583 passed, 7 skipped, 0 failed** exit 0 (13 seconds). All used invocation-only `VSTEST_CONNECTION_TIMEOUT=120`; discovery/focused had `--diag`, full also had TRX and `--blame-hang-timeout 5m`. No permanent configuration change or infrastructure fix was made. Parent repeated the combined focused `--no-build --no-restore` command and independently observed 18/18 pass.

Evidence: `/tmp/mcp-cad-vstest-diag.QLAtSRVR/summary.txt`, SHA-256 `36a87708148201344c679e8da6ea0c488e18a59c5c2240521c6a30069cf02627`; exact commands, TRX and runner/testhost logs are recorded there. Each testhost negotiated protocol 7 and exited successfully. Before/after source and compiled-artifact inventories were unchanged during diagnosis. Core build success belongs to the preceding independent verification, not a build during this no-build sequence.

The earlier handshake timeout was **not reproduced**; its root cause is unknown. Root filesystem had approximately 320 MB available and resource pressure is only a hypothesis. No source changes, installs, environment-wide settings, process killing or CAD calls were used to make tests pass. The tool automatically wrote `/tmp/pi-bash-d160aff64b66e6c8.log` when truncating diagnostic output; that extra tool-owned log is preserved and explicitly outside the requested diagnostic directory, so strict logdir-only containment is not claimed. No other unexpected mutation or remaining own test processes was reported.

Scoped null-point fix and net8.0 regression now have passing evidence; this does not resolve the historical whole-solution Inventor/Linux build limitation, prove a permanent VSTest repair, or authorize Units 2–6, full-change verification/archive, commits or publication.


## Current null-point correction verification

This section supersedes older readiness claims below; historical evidence is retained.

- The user authorized the null-profile-point fix after the split. Three new cases first failed with `NullReferenceException`; `TryConvertPoint` now rejects null points with a typed validation error.
- Independent verifier reports the combined contract/validation command passed **18/18**, with no skipped tests.
- Full regression command `dotnet test tests/McpCad.Tests/McpCad.Tests.csproj --framework net8.0 --no-restore` aborted with exit 1 during VSTest protocol negotiation after 90 seconds. The invocation-only retry `VSTEST_CONNECTION_TIMEOUT=180 dotnet test tests/McpCad.Tests/McpCad.Tests.csproj --framework net8.0 --no-restore` also aborted with exit 1 after 180 seconds. Neither run produced a passing regression result.
- Independent Core build passed with zero warnings/errors; `git diff --check` passed. Verifier reports unchanged source status and no remaining matching runtime processes. No global timeout configuration was changed; no further retry is planned.
- Logical Unit 1B is the validation source and validation test files only (writer count 162 + 145 = 307 lines). Unit 1A remains separate (133 + 48 = 181); combined 488 is not a single slice. Native incremental accounting remains provider-owned.
- Outcome remains **partial: scoped correction passes, full regression verification blocked by test infrastructure**. Do not check the full verification tasks or claim full Unit 1 completion. The next step is a separate diagnosis of the VSTest handshake failure, not another blind timeout increase.


## Native-reset recovery note (current authority)

Parent performed a native objective reset with a fresh revision and explicitly authorized two independent review slices: Unit 1A contracts plus contract-shape tests, then Unit 1B validation plus validation tests. **Neither Unit 1A nor Unit 1B is complete, and no current RED/GREEN/TRIANGULATE/REFACTOR or verification result exists.** The existing source is only partial implementation awaiting the scoped reorganization and fresh independent verification.

The historical sections below are preserved unchanged as failed-attempt provenance: 15 focused and 580 regression writer results, the Linux Inventor-interop solution-build failure, and the failed 452 source/test / 523 native-budget lifetime measurements. They must not be reused as current completion or TDD evidence. The current recovery is limited to moving the two contract-shape assertion groups into `tests/McpCad.Tests/Modeling/PartModelingContractsTests.cs`, retaining validation assertions in `tests/McpCad.Tests/Modeling/PartModelingValidationTests.cs`, and then recording fresh results. No source fix, native behavior, provider/adaptor work, commit, PR topology, or publication is authorized. Both adapter Units 2–6 remain HOLD; AUTO/OpenSpec `ask-on-risk` remains in force and actual chain topology is deferred.

## Current bounded split evidence (2026-09-10)

Authorized scope was the mechanical Unit 1A test split only. No production, provider, adapter, registration, live-CAD, review, commit, staging, or publication action occurred.

- Completed persisted checkbox: only `REORGANIZE (mechanical, not RED/GREEN)` is now `[x]`. Per parent instruction, the Unit 1A verify, Unit 1B verify, and budget-record rows remain unchecked; neither subunit nor Unit 1 is claimed complete.
- Created `tests/McpCad.Tests/Modeling/PartModelingContractsTests.cs` (48 lines) and moved, without assertion edits, `Contracts_ExposeClosedEnumsTwoOperationResultAndNativeFreeReferences`, `PortableValuesAreInitOnlyAndReferencesExposeOnlyNativeFreeFactories`, and `PublicMembersAcceptingNativeData` from `tests/McpCad.Tests/Modeling/PartModelingValidationTests.cs` (164 to 122 lines). The moved definitions occur exactly twice in the new fixture and zero times in the validation fixture. The pre-split 15 tests equal the post-split 2 contract + 13 validation tests; no assertion was lost.
- Actual reorganization measurement is **+48 / -42 = 90 changed lines**, rather than the planning projection +48/-37. The five-line variance comes from the actual named moved blocks and their surrounding whitespace in the current untracked baseline; it was recorded rather than compressed. Logical slice counts are Unit 1A **133 Core-contract + 48 contract-test = 181** and Unit 1B **155 Core-validation + 122 retained-validation-test = 277**. Each independent slice is below 400; their 458-line aggregate is not one authorized delivery unit.
- This repairs the prior 452-line single-Unit-1 budget failure for this bounded split only: the former evidence is remediated by two independently below-budget logical slices, not by a size exception and not by a claim that Unit 1 is green.

### Fresh verification

| Command | Exit | Result |
| --- | ---: | --- |
| `dotnet test tests/McpCad.Tests/McpCad.Tests.csproj --framework net8.0 --no-restore --filter 'FullyQualifiedName~McpCad.Tests.Modeling.PartModelingValidationTests'` (pre-move safety net) | 0 | 15 passed, 0 failed |
| `dotnet test tests/McpCad.Tests/McpCad.Tests.csproj --framework net8.0 --no-restore --filter 'FullyQualifiedName~McpCad.Tests.Modeling.PartModelingValidationTests|FullyQualifiedName~McpCad.Tests.Modeling.PartModelingContractsTests'` | 0 | 15 passed, 0 failed |
| `dotnet test tests/McpCad.Tests/McpCad.Tests.csproj --framework net8.0 --no-restore --filter 'FullyQualifiedName~McpCad.Tests.Modeling.PartModelingContractsTests'` | 0 | 2 passed, 0 failed |
| `dotnet test tests/McpCad.Tests/McpCad.Tests.csproj --framework net8.0 --no-restore --filter 'FullyQualifiedName~McpCad.Tests.Modeling.PartModelingValidationTests'` (post-move) | 0 | 13 passed, 0 failed |
| `dotnet test tests/McpCad.Tests/McpCad.Tests.csproj --framework net8.0 --no-restore` | 0 | 580 passed, 0 failed, 7 skipped, 587 total |
| `dotnet build src/McpCad.Core/McpCad.Core.csproj --no-restore` | 0 | 0 warnings, 0 errors |
| `git diff --check` | 0 | clean |

The historical Linux full-solution build failure caused by unavailable Inventor interop is retained below as failed provenance. It was not rerun and is not included in this bounded split's passing claim.

### TDD Cycle Evidence

| Task | Test file | Layer | Safety net | RED | GREEN | TRIANGULATE | REFACTOR |
| --- | --- | --- | --- | --- | --- | --- | --- |
| Mechanical contract-test split | `tests/McpCad.Tests/Modeling/PartModelingContractsTests.cs` | Unit | 15/15 existing validation tests before the move | N/A — authorized assertion-preserving move; no behavioral change or production edit | 2/2 contract tests and 13/13 retained validation tests after move | N/A — no new behavior | N/A — no refactor/production edit |

No new tests or production code were written. Historical RED evidence below remains attributed to its prior attempt and is not reused as a new cycle.

### Remaining current task lines

- [ ] **VERIFY —** Run the Unit 1A focused and full test commands below after the move. Record the exact exit code and test totals in `openspec/changes/provider-neutral-part-modeling/apply-progress.md`; if either command fails, stop without changing contracts or validation and leave this checkbox unchecked. <!-- sdd-owner: implementation -->
- [ ] **VERIFY (recovery, not fabricated TDD) —** Run the Unit 1B focused and full test commands below against the retained validation tests and `src/McpCad.Core/Modeling/PartModelingValidation.cs`. Do not add a RED/GREEN/TRIANGULATE/REFACTOR claim from historical output; if a test fails, record the exact failure and stop rather than fixing code or adding tests in this scope. <!-- sdd-owner: implementation -->
- [ ] **RECORD BUDGET —** Record the actual per-slice diff counts for Unit 1A and Unit 1B separately, including the test-move +48/-37 mechanical diff, in `openspec/changes/provider-neutral-part-modeling/apply-progress.md`. Keep each logical slice at or below 400 additions plus deletions; do not aggregate them as one authorized delivery unit. <!-- sdd-owner: implementation -->
- [ ] **HOLD — Unit 2:** After explicit release of the Onshape REST `BTMSketch-151` datum/frame bridge and bounded FeatureScript value/version evidence gap, complete the existing described Onshape frame-reader implementation and parser/preflight tests in `src/McpCad.Onshape/Modeling/OnshapePartModelingFrameReader.cs` and `tests/McpCad.Tests/Onshape/OnshapePartModelingFrameReaderTests.cs`, including proof that inspection failure prevents writes. <!-- sdd-owner: implementation -->
- [ ] **HOLD — Unit 3:** After explicit release and completion of Units 1–2, complete the existing described address-bound Onshape context and local WireMock conformance tests in `src/McpCad.Onshape/Modeling/OnshapePartModelingAdapter.cs` and `tests/McpCad.Tests/Onshape/OnshapePartModelingWireMockTests.cs`, including transformed geometry, exact sketch linkage, explicit new-body direction, and stopped-state behavior. <!-- sdd-owner: implementation -->
- [ ] **HOLD — Unit 4:** After explicit release, Unit 1 completion, and availability of the configured Windows test boundary, complete the existing described pinned Inventor native-frame seam and fake-boundary tests in `src/McpCad.Inventor/Modeling/InventorPartModelingNative.cs` and `tests/McpCad.Tests/Inventor/InventorPartModelingNativeTests.cs`, including unique frame selection and zero mutation on preflight failure. <!-- sdd-owner: implementation -->
- [ ] **HOLD — Unit 5:** After explicit release and completion of Units 1 and 4, complete the existing described Inventor context, transform, and Windows conformance work in `src/McpCad.Inventor/Modeling/InventorPartModelingAdapter.cs` and `tests/McpCad.Tests/Inventor/InventorPartModelingTests.cs`, including exact profile/extrusion binding, direction mapping, uncertainty, and stopped-state behavior. <!-- sdd-owner: implementation -->
- [ ] **HOLD — Unit 6:** After explicit release and completion of Units 3 and 5, complete the existing described additive configured registration and compatibility tests in `src/McpCad.Server/ProviderBootstrap.cs`, `src/McpCad.Server/ProviderBootstrap.Inventor.cs`, and `tests/McpCad.Tests/Modeling/PartModelingRegistrationTests.cs`, without changing public tools or schemas. <!-- sdd-owner: implementation -->

## Status

**Partial / blocked by the authorized Unit 1 400-line limit.** Parent status consumed: `gentle-ai.sdd-status@2`, change `provider-neutral-part-modeling`, OpenSpec, apply ready, repo-local root `/home/samael/Escritorio/Programacion/work/fisica/mcp-cad`, allowed edit root that same workspace, no action-context warnings. Only Unit 1 surfaces were edited; no provider, frame, adapter, registration, plan engine, live CAD, commit, PR, or review work was started.

## Completed and persisted tasks

- `[x]` RED — test contract first.
- `[x]` GREEN — additive Core contracts and pure validation.

The TRIANGULATE and REFACTOR rows remain unchecked: their required final source-plus-test budget condition is not met (452 authored lines, 52 over the authorized 400). Unit 1 and the full change are **not complete**.

## Files changed

- `src/McpCad.Core/Modeling/PartModelingContracts.cs` — immutable portable values/enums, typed result/error/capability declarations, opaque native-ID-free reference factories, and `IPartModelingContext` interface only.
- `src/McpCad.Core/Modeling/PartModelingValidation.cs` — pure deterministic meter normalization and typed invalid-input results.
- `tests/McpCad.Tests/Modeling/PartModelingValidationTests.cs` — unit coverage for contract shape, immutability, units, finite/positive/degenerate/enum/collapse inputs.
- `openspec/changes/provider-neutral-part-modeling/tasks.md` — persisted RED/GREEN checkbox updates only.

Actual new-file counts: contracts 133, validation 155, tests 164; **452 source-plus-test lines**. This exceeds the Unit 1 limit by 52. Artifact lines are excluded from that count.

## TDD Cycle Evidence

| Task | Test file | Layer | Safety Net | RED | GREEN | TRIANGULATE | REFACTOR |
|---|---|---|---|---|---|---|---|
| RED | `tests/McpCad.Tests/Modeling/PartModelingValidationTests.cs` | Unit | N/A (new files) | Focused command exited 1: `CS0234` because `McpCad.Core.Modeling` did not exist | Focused command passed 4/4 after contracts/validation | N/A | N/A |
| GREEN | `tests/McpCad.Tests/Modeling/PartModelingValidationTests.cs` | Unit | N/A (new files) | Same contract-first RED | Passed 4/4 | Added all-unit/equivalence/invalid-input/reference-shape cases | N/A |
| TRIANGULATE | `tests/McpCad.Tests/Modeling/PartModelingValidationTests.cs` | Unit | N/A (new files) | Focused command exited 1: null extrusion caused `NullReferenceException` in `ValidateExtrusion` | Added typed null-definition failure; passed 15/15 | Passed 15/15 | Refactored duplicate positive-length checks; passed 15/15, but budget gate blocks task completion |

Pure functions created: `ValidateSketch` and `ValidateExtrusion` with private conversion helpers. No mocks or live CAD were used.

## Verification evidence

1. `dotnet test tests/McpCad.Tests/McpCad.Tests.csproj --framework net8.0 --no-restore --filter FullyQualifiedName~McpCad.Tests.Modeling.PartModelingValidationTests`
   - RED exact failure: exit 1; `PartModelingValidationTests.cs(2,19): error CS0234: ... 'Modeling' no existe en el espacio de nombres 'McpCad.Core'`.
   - First GREEN attempt: exit 1 due test-only `CS0411` generic inference errors at test lines 49, 51, and 53 for `BeEquivalentTo`; corrected to `Equal` before proceeding.
   - GREEN: exit 0, passed 4/4.
   - TRIANGULATE RED: exit 1, passed 14/15; `Validation_ReturnsTypedFailureForNullExtrusionDefinition` failed with `System.NullReferenceException` at `PartModelingValidation.ValidateExtrusion` line 32.
   - TRIANGULATE GREEN: exit 0, passed 15/15.
   - REFACTOR: exit 0, passed 15/15.
2. `dotnet test tests/McpCad.Tests/McpCad.Tests.csproj --framework net8.0 --no-restore`
   - Exit 0: passed 580, failed 0, skipped 7, total 587. The seven skipped tests are existing Inventor integration tests.
3. `dotnet build src/mcp-cad.sln --no-restore`
   - Exit 1: `McpCad.Core`, tests, and non-Inventor projects built; `McpCad.Inventor` failed with 11 `CS0246` errors because the `Inventor` namespace / `Application` type is unavailable in this Linux environment (for example `IntersectionMerger.cs(1,7)` and `InventorDriver.cs(3,16)`). No Inventor files were edited or fixed.
4. `git diff --check`
   - Exit 0; clean. Because new source/test files are untracked, the explicit `wc -l` counts above are the authoritative workload measurement.

Existing compiler warnings appeared during focused/full test compilation in unrelated Onshape mock/WireMock test files; they did not fail the test commands.

## Deviation, boundary, and remaining work

No design deviation: the public factories take no native ID, address, target, COM object, revision, or evidence data, while adapter-owned context validation remains future scope. The public factories are intentionally native-ID-free so future adapter assemblies can issue opaque references; adapters must still enforce context ownership from their private maps.

Workload/PR boundary: one additive Unit 1 work unit only; no PR, commit, chain, or delivery action. Stop here because 452 exceeds the authorized 400-line limit. Required decision: authorize a new coherent scoped delivery boundary or explicitly accept a size exception; neither is inferred.

Exact unchecked implementation lines:

- [ ] **TRIANGULATE —** Extend `tests/McpCad.Tests/Modeling/PartModelingValidationTests.cs` with independent cases for all three units, equivalent converted values, enum casts outside the declared values, `NaN`/infinity, zero/negative radius or depth, equal rectangle axes, and overflow or conversion-collapse boundaries. Add only the smallest pure validation correction required by each newly failing case in `src/McpCad.Core/Modeling/PartModelingValidation.cs`; keep adapters, transforms, and provider behavior unmodeled. Verify the focused filter remains green and the three Unit 1 files remain within 400 authored changed lines; rollback is removal of the added cases and any matching validation-only correction. <!-- sdd-owner: implementation -->
- [ ] **REFACTOR —** Refactor only `src/McpCad.Core/Modeling/PartModelingContracts.cs`, `src/McpCad.Core/Modeling/PartModelingValidation.cs`, and `tests/McpCad.Tests/Modeling/PartModelingValidationTests.cs` to remove duplication and clarify names without changing the established contract or expanding scope. Re-run the focused filter and then the required Unit 1 regression command `dotnet test tests/McpCad.Tests/McpCad.Tests.csproj --framework net8.0 --no-restore`; record exact results and the final authored changed-line count. If that count exceeds 400, stop rather than code-golf or start another unit. Rollback is deletion of the three Unit 1 files as one additive boundary. <!-- sdd-owner: implementation -->
- [ ] **HOLD — Unit 2:** After explicit release of the Onshape REST `BTMSketch-151` datum/frame bridge and bounded FeatureScript value/version evidence gap, complete the existing described Onshape frame-reader implementation and parser/preflight tests in `src/McpCad.Onshape/Modeling/OnshapePartModelingFrameReader.cs` and `tests/McpCad.Tests/Onshape/OnshapePartModelingFrameReaderTests.cs`, including proof that inspection failure prevents writes. <!-- sdd-owner: implementation -->
- [ ] **HOLD — Unit 3:** After explicit release and completion of Units 1–2, complete the existing described address-bound Onshape context and local WireMock conformance tests in `src/McpCad.Onshape/Modeling/OnshapePartModelingAdapter.cs` and `tests/McpCad.Tests/Onshape/OnshapePartModelingWireMockTests.cs`, including transformed geometry, exact sketch linkage, explicit new-body direction, and stopped-state behavior. <!-- sdd-owner: implementation -->
- [ ] **HOLD — Unit 4:** After explicit release, Unit 1 completion, and availability of the configured Windows test boundary, complete the existing described pinned Inventor native-frame seam and fake-boundary tests in `src/McpCad.Inventor/Modeling/InventorPartModelingNative.cs` and `tests/McpCad.Tests/Inventor/InventorPartModelingNativeTests.cs`, including unique frame selection and zero mutation on preflight failure. <!-- sdd-owner: implementation -->
- [ ] **HOLD — Unit 5:** After explicit release and completion of Units 1 and 4, complete the existing described Inventor context, transform, and Windows conformance work in `src/McpCad.Inventor/Modeling/InventorPartModelingAdapter.cs` and `tests/McpCad.Tests/Inventor/InventorPartModelingTests.cs`, including exact profile/extrusion binding, direction mapping, uncertainty, and stopped-state behavior. <!-- sdd-owner: implementation -->
- [ ] **HOLD — Unit 6:** After explicit release and completion of Units 3 and 5, complete the existing described additive configured registration and compatibility tests in `src/McpCad.Server/ProviderBootstrap.cs`, `src/McpCad.Server/ProviderBootstrap.Inventor.cs`, and `tests/McpCad.Tests/Modeling/PartModelingRegistrationTests.cs`, without changing public tools or schemas. <!-- sdd-owner: implementation -->

Rollback boundary: remove the three additive Unit 1 source/test files together; no existing provider behavior was touched.

## Unit 1B null profile-point correction attempt (current)

User-authorized scope is only `PartModelingValidation.TryConvertPoint` and its retained validation fixture; no provider, interface, registration, live-CAD, commit, staging, or delivery action occurred. Parent status consumed: explicit `provider-neutral-part-modeling`, OpenSpec apply ready, repo-local allowed root with no warnings; Units 2–6 remain HOLD.

### TDD Cycle Evidence

| Task | Test file | Layer | Safety net | RED | GREEN | TRIANGULATE | REFACTOR |
| --- | --- | --- | --- | --- | --- | --- | --- |
| Null `Point2` validation | `tests/McpCad.Tests/Modeling/PartModelingValidationTests.cs` | Unit | Superseded by the required new-test-first RED | Exit 1: 3/18 failures, each `NullReferenceException` at `TryConvertPoint` | Blocked: runner aborted after a 90-second VSTest protocol-negotiation timeout | Three table-driven cases: first rectangle corner, opposite rectangle corner, circle center | None; local guard is already minimal |

The test asserts no exception, `IsValid == false`, null normalized value, and `DegenerateGeometry`. The minimal attempted GREEN changes `TryConvertPoint` to accept `Point2?` and return a typed `DegenerateGeometry` failure with default/null output when its point is null. It is not claimed green until a fresh focused run completes.

| Command | Exit | Result |
| --- | ---: | --- |
| `dotnet test tests/McpCad.Tests/McpCad.Tests.csproj --framework net8.0 --no-restore --filter 'FullyQualifiedName~McpCad.Tests.Modeling.PartModelingValidationTests|FullyQualifiedName~McpCad.Tests.Modeling.PartModelingContractsTests'` (RED) | 1 | 15 passed, 3 failed, 18 total; all three failed with `NullReferenceException` from `TryConvertPoint` line 102. |
| Same command after the guard | 1 | Aborted before results: VSTest protocol negotiation timed out after 90 seconds. |
| Regression, Core build | Not run | Stopped on the unexpected focused-run infrastructure failure. |
| `git diff --check` | 0 | Clean; no `vstest`, `testhost`, or matching test process remained after the aborted run. |

No task checkbox changed: this correction has no dedicated task row and no implementation task is complete while GREEN is unconfirmed; VERIFY and HOLD rows were left untouched. Current Unit 1B logical source-plus-test length is **307** lines (`162 + 145`), up from the recorded 277; this correction adds **+7 source / +23 test = +30** lines, below 400. This progress addition is the only documentation delta. Resume with the same focused command, then the requested regression and Core build on a successful GREEN.

## Unit 2A frame reader/parser attempt (incomplete)

Parent authority consumed: `gentle-ai.sdd-status@2`, change `provider-neutral-part-modeling`, OpenSpec, apply ready, repository-local workspace `/home/samael/Escritorio/Programacion/work/fisica/mcp-cad`, allowed root that workspace, no action-context warnings. Scope was only released Unit 2A and its two source/test surfaces; Unit 2 remainder and Units 3–6 remain HOLD. CodeGraph exploration was attempted before fallback reads but timed out; targeted repository reads then established the existing `OnshapeClient.SendJsonAsync` transport, current `GET /features` library-version source, WireMock convention, and InternalsVisibleTo test seam.

### Attempted implementation

- Added `src/McpCad.Onshape/Modeling/OnshapePartModelingFrameReader.cs` (**238 lines**) with an internal target-bound reader. It obtains `libraryVersion` from the existing v6 features GET, posts a fixed two-argument FeatureScript lambda to the v17 evaluator, uses only protocol datum IDs JDC/JCC/JEC, and parses exact current (`BTFSValueArray-1499`, `BTFSValueNumber-772`, `BTFSValueString-1422`) plus measured fully-qualified legacy aliases.
- The parser rejects notices, microversion skew, missing/malformed/unsupported values, numeric strings, non-finite numbers, duplicate/missing rows, and non-rigid/right-handed bases through `OnshapePartModelingFrameReadException`. It preserves returned axes; it does not guess or normalize a basis. The reader does not invoke model feature endpoints. Existing client behavior remains unchanged: its bounded 429 retry may repeat the read-only evaluator POST, which does not create or mutate model features.
- Added `tests/McpCad.Tests/Onshape/OnshapePartModelingFrameReaderTests.cs` (**140 lines**) with synthetic sanitized WireMock fixtures for all three measured frames in current and complete legacy aliases, malformed-response rejection, pre-cancellation, library-version absence, and only GET/featurescript POST transport assertions. No live CAD, credentials, configuration, public provider-interface, adapter, or model-write work occurred.

Combined source/test line count is **378**, within the approved 400-line Unit 2A boundary. This is a line-count forecast for the currently untracked files, not completed-diff evidence.

### TDD Cycle Evidence

| Task | RED | GREEN attempt 1 | GREEN correction attempt 2 | TRIANGULATE / REFACTOR |
|---|---|---|---|---|
| Unit 2A reader/parser | Focused command exited 1 as intended: reader type did not exist (`CS0246`). | Exited 1: the new typed exception incorrectly derived from sealed `CadProviderOperationException` (`CS0509`). | Exited 1: Onshape assembly built after the base-class correction, but the test fixture lacks `using McpCad.Onshape.Modeling`, so `OnshapePartModelingFrameReader` remains unresolved (`CS0246`). | Not reached. The parent-authorized two implementation attempts are exhausted; no further source or test retry was run. |

| Command | Exit | Result |
|---|---:|---|
| `dotnet test tests/McpCad.Tests/McpCad.Tests.csproj --framework net8.0 --no-restore --filter 'FullyQualifiedName~OnshapePartModelingFrameReaderTests'` (RED) | 1 | Expected missing-reader `CS0246`. |
| Same focused command (GREEN attempt 1) | 1 | `CS0509`: typed exception derived from sealed `CadProviderOperationException`. |
| Same focused command (GREEN correction attempt 2) | 1 | Onshape project built; test fixture `CS0246` due its missing Modeling namespace import. |
| `git diff --check` | 0 | Clean for tracked diff; reader/test files are untracked. |

Evidence logs are retained privately in `/tmp/onshape-reader-2a.86WeYKVM`; `red.log` SHA-256 `a775f9d453ad0cb560e8d4097d24b136be8c6d83b90192daeecc723bb7e47b39`, `green.log` SHA-256 `6058a5946be46a7dd5c42ef2d9b830a813e4f51542f8806c4794b415e50338e4`, and `green-correction.log` SHA-256 `1d48f773c217debe229698fae4e2ce2c655e397ea2d36c24944bae632474e592`.

### Task status and boundary

The Unit 2A checkbox remains unchecked because its focused test is not green. No persisted task checkbox changed in this attempt. Exact remaining released task line:

- [ ] **RELEASED — Unit 2A:** Implement the internal, target-bound read-only evaluator frame reader and bounded parser in `src/McpCad.Onshape/Modeling/OnshapePartModelingFrameReader.cs` with `tests/McpCad.Tests/Onshape/OnshapePartModelingFrameReaderTests.cs`. Cover the measured Top/Front/Right frames, exact current and measured legacy aliases, strict finite values, notices/skew rejection, malformed/unsupported/absent results, and prove the reader itself makes no model writes or public-API changes. Use [sanitized evidence](evidence/onshape-unit2-frame-summary.md); do not add an adapter. <!-- sdd-owner: implementation -->

Remaining work requires parent authorization for an additional attempt: add the single missing test namespace import, re-run the focused test, then perform the required triangulation/refactor and scoped regression only if focused GREEN succeeds. No delivery/review/lifecycle action is requested or performed.

## Authorized parser split: verified; transport pending

The user subsequently authorized splitting parser and transport after native accounting recorded 414 lines. The interrupted history above is preserved; its old `/tmp` logs are now absent. Native authority, not individual compiler invocations, governs attempts.
The extracted parser rejects malformed root/row arrays with typed failures. Fresh behavioral RED: 14 passed, 3 failed; independent GREEN: 24 passed. Regression excluding the pending `OnshapePartModelingFrameReaderTests`: 607 passed, 7 skipped, 0 failed (not full-suite success).
Fresh evidence: `/tmp/onshape-parser-behavior-red.55pTjvOS/red.log` and `/tmp/onshape-parser-green.UHIOdvJR/`. Reader test compilation is repaired; its FeatureScript/transport corrections remain pending. Unit 2A stays unchecked; no adapter, live CAD, or delivery approval is implied.

## Transport correction (awaiting independent verification)

Behavioral RED was 15 passed / 8 failed; focused GREEN is 23 passed and full net8 regression is 630 passed / 7 skipped. Evidence is `/tmp/onshape-reader-transport.qm31YWAL/`; no Unit 2A checkbox changed, and transport remains awaiting independent verification.

## Unit 2A independent verification complete

Contract readback caught two evaluator-signature regressions despite mocks passing. New regression assertions produced 6 failures / 17 passes; restoring the two-argument lambda and `alignCanonically(context, ...)` resolved both without adding a JSON `queries` payload.
Independent final verification: 47 focused passed; full net8 test project 630 passed, 7 skipped, 0 failed. Evidence: `/tmp/onshape-unit2a-final.106inUmj/`; regression SHA-256 `7b0aa76f7866e13be90e253d9ed0b5d9045968c71a32916a2726bdc181ac6678`.
Unit 2A is checked complete, comprising the separate internal frame parser/reader and their tests. Unit 2 remainder and adapters remain HOLD. This is offline contract verification, not a new live CAD run, full-solution build, native review approval, or delivery authorization.

## Unit 2B mapper: implementation and verification

Tests were written first; RED correctly failed for missing mapper types. Two GREEN compiler invocations then failed (`CanonicalFrame` member-name collision, followed by an unresolved switch-expression best type). That was an intermediate, unverified state; compiler invocations do not determine native attempt authority.

### Unit 2B independently verified

After compilation fixes, a behavioral overflow regression produced 12 passes / 1 failure. Finite input coordinates can overflow during projection; the mapper now rejects non-finite projected coordinates with a typed failure. World-coordinate assertions use per-component tolerance and invalid-frame cases are isolated.
Independent final result: 13 focused passed; full net8 test project 643 passed, 7 skipped, 0 failed. Evidence: `/tmp/onshape-mapper-final.BOYkhqIY/`; regression SHA-256 `d3f9e7f415372a8f5aa6785ab345bff3db2bfae67802147fe04f9771cd51d9d9`.
Unit 2B is checked complete for pure mapping only. No adapter, HTTP, builder, live CAD mutation, native review approval, commit, or delivery work occurred; Unit 2 remainder remains HOLD.

## Unit 2C address-bound preflight adapter

The vague Unit 2 remainder is reconciled as a coherent **187-line** work unit (50 production + 137 tests), below the 400-line review budget. It is deliberately a preflight-only adapter, not Unit 3: it validates portable requests, reads the target-bound datum frame, then maps normalized geometry. It owns no builders, `IPartModelingContext`, registration, sketch/feature request, or model mutation. Its only transport is the reader's existing feature-list GET and fixed evaluator POST, locally exercised against WireMock; no live CAD, credentials, commits, staging, cleanup, publication, or delivery action occurred.

- Persisted task update: `RELEASED — Unit 2C preflight adapter` is `[x]` in `tasks.md`.
- Added `src/McpCad.Onshape/Modeling/OnshapePartModelingPreflightAdapter.cs`.
- Added `tests/McpCad.Tests/Onshape/OnshapePartModelingPreflightAdapterTests.cs`.
- No design deviation: this is the smallest read/map bridge explicitly requested by the user. Unit 3 mutation/context and all later held work remain unstarted.

### TDD Cycle Evidence

| Stage | Evidence |
| --- | --- |
| RED | New focused test was written first. `dotnet test tests/McpCad.Tests/McpCad.Tests.csproj --framework net8.0 --no-restore --filter 'FullyQualifiedName~OnshapePartModelingPreflightAdapterTests'` exited 1 with `CS0246` because `OnshapePartModelingPreflightAdapter` did not exist. |
| GREEN | Added the minimal coordinator and typed validation exception. The initial runner attempt timed out without test results; a diagnostic run exposed only two new-test assertion defects, which were corrected without changing production code. The focused command with `VSTEST_CONNECTION_TIMEOUT=120` then passed 3/3. |
| TRIANGULATE | Added independent circle + inverted-normal coverage, proving mapped center/radius and direction flip through reader-to-mapper composition. One 120-second VSTest protocol-negotiation timeout produced no results; `VSTEST_CONNECTION_TIMEOUT=180` then passed 4/4. |
| REFACTOR | Reviewed the coordinator and test helper; no safe simplification remained, so no production refactor was made. The required full regression below remained green. |

### Verification

| Command | Exit | Result |
| --- | ---: | --- |
| Focused RED command above | 1 | Expected missing-adapter `CS0246`. |
| Focused GREEN, `VSTEST_CONNECTION_TIMEOUT=120` | 0 | 3 passed, 0 failed, 0 skipped. |
| Focused triangulation, `VSTEST_CONNECTION_TIMEOUT=180` | 0 | 4 passed, 0 failed, 0 skipped. |
| `dotnet test tests/McpCad.Tests/McpCad.Tests.csproj --framework net8.0 --no-restore` | 0 | 647 passed, 7 skipped, 0 failed (654 total). |
| `git diff --check` | 0 | Clean; this command does not include untracked files. |

The full regression's seven skips are existing Inventor integration tests. The focused VSTest protocol timeouts are recorded as infrastructure events, not passing evidence. Existing unrelated nullable/unused-field compiler warnings were emitted by the test project.

### Remaining implementation tasks

- [ ] **HOLD — Unit 3:** After explicit release and Units 1–2, implement the address-bound Onshape context and local WireMock conformance coverage in `src/McpCad.Onshape/Modeling/OnshapePartModelingAdapter.cs` and `tests/McpCad.Tests/Onshape/OnshapePartModelingWireMockTests.cs`. <!-- sdd-owner: implementation -->
- [ ] **HOLD — Unit 4:** After explicit release, Unit 1 completion, and a configured Windows boundary, implement the Inventor native-frame seam and fake-boundary tests in `src/McpCad.Inventor/Modeling/InventorPartModelingNative.cs` and `tests/McpCad.Tests/Inventor/InventorPartModelingNativeTests.cs`. <!-- sdd-owner: implementation -->
- [ ] **HOLD — Unit 5:** After explicit release and Units 1 and 4, implement Inventor context/transform conformance in `src/McpCad.Inventor/Modeling/InventorPartModelingAdapter.cs` and `tests/McpCad.Tests/Inventor/InventorPartModelingTests.cs`. <!-- sdd-owner: implementation -->
- [ ] **HOLD — Unit 6:** After explicit release and Units 3 and 5, implement configured registration and compatibility coverage in `src/McpCad.Server/ProviderBootstrap.cs`, `src/McpCad.Server/ProviderBootstrap.Inventor.cs`, and `tests/McpCad.Tests/Modeling/PartModelingRegistrationTests.cs`, without changing public tools or schemas. <!-- sdd-owner: implementation -->

### Status and delivery boundary

Consumed `gentle-ai.sdd-status@2`: change `provider-neutral-part-modeling`, OpenSpec, `applyState: ready`, `nextRecommended: apply`, repo-local workspace and allowed root `/home/samael/Escritorio/Programacion/work/fisica/mcp-cad`, with no native blockers. CodeGraph was present but its required explore request timed out, so targeted reads were used as fallback. Session delivery is `ask-on-risk`; this 187-line Unit 2C work unit is the review boundary and does not require a size exception, PR, chain selection, commit, or review. Parent-owned lifecycle actions: none. `skill_resolution: paths-injected`.

## Unit 3 Onshape context — GREEN blocked by test infrastructure

The user explicitly released Unit 3 only. Work remains local/offline: the new adapter test uses only a WireMock localhost server; no credentials, live CAD, external network, registration, Inventor code, commit, staging, push, cleanup, or publication action was used. Units 4–6 remain HOLD.

- Added `src/McpCad.Onshape/Modeling/OnshapePartModelingAdapter.cs` (206 lines): a target-bound, one-shot `IPartModelingContext` implementation that invokes the existing read-only preflight seam, maps a rectangle/circle through existing builders, posts a sketch and exact native-ID-linked new-body extrusion, reports unsupported optional capabilities, rejects foreign references, and marks write failures as uncertain without retry or rollback.
- Added `tests/McpCad.Tests/Onshape/OnshapePartModelingWireMockTests.cs` (137 lines): WireMock-only rectangle mapping/linkage/new-body assertions, inspection-failure no-write behavior, and cross-context reference rejection.
- Current authored Unit 3 source-plus-test count is **343 lines**, below the 400-line budget. This is a pending implementation boundary, not completion evidence.
- `git diff --check` exited 0. Both Unit 3 files are untracked, so the check does not include their content.

### TDD Cycle Evidence

| Task | Test file | Layer | Safety net | RED | GREEN | TRIANGULATE | REFACTOR |
| --- | --- | --- | --- | --- | --- | --- | --- |
| Unit 3 address-bound Onshape context | `tests/McpCad.Tests/Onshape/OnshapePartModelingWireMockTests.cs` | Local WireMock integration | N/A (new files) | Focused command exited 1 with expected `CS0246`: `OnshapePartModelingAdapter` did not exist. The first RED invocation exposed a test-only malformed interpolated raw string (`CS9007`); it was corrected before the behavioral RED. | **Blocked:** after the minimal adapter was added, the exact focused command compiled all projects and began test execution but timed out at 180 seconds with no test result. | Not reached: focused GREEN is unconfirmed. | Not reached. |

| Command | Exit | Result |
| --- | ---: | --- |
| `dotnet test tests/McpCad.Tests/McpCad.Tests.csproj --framework net8.0 --no-restore --filter 'FullyQualifiedName~OnshapePartModelingWireMockTests'` (RED) | 1 | Expected `CS0246` for missing `OnshapePartModelingAdapter`. |
| Same command after minimal implementation | timeout | Projects compiled; VSTest started test execution, then the harness timed out after 180 seconds without test totals. |
| `git diff --check` | 0 | Clean tracked diff; Unit 3 files are untracked. |

No Unit 3 checkbox was changed because GREEN, triangulation, and refactor evidence are incomplete. Exact remaining implementation task:

- [ ] **HOLD — Unit 3:** After explicit release and Units 1–2, implement the address-bound Onshape context and local WireMock conformance coverage in `src/McpCad.Onshape/Modeling/OnshapePartModelingAdapter.cs` and `tests/McpCad.Tests/Onshape/OnshapePartModelingWireMockTests.cs`. <!-- sdd-owner: implementation -->

Consumed structured status: `gentle-ai.sdd-status@2`, selected change `provider-neutral-part-modeling`, OpenSpec authoritative `applyState: ready`, `nextRecommended: apply`, workspace and only allowed edit root `/home/samael/Escritorio/Programacion/work/fisica/mcp-cad`; no blockers or action-context warnings. CodeGraph was present; its required explore call timed out, so targeted reads were the documented fallback. Workload / PR boundary: Unit 3 only, 343 authored source/test lines, below 400; delivery remains `ask-on-risk` and no PR or chain decision was made. Parent-owned lifecycle actions: none. `skill_resolution: paths-injected`.

## Unit 3 corrective verification rerun — blocked by VSTest data-collection failure

This is a narrow corrective rerun for released Unit 3 only. Before touching source, the persisted artifacts, strict-TDD guidance, current adapter, current WireMock test fixture, and prior progress were read. CodeGraph was available and used before the targeted fallback reads; its second exploration did not return the requested complete test/adapter path, so the two target files were read directly. No source investigation led to a deterministic adapter/test defect, and no production behavior was altered to influence the runner. Units 4–6 remain HOLD.

### Bounded incident diagnosis

The prior 180-second no-result focused VSTest incident was **not reproduced** by the single bounded corrective attempt. The exact focused filter was run with invocation-only `VSTEST_CONNECTION_TIMEOUT=120`, VSTest `--diag`, TRX, and `--blame-hang --blame-hang-timeout 2m`; no process was killed, no global environment/configuration/dependency state changed, and no cleanup/live CAD/network/credential action occurred.

| Attempt | Exit | Behavioral result | Diagnostic conclusion |
| --- | ---: | --- | --- |
| Corrective focused execution | 0 | 3 passed, 0 failed/skipped; 365 ms | Original timeout did not recur. Blame reported all tests completed and generated no sequence file. |
| Triangulation focused execution | 1 | TRX records 4 passed, 0 failed, 0 timed out; 314 ms | VSTest data collection failed before/after execution: `Newtonsoft.Json.JsonReaderException` (`Unexpected character ... E`) and `EndOfStreamException` in `DataCollectionRequestSender`. This is runner/diagnostic infrastructure evidence, not a test or adapter failure. |

Evidence is retained at `/tmp/mcp-cad-unit3-vstest-20260917T211102/` and `/tmp/mcp-cad-unit3-triangulate-20260917T211218/` (`summary.txt`, `command.log`, VSTest logs, and TRX). The historical timeout root cause remains unknown; this rerun does not claim a permanent repair. The nonzero triangulation invocation prevents a strict-TDD completion claim, so no full regression was run and no task was checked.

### TDD Cycle Evidence

| Task | Test file | Layer | Safety net | RED | GREEN | TRIANGULATE | REFACTOR |
| --- | --- | --- | --- | --- | --- | --- | --- |
| Unit 3 Onshape context | `tests/McpCad.Tests/Onshape/OnshapePartModelingWireMockTests.cs` | Local WireMock integration | N/A for the pre-existing new-file slice; `git diff --check` passed | Historical behavioral RED: missing `OnshapePartModelingAdapter` (`CS0246`) | Corrective focused execution exited 0 with 3/3 passed | Added circle-success/meter-radius coverage after the successful focused GREEN; all 4 tests passed in TRX, but the VSTest invocation exited 1 during diagnostic data collection | Not reached; no refactor was performed because the triangulation command was nonzero |

### Current boundary and persisted task state

- The Unit 3 source/test boundary is now 206 + 154 = **360 authored lines**, under the 400-line budget. This rerun added only `Context_CreatesBoundCircleSketchWithMeterRadius` (17 test lines); it exercised the existing circle path and required no production edit.
- `git diff --check` passed. The two Unit 3 files remain untracked, so that Git check does not validate their content.
- The Unit 3 checkbox remains visibly unchecked in `tasks.md`; no persisted task checkbox was changed because the strict focused triangulation invocation exited nonzero.
- Required full net8 regression was **not run**: strict TDD requires the focused triangulation command to exit successfully before that regression. Do not infer regression GREEN from the TRX test totals.

Remaining exact implementation task:

- [ ] **HOLD — Unit 3:** After explicit release and Units 1–2, implement the address-bound Onshape context and local WireMock conformance coverage in `src/McpCad.Onshape/Modeling/OnshapePartModelingAdapter.cs` and `tests/McpCad.Tests/Onshape/OnshapePartModelingWireMockTests.cs`. <!-- sdd-owner: implementation -->

Status consumed: `gentle-ai.sdd-status@2`, change `provider-neutral-part-modeling`, OpenSpec authoritative `applyState: ready`, `nextRecommended: apply`, repo-local workspace/allowed root `/home/samael/Escritorio/Programacion/work/fisica/mcp-cad`, and no blockers or action-context warnings. Delivery remains `ask-on-risk`; Unit 3 is the sole 360-line work-unit boundary. Parent-owned lifecycle work was not started. `skill_resolution: paths-injected`.

## Unit 3 clean completion after data-collection incident

The persisted prior triangulation evidence was inspected before this continuation. Its TRX recorded 4/4 passing tests, but the command exited 1 because optional VSTest diagnostics/data collection threw `JsonReaderException` and `EndOfStreamException`; that nonzero result was not treated as success. A single clean focused invocation then ran without `--diag`, TRX, blame, or other data-collection features. It used only the previously proven invocation-local `VSTEST_CONNECTION_TIMEOUT=120` safeguard and exited 0 with 4/4 passed. No retry loop, infrastructure change, source edit, live CAD, external network, credentials, registration, commit, staging, cleanup, or delivery action occurred.

A minimal refactor review found no safe simplification in the already bounded adapter/WireMock slice, so no behavior-preserving source refactor was made. The required clean net8 regression then exited 0 with 651 passed, 7 skipped, and 0 failed. The seven skips are the existing Inventor integration tests.

| Command | Exit | Result | Evidence |
| --- | ---: | --- | --- |
| `VSTEST_CONNECTION_TIMEOUT=120 dotnet test tests/McpCad.Tests/McpCad.Tests.csproj --framework net8.0 --no-restore --filter 'FullyQualifiedName~OnshapePartModelingWireMockTests'` | 0 | 4 passed, 0 failed/skipped | `/tmp/mcp-cad-unit3-clean-focused-20260918T023608Z/` |
| `VSTEST_CONNECTION_TIMEOUT=120 dotnet test tests/McpCad.Tests/McpCad.Tests.csproj --framework net8.0 --no-restore` | 0 | 651 passed, 7 skipped, 0 failed (658 total) | `/tmp/mcp-cad-unit3-full-net8-20260918T023631Z/` |
| `git diff --check` | 0 | clean tracked diff | current workspace |

### TDD Cycle Evidence

| Task | Test file | Layer | Safety net | RED | GREEN | TRIANGULATE | REFACTOR |
| --- | --- | --- | --- | --- | --- | --- | --- |
| Unit 3 Onshape context | `tests/McpCad.Tests/Onshape/OnshapePartModelingWireMockTests.cs` | Local WireMock integration | N/A (new Unit 3 files) | Historical behavioral RED: missing `OnshapePartModelingAdapter` (`CS0246`) | Prior clean diagnostic focused run: 3/3, exit 0 | Circle meter-radius case: the clean, non-data-collection focused command passed 4/4, exit 0 | Reviewed after GREEN; no safe refactor was needed, and the full regression passed |

- Persisted checkbox update: Unit 3 is `[x]` in `tasks.md`.
- Files in the completed work unit: `src/McpCad.Onshape/Modeling/OnshapePartModelingAdapter.cs` (206 lines) and `tests/McpCad.Tests/Onshape/OnshapePartModelingWireMockTests.cs` (154 lines), totaling **360** authored source/test lines, within the 400-line boundary.
- Design deviations: none. Optional evidence/revision remain explicitly unsupported; the context stays address-bound, one-shot, preflight-first, and does not retry or roll back.
- Remaining implementation tasks (persisted, exact):
  - [ ] **HOLD — Unit 4:** After explicit release, Unit 1 completion, and a configured Windows boundary, implement the Inventor native-frame seam and fake-boundary tests in `src/McpCad.Inventor/Modeling/InventorPartModelingNative.cs` and `tests/McpCad.Tests/Inventor/InventorPartModelingNativeTests.cs`. <!-- sdd-owner: implementation -->
  - [ ] **HOLD — Unit 5:** After explicit release and Units 1 and 4, implement Inventor context/transform conformance in `src/McpCad.Inventor/Modeling/InventorPartModelingAdapter.cs` and `tests/McpCad.Tests/Inventor/InventorPartModelingTests.cs`. <!-- sdd-owner: implementation -->
  - [ ] **HOLD — Unit 6:** After explicit release and Units 3 and 5, implement configured registration and compatibility coverage in `src/McpCad.Server/ProviderBootstrap.cs`, `src/McpCad.Server/ProviderBootstrap.Inventor.cs`, and `tests/McpCad.Tests/Modeling/PartModelingRegistrationTests.cs`, without changing public tools or schemas. <!-- sdd-owner: implementation -->

Structured status consumed: `gentle-ai.sdd-status@2`; `applyState: ready`, `nextRecommended: apply`, root and only allowed edit root `/home/samael/Escritorio/Programacion/work/fisica/mcp-cad`, no blockers or action-context warnings. Workload / PR boundary remains Unit 3 only at 360 lines under the 400-line budget; delivery is `ask-on-risk`. Units 4–6 remain deferred lifecycle scope. `skill_resolution: paths-injected`.

## Unit 3 correction R3-write-cancellation-state — blocked before TDD

The native status was authoritative and ready for apply, with the repository root as the only allowed edit root. Required proposal/spec/design/tasks/progress/config artifacts and strict-TDD guidance were read. The task forecast, however, still states `Chained PRs recommended: Yes`; the supplied delivery strategy is `ask-on-risk`, not a resolved `auto-chain`, chosen stacked/chained mode, or explicit `size:exception`. Unit 3 already records 360 authored source/test lines against the 400-line review budget, while the requested correction permits up to 60 further diff lines. Per the workload gate, no test or production file was edited, no test command was run, and no task checkbox changed.

Required delivery decision: authorize `auto-chain`/a specific chained slice for this correction, or explicitly accept `size:exception` for a single PR if the resulting Unit 3 boundary exceeds 400 lines. Parent-owned review lineage `review-558c116dbf24f7e9` was not accessed or manipulated. Existing Unit 3 completion and all unchecked Unit 4–6 lines remain unchanged. `skill_resolution: paths-injected`.

## Unit 3 cancellation correction `R3-write-cancellation-state` — complete

The user resolved the delivery gate as an `auto-chain` correction slice using `feature-branch-chain`. Planned order is **existing Unit 3 slice → this separate cancellation correction slice**. No branch, commit, staging, PR, review action, live CAD/network, credential, Inventor, registration, cleanup, or publication action was performed.

- After the pre-write `ThrowIfCancellationRequested` check, both write paths now convert a cancellation/error from the dispatched `SendJsonAsync` request into `OperationStatus.Uncertain` with `MutationDisposition.Unknown`. The pre-write check remains outside the catch, so cancellation before dispatch still escapes with no mutation/result update.
- Added strict-TDD theory coverage for both write positions. Its controlled `HttpMessageHandler` returns cancellation only after it receives sketch write 1 or extrusion write 2; each case verifies uncertain/unknown state and that a repeated operation returns the latched result rather than issuing another write.
- Persisted task truth: `[x] COMPLETE — Unit 3 cancellation correction slice` was added to `tasks.md` with the implementation ownership marker.
- Files changed: `src/McpCad.Onshape/Modeling/OnshapePartModelingAdapter.cs`, `tests/McpCad.Tests/Onshape/OnshapePartModelingWireMockTests.cs`, `openspec/changes/provider-neutral-part-modeling/tasks.md`, and this cumulative progress file.
- Workload boundary: the original Unit 3 source/test count was 360 (206 adapter + 154 tests); this correction is +60 net test lines, producing 420 across the two chained slices. The correction itself remains within the authorized 60-line boundary; no size exception is used.

### TDD Cycle Evidence

| Task | Test file | Layer | Safety net | RED | GREEN | TRIANGULATE | REFACTOR |
| --- | --- | --- | --- | --- | --- | --- | --- |
| `R3-write-cancellation-state` | `tests/McpCad.Tests/Onshape/OnshapePartModelingWireMockTests.cs` | Focused adapter/HTTP integration | Existing focused Unit 3 suite passed 4/4 before edits | New sketch-write and extrusion-write cases failed: `TaskCanceledException` escaped after dispatch, leaving the state unasserted | Minimal catch-boundary change passed 6/6 | Theory cases exercise cancellation after write 1 (sketch) and write 2 (extrusion), including repeated-operation latching | Simplified the test handler after GREEN; focused suite remained 6/6 and no further production refactor was needed |

| Command | Exit | Result |
| --- | ---: | --- |
| `VSTEST_CONNECTION_TIMEOUT=120 dotnet test tests/McpCad.Tests/McpCad.Tests.csproj --framework net8.0 --no-restore --filter 'FullyQualifiedName~OnshapePartModelingWireMockTests'` | 0 | 6 passed, 0 failed/skipped |
| `VSTEST_CONNECTION_TIMEOUT=120 dotnet test tests/McpCad.Tests/McpCad.Tests.csproj --framework net8.0 --no-restore` | 0 | 653 passed, 7 skipped, 0 failed (660 total) |
| `git diff --check` | 0 | clean tracked diff |

No design deviation: cancellation after a dispatched REST write is conservatively ambiguous and never retried; cancellation before dispatch remains no-mutation behavior. Remaining persisted implementation tasks are exactly:

- [ ] **HOLD — Unit 4:** After explicit release, Unit 1 completion, and a configured Windows boundary, implement the Inventor native-frame seam and fake-boundary tests in `src/McpCad.Inventor/Modeling/InventorPartModelingNative.cs` and `tests/McpCad.Tests/Inventor/InventorPartModelingNativeTests.cs`. <!-- sdd-owner: implementation -->
- [ ] **HOLD — Unit 5:** After explicit release and Units 1 and 4, implement Inventor context/transform conformance in `src/McpCad.Inventor/Modeling/InventorPartModelingAdapter.cs` and `tests/McpCad.Tests/Inventor/InventorPartModelingTests.cs`. <!-- sdd-owner: implementation -->
- [ ] **HOLD — Unit 6:** After explicit release and Units 3 and 5, implement configured registration and compatibility coverage in `src/McpCad.Server/ProviderBootstrap.cs`, `src/McpCad.Server/ProviderBootstrap.Inventor.cs`, and `tests/McpCad.Tests/Modeling/PartModelingRegistrationTests.cs`, without changing public tools or schemas. <!-- sdd-owner: implementation -->

Structured status consumed: `gentle-ai.sdd-status@2`, `applyState: ready`, `nextRecommended: apply`, OpenSpec repo-local workspace and only allowed edit root `/home/samael/Escritorio/Programacion/work/fisica/mcp-cad`; no blockers or action-context warnings. Parent-owned lifecycle work, including native review lineage `review-558c116dbf24f7e9`, was not started. `skill_resolution: paths-injected`.

## Unit 6A Onshape registration and compatibility — complete

The authoritative task/design dependency is reconciled: former Unit 6 is now a released and completed **Unit 6A (Onshape-only)** and a separately unchecked **Unit 6B (Inventor registration)**. Unit 6A depends only on completed Unit 3; Units 4–5 and all `ProviderBootstrap.Inventor.cs` changes remain HOLD. This is not a claim of two-provider registration, full provider-neutral completion, Windows support, or a native status refresh.

- Added the internal-use CLR `IPartModelingContextFactory` contract and an Onshape factory that binds a fresh address-bound context through the existing adapter. The server's existing `RegisterOnshape` path registers that factory alongside—not instead of—the existing provider and public Onshape tool registrations.
- Added Linux-only DI compatibility coverage. It creates two contexts without calling CAD/network APIs and proves both unsupported optional capabilities; a second case proves the existing `OnshapeAtomicTools` family remains resolvable alongside the factory.
- Persisted task update: `[x] COMPLETE — Unit 6A (Onshape-only)` is visible in `tasks.md`. Unit 6B stayed byte-for-byte HOLD after the new split.
- Files changed: `src/McpCad.Core/Modeling/PartModelingContracts.cs`, `src/McpCad.Onshape/Modeling/OnshapePartModelingAdapter.cs`, `src/McpCad.Server/ProviderBootstrap.cs`, `tests/McpCad.Tests/Modeling/PartModelingRegistrationTests.cs`, `openspec/changes/provider-neutral-part-modeling/design.md`, `openspec/changes/provider-neutral-part-modeling/tasks.md`, and this cumulative progress file.

### TDD Cycle Evidence

| Task | Test file | Layer | Safety net | RED | GREEN | TRIANGULATE | REFACTOR |
| --- | --- | --- | --- | --- | --- | --- | --- |
| Unit 6A Onshape registration | `tests/McpCad.Tests/Modeling/PartModelingRegistrationTests.cs` | Unit / DI | Existing `ProviderBootstrapTests`: 5/5 passed | New factory-resolution test failed at compile time: `CS0246` for missing `IPartModelingContextFactory` | Minimal Core factory contract, Onshape factory, and bootstrap registration: 1/1 passed after correcting the missing server Modeling namespace import | Added existing-Onshape-tools coexistence case: 2/2 passed | Reviewed the small factory/registration seam; no safe simplification was needed and no refactor was made |

| Command | Exit | Result |
| --- | ---: | --- |
| `VSTEST_CONNECTION_TIMEOUT=120 dotnet test tests/McpCad.Tests/McpCad.Tests.csproj --framework net8.0 --no-restore --filter 'FullyQualifiedName~McpCad.Tests.Server.ProviderBootstrapTests'` | 0 | 5 passed, 0 failed/skipped |
| `VSTEST_CONNECTION_TIMEOUT=120 dotnet test tests/McpCad.Tests/McpCad.Tests.csproj --framework net8.0 --no-restore --filter 'FullyQualifiedName~McpCad.Tests.Modeling.PartModelingRegistrationTests'` (RED) | 1 | Expected `CS0246` for missing `IPartModelingContextFactory` |
| Same focused command after GREEN | 0 | 1 passed, 0 failed/skipped |
| Same focused command after triangulation | 0 | 2 passed, 0 failed/skipped |
| `VSTEST_CONNECTION_TIMEOUT=120 dotnet test tests/McpCad.Tests/McpCad.Tests.csproj --framework net8.0 --no-restore` | 0 | 655 passed, 7 skipped, 0 failed (662 total) |
| `git diff --check` plus `git diff --check --no-index /dev/null tests/McpCad.Tests/Modeling/PartModelingRegistrationTests.cs` | 0 | no whitespace errors; the no-index command's expected diff exit was handled |

Workload / PR boundary: **Unit 6A only**, 75 authored additions (Core 7, Onshape factory 12, bootstrap 3, registration tests 53), below the 400-line review budget. Delivery is the explicitly selected `auto-chain` / `feature-branch-chain`; this is the next review slice after Unit 3 and its cancellation correction. No branch, commit, PR, staging, push, cleanup, publication, review, receipt, or other native lifecycle action was performed.

No design deviation: no public MCP tool/schema, provider argument, universal target, external request, credential use, or Inventor reference was added. CodeGraph was present but its required exploration timed out, so targeted reads were used as the documented fallback. The consumed structured status was `gentle-ai.sdd-status@2`, `applyState: ready`, `nextRecommended: apply`, OpenSpec/repo-local with only allowed root `/home/samael/Escritorio/Programacion/work/fisica/mcp-cad`, and no blockers/action-context warnings. The native projection predates this task split and is not rewritten here.

Remaining persisted implementation tasks, exactly:

- [ ] **HOLD — Unit 4:** After explicit release, Unit 1 completion, and a configured Windows boundary, implement the Inventor native-frame seam and fake-boundary tests in `src/McpCad.Inventor/Modeling/InventorPartModelingNative.cs` and `tests/McpCad.Tests/Inventor/InventorPartModelingNativeTests.cs`. <!-- sdd-owner: implementation -->
- [ ] **HOLD — Unit 5:** After explicit release and Units 1 and 4, implement Inventor context/transform conformance in `src/McpCad.Inventor/Modeling/InventorPartModelingAdapter.cs` and `tests/McpCad.Tests/Inventor/InventorPartModelingTests.cs`. <!-- sdd-owner: implementation -->
- [ ] **HOLD — Unit 6B (Inventor registration):** After explicit release and Units 4–5, register the Inventor adapter factory in `src/McpCad.Server/ProviderBootstrap.Inventor.cs` and extend compatible registration coverage, without changing public MCP tools or schemas. <!-- sdd-owner: implementation -->

Deferred lifecycle actions: parent-owned native status refresh and all review/delivery actions. `skill_resolution: paths-injected`.

## Unit 3 stopped-context correction `R3-stopped-context-can-still-write` — complete

Implemented the authorized `auto-chain` / `feature-branch-chain` correction slice. `CreateSketch` now returns its unchanged `NotAttempted` result when the extrusion slot is already `Failed` or `Uncertain`, so an out-of-order extrusion failure latches the context before any sketch preflight, transport, or write. Valid sketch-then-extrude ordering and dispatched-write cancellation handling are unchanged. Unit 6A registration behavior was not modified.

- Persisted task update: `[x] COMPLETE — Unit 3 stopped-context correction slice` in `tasks.md`.
- Source/test files: `src/McpCad.Onshape/Modeling/OnshapePartModelingAdapter.cs`; `tests/McpCad.Tests/Onshape/OnshapePartModelingWireMockTests.cs`.
- Workload / PR boundary: this is the separate correction slice after Unit 6A under `feature-branch-chain`; its source/test delta is 19 lines (2 changed production lines, 17 added test lines), within the 30-line correction limit and 400-line budget.
- No design deviation. No live CAD/network, credentials, Inventor, registration, branch, commit, staging, PR, review, cleanup, or publication action was performed. Native lineage `review-0419f6b666c4977c` was not accessed.

### TDD Cycle Evidence

| Task | Test file | Layer | Safety net | RED | GREEN | TRIANGULATE | REFACTOR |
| --- | --- | --- | --- | --- | --- | --- | --- |
| `R3-stopped-context-can-still-write` | `tests/McpCad.Tests/Onshape/OnshapePartModelingWireMockTests.cs` | Local WireMock integration | 6/6 focused tests passed | New out-of-order extrusion-then-sketch test failed: sketch became `Uncertain` after transport | Minimal stopped-state guard passed 7/7 | Null and foreign sketch-reference variants both prove failed extrusion blocks all later sketch transport; 8/8 passed | Reviewed; the one guard is minimal and needed no refactor |

| Command | Exit | Result |
| --- | ---: | --- |
| `VSTEST_CONNECTION_TIMEOUT=120 dotnet test tests/McpCad.Tests/McpCad.Tests.csproj --framework net8.0 --no-restore --filter 'FullyQualifiedName~OnshapePartModelingWireMockTests'` | 0 | safety net 6/6; GREEN 7/7; triangulation 8/8 |
| `VSTEST_CONNECTION_TIMEOUT=120 dotnet test tests/McpCad.Tests/McpCad.Tests.csproj --framework net8.0 --no-restore` | 0 | 657 passed, 7 skipped, 0 failed (664 total) |
| `git diff --check` | 0 | clean |

Remaining persisted implementation tasks:

- [ ] **HOLD — Unit 4:** After explicit release, Unit 1 completion, and a configured Windows boundary, implement the Inventor native-frame seam and fake-boundary tests in `src/McpCad.Inventor/Modeling/InventorPartModelingNative.cs` and `tests/McpCad.Tests/Inventor/InventorPartModelingNativeTests.cs`. <!-- sdd-owner: implementation -->
- [ ] **HOLD — Unit 5:** After explicit release and Units 1 and 4, implement Inventor context/transform conformance in `src/McpCad.Inventor/Modeling/InventorPartModelingAdapter.cs` and `tests/McpCad.Tests/Inventor/InventorPartModelingTests.cs`. <!-- sdd-owner: implementation -->
- [ ] **HOLD — Unit 6B (Inventor registration):** After explicit release and Units 4–5, register the Inventor adapter factory in `src/McpCad.Server/ProviderBootstrap.Inventor.cs` and extend compatible registration coverage, without changing public MCP tools or schemas. <!-- sdd-owner: implementation -->

Structured status consumed: `gentle-ai.sdd-status@2`, change `provider-neutral-part-modeling`, OpenSpec, authoritative `applyState: ready`, `nextRecommended: apply`, repository-local workspace and only allowed edit root `/home/samael/Escritorio/Programacion/work/fisica/mcp-cad`; no blockers or action-context warnings. Deferred lifecycle actions remain parent-owned. `skill_resolution: paths-injected`.

## Scope reconciliation — completed Onshape/Linux delivery

The user explicitly selected this scope split. The current change is reconciled as the completed **Onshape/Linux** delivery: shared typed geometry, Onshape frame reader/mapper/preflight/context, and internal Onshape factory registration. It preserves existing public MCP tools and schemas. The focused Unit 3 suite passed **8** tests; the net8.0 regression passed **657**, skipped **7**, and failed **0**. Those results are retained prior evidence only; this documentation-only reconciliation did not run tests, builds, CAD, network/credential operations, source changes, review, verification, archive, staging, commit, or delivery commands.

The approved and acknowledged native review lineage `review-0419f6b666c4977c` is retained as prior review evidence. This phase did not create, approve, acknowledge, inspect, or otherwise operate on a receipt.

### Persisted task reconciliation

The following implementation-owned current-change rows are visibly `[x]` in `tasks.md` because their **transfer** is complete, not because the deferred work is implemented:

- `RECONCILED — Unit 4 deferred` → `inventor-windows-part-modeling`
- `RECONCILED — Unit 5 deferred` → `inventor-windows-part-modeling`
- `RECONCILED — Unit 6B deferred` → `inventor-windows-part-modeling`

All current-change task rows are now complete. Parent-owned lifecycle actions remain: refresh native status and route from its `nextRecommended`; do not infer verify or archive from this task reconciliation.

### Deferred-change handoff

**Exact future change name:** `inventor-windows-part-modeling`.

A second active OpenSpec change was intentionally not created in this apply pass. The repository convention shows change artifacts but no supplied native planning launch or proposal-approval authority for a new change; creating a populated follow-up proposal/design/tasks set here could fabricate that authorization and make active-change selection ambiguous. The next planner must launch the named change natively and obtain proposal approval before materializing its planning artifacts.

| Handoff topic | Required future scope |
| --- | --- |
| Unit 4 | Inventor native-frame seam and fake-boundary tests: unique canonical frame selection, pinned document/sketch/profile identity, pre-mutation failure behavior, and mutation uncertainty. |
| Unit 5 | Inventor context and transform conformance: rectangle/circle mapping, explicit direction/new-body semantics, exact profile/extrusion binding, reference ownership, stopped state, and Windows fake/native-boundary coverage. |
| Unit 6B | Register the Inventor context factory in `src/McpCad.Server/ProviderBootstrap.Inventor.cs` with compatible registration coverage; preserve public MCP tools and schemas. |
| Dependencies | Unit 4 before Unit 5; Units 4–5 before Unit 6B. Revalidate the existing shared contracts and Onshape registration seam rather than altering reviewed behavior. |
| Platform boundary | Windows with configured Inventor interop/test environment. Linux evidence must not be treated as execution or validation evidence for this work. No live CAD, credentials, or external network is implied by planning. |
| Next planning phase | Native launch of proposal/planning for `inventor-windows-part-modeling`, followed by its independently authorized specification, design, task, apply, and Windows verification phases. |

### Reconciliation evidence and boundary

Files changed only for this artifact reconciliation: `proposal.md`, `specs/provider-neutral-part-modeling/spec.md`, `design.md`, `tasks.md`, and this cumulative `apply-progress.md`. No source or test file was edited. The reviewed candidate's behavior is unchanged.

Workload / PR boundary: documentation-only reconciliation slice under the user-selected `auto-chain` / `feature-branch-chain` strategy and 400-line review budget. No branch, commit, or PR action is requested or performed.

### TDD Cycle Evidence

| Task | Test file | Layer | Safety net | RED | GREEN | TRIANGULATE | REFACTOR |
| --- | --- | --- | --- | --- | --- | --- | --- |
| Scope reconciliation | N/A — planning artifacts only | N/A | N/A | N/A | N/A | N/A | N/A — no production or test behavior changed; user prohibited test execution |

Structured status consumed: `gentle-ai.sdd-status@2`, `applyState: ready`, `nextRecommended: apply`, action context `repo-local`, workspace and only allowed edit root `/home/samael/Escritorio/Programacion/work/fisica/mcp-cad`, with no warnings. `skill_resolution: paths-injected`.
