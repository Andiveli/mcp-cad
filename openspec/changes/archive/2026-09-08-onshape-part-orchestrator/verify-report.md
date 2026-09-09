# Verification Report: Onshape Part Orchestrator

**Change:** `onshape-part-orchestrator`  
**Status:** PASS WITH WARNINGS  
**Verification mode:** AUTO, strict TDD  
**Workspace:** `/home/samael/Escritorio/Programacion/work/fisica/mcp-cad`

## Executive summary

The implementation satisfies the version-1 `onshape_orchestrate_part(address_json: string, plan_json: string)` specification and all 28 implementation-owned tasks are checked. The current full test run passed 565 tests with 7 skipped (572 total), the focused orchestrator/registration/god-macro regression selection passed 104/104, and the Onshape tools, server, and test projects build with zero warnings or errors.

The required solution build was run and failed only in the unchanged Windows/COM-only `McpCad.Inventor` project because this Linux host lacks Autodesk Inventor interop types. The solution build successfully produced `McpCad.Core`, `McpCad.Installer`, `McpCad.Onshape`, `McpCad.Tools`, `McpCad.Tools.Onshape`, `McpCad.Server`, and `McpCad.Tests` before reporting 11 `CS0246` errors under `src/McpCad.Inventor`. This is an external platform limitation, not a candidate-caused Onshape failure.

One delivery-boundary warning remains: the working tree contains modified `.atl/` files and untracked `.pi/` and `.pi-subagents/` files. They are outside the declared change surfaces and no files are staged, but an actual delivery diff excluding them cannot yet be proven. The corresponding parent lifecycle checkbox remains unchecked. Do not include these paths in a commit or PR.

## Structured status and action context

- Explicit active change: `onshape-part-orchestrator`; selection is unambiguous.
- Authoritative parent status consumed: 28/28 implementation tasks complete, apply `all_done`, verify ready, sync/archive blocked pending verification.
- `actionContext.mode`: `repo-local`.
- Workspace and allowed edit root: `/home/samael/Escritorio/Programacion/work/fisica/mcp-cad`.
- Action-context warnings: none.
- Implementation ownership is proven within the authoritative workspace and declared unit surfaces.
- The verify executor edited only this report and truthfully proven parent lifecycle checkboxes in `tasks.md`.
- CodeGraph was present but its structural exploration request timed out; verification fell back to targeted reads of every declared implementation and changed/created test file.

## Spec coverage

| Requirement / acceptance area | Evidence | Result |
|---|---|---|
| Exact two-string MCP contract and strict versioned JSON | Adapter reflection tests; strict parser allowlists, duplicate/type/version tests | PASS |
| 1–16 ordered stages and at most 3 checkpoints | Validator bounds tests and sequential executor tests | PASS |
| Unique IDs and typed backward references | Validator duplicate, forward, wrong-kind, and stale-checkpoint cases | PASS |
| Rectangle, circle, polygon, and mixed profiles on canonical planes | Parser/validator tests, builder payload checks, 1/8/9-arc boundaries | PASS |
| `new`/`add`/`remove`, finite nonzero depth, native sketch-ID linkage | Parser/validator tests; executor payload assertions; WireMock POST inspection | PASS |
| Complete preflight before provider activity | Late-invalid-plan adapter test with strict provider and zero invocations | PASS |
| Declaration-order execution and first modeling-failure stop | Recording-provider and result tests | PASS |
| Checkpoint capture order, image projection, and nonfatal failure | Executor/result tests and local WireMock POST → GET → POST evidence | PASS |
| Exact ordered result envelope and partial-status semantics | Result projection tests compare exact keys, statuses, nulls, errors, and image count | PASS |
| Additive compatibility with `skill_onshape_god_part` | Registration test plus focused god-macro regression selection | PASS |
| Non-idempotency, no rollback/retry, single-body-result assumption, evidence-only images | Code boundaries and both provider documentation files | PASS |
| No live CAD calls, credentials, or hard-coded live endpoint in candidate code/tests | Tests use strict mocks or localhost WireMock with explicit fixture strings | PASS |

All ten proposal success criteria are covered by implementation inspection and automated evidence. No provider interface expansion, raw internal MCP calls, rollback, global mutable state, or unsupported topology/body-selection surface was found.

## Protected surfaces and compatibility

`git status --short -- <protected paths>` returned no changes for:

- `src/McpCad.Tools/Onshape/OnshapeGodPart.cs`
- `src/McpCad.Tools/Onshape/OnshapeGodPartCapture.cs`
- `src/McpCad.Tools/Onshape/OnshapeSkillTools.cs`
- `src/McpCad.Core/ICadWebProvider.cs`
- `src/McpCad.Core/Models/CadElementAddress.cs`
- existing Onshape builders/provider/manager files
- `src/McpCad.Server/Program.cs`

Registration inspection confirms exactly one `onshape_orchestrate_part` method and retention of `skill_onshape_god_part`. Focused builds confirm the established discovery path compiles without server-registration changes.

## Task completion

- Implementation-owned tasks: **28/28 checked**.
- Exact unchecked implementation task lines: **none**.
- Parent lifecycle tasks proven and checked during verification: unit review-size boundaries, bounded approved reviews, and final test/build evidence with lifecycle actions deferred to the parent.
- Remaining unchecked parent-only line:

```text
- [ ] Confirm the delivery diff excludes `.atl/`, `.pi/`, `.pi-subagents/`, source/test files outside the declared unit surfaces, credentials, and generated artifacts. <!-- sdd-owner: parent -->
```

This is not an implementation-completeness defect, but archive/delivery is not ready until the actual commit/PR boundary excludes those paths.

## Review workload / PR boundary

The selected strategy was a linear seven-unit chain with no `size:exception`. Parent context confirms every bounded unit review was approved; Unit 7 review `review-f830cb9031da1d41` was approved and acknowledged.

Recomputed file footprints remain below 400 changed lines per unit:

| Unit | Verified footprint |
|---|---:|
| 1 | 232 |
| 2 | 340 |
| 3 | 242 |
| 4 | 334 |
| 5 | 279 |
| 6 | 137 |
| 7 | 135 |

The implementation matches the linear Unit 1 → Unit 7 boundary. No source/test scope creep outside declared unit surfaces was found. Working-tree metadata outside delivery scope is reported separately below.

## Test and validation commands

| Command | Result |
|---|---|
| `VSTEST_CONNECTION_TIMEOUT=180 dotnet test tests/McpCad.Tests/McpCad.Tests.csproj --framework net8.0 --no-restore` | PASS — 565 passed, 7 skipped, 572 total |
| `dotnet test tests/McpCad.Tests/McpCad.Tests.csproj --framework net8.0 --no-restore --filter 'FullyQualifiedName~OnshapePartPlan\|FullyQualifiedName~OnshapePartOrchestrator\|FullyQualifiedName~ToolRegistrationCollisionTests\|FullyQualifiedName~OnshapeGodPart'` | PASS — 104/104 |
| `dotnet build src/McpCad.Tools/Onshape/McpCad.Tools.Onshape.csproj --no-restore` | PASS — 0 warnings, 0 errors |
| `dotnet build src/McpCad.Server/McpCad.Server.csproj --no-restore` | PASS — 0 warnings, 0 errors |
| `dotnet build tests/McpCad.Tests/McpCad.Tests.csproj --no-restore` | PASS — 0 warnings, 0 errors |
| `dotnet build src/mcp-cad.sln` | EXTERNAL PLATFORM FAILURE — 11 `CS0246` errors only in unchanged `src/McpCad.Inventor` because Linux lacks Autodesk Inventor interop; Onshape/tools/server/tests built |
| `VSTEST_CONNECTION_TIMEOUT=180 dotnet test tests/McpCad.Tests/McpCad.Tests.csproj --framework net8.0 --no-restore --collect:'XPlat Code Coverage' --results-directory /tmp/mcp-cad-verify-coverage` | PASS — 565 passed, 7 skipped, 572 total; report kept outside repository |

The required test and solution-build commands were both run. The failed solution build is not reported as passing.

## Strict TDD compliance

The cumulative `apply-progress.md` contains TDD Cycle Evidence for all seven units, including recorded RED failures followed by GREEN, TRIANGULATE, and REFACTOR reruns. Every reported test file exists. Historical RED was not destructively replayed; current GREEN was independently confirmed.

| Check | Result | Details |
|---|---|---|
| TDD evidence reported | PASS | Evidence is present for Units 1–7 |
| All units have tests | PASS | 7/7 implementation units map to real test files |
| RED evidence | PASS | Recorded executable failures precede implementation; all referenced files exist |
| GREEN confirmed now | PASS | Full suite 565/572; focused change/compatibility selection 104/104 |
| Triangulation adequate | PASS | Boundary, alternate response, failure, ordering, and exact-schema cases are present |
| Safety net | PASS | New files are reported as new; the modified registration test records and retains the existing baseline |

**TDD compliance:** 6/6 checks passed.

### Test layer distribution

The 66 orchestrator-specific discovered test cases comprise:

| Layer | Tests | Files | Tools |
|---|---:|---:|---|
| Unit | 63 | 6 | xUnit, FluentAssertions, Moq/reflection |
| Integration | 3 | 1 | xUnit + local WireMock.Net/provider stack |
| E2E/live CAD | 0 | 0 | Intentionally out of scope |
| **Total** | **66** | **7** | |

The WireMock layer covers transport payloads, nested/top-level feature-ID normalization, request order, checkpoint PNG conversion, and capture-failure continuation without a live Onshape endpoint.

### Changed production-file coverage

Coverage came from the successful full-suite collector run. Cobertura emitted no usable aggregate branch percentages for these files.

| File | Line coverage | Branch coverage | Uncovered executable lines | Rating |
|---|---:|---:|---|---|
| `OnshapePartOrchestratorTool.cs` | 100.0% | n/a | — | Excellent |
| `OnshapeFeatureWriteResult.cs` | 100.0% | n/a | — | Excellent |
| `OnshapeOrchestrationResultBuilder.cs` | 96.4% | n/a | 72–73 | Excellent |
| `OnshapePartPlanExecutor.cs` | 88.7% | n/a | 52–58, 80, 96–98, 172–174 | Acceptable |
| `OnshapePartPlanModels.cs` | 100.0% | n/a | — | Excellent |
| `OnshapePartPlanParser.cs` | 99.4% | n/a | 143 | Excellent |
| `OnshapePartPlanValidator.cs` | 91.5% | n/a | 41, 54–55, 73, 80, 106, 114 | Acceptable |

**Aggregate changed production-file line coverage:** 498/522 executable lines, **95.4%**. Configured threshold is 0%; no coverage blocker exists.

### Assertion quality

All eight changed/created test files, including `ToolRegistrationCollisionTests.cs`, were inspected. No tautologies, ghost loops, tests without production calls, smoke-only tests, CSS assertions, or mock-heavy files were found. The parser's hard-coded non-empty case loop cannot silently skip.

| File | Line | Assertion | Issue | Severity |
|---|---:|---|---|---|
| `tests/McpCad.Tests/Tools/OnshapePartPlanParserTests.cs` | 76 | `.Should().NotBeNull()` | Acceptance-only/type-presence assertion does not inspect the normalized repeated closing point; the validator integration test supplies downstream behavioral coverage | WARNING |

**Assertion quality:** 0 CRITICAL, 1 WARNING.

### Quality metrics

- **Linter:** not configured.
- **Type/build checks:** focused Onshape tool, server, and tests builds pass with 0 warnings/errors.
- **Full solution:** fails only at unchanged Windows-only Inventor interop on Linux; no changed-file type errors were reported.

## Credential, endpoint, and mutation audit

- Candidate production code contains no endpoint or credential literal.
- The WireMock test uses `_server.Url`, asserts `http://localhost`, and uses explicit fixture strings `local-access-key` / `local-secret`; these are not live credentials.
- Existing provider documentation describes the configured Onshape base URL and secret variable names, not secret values.
- No live Onshape call or CAD mutation occurred during verification.

## Findings and exact blockers

### Warnings

1. **Delivery-boundary contamination:** `git status` shows modified `.atl/.skill-registry.cache.json`, modified `.atl/skill-registry.md`, untracked `.pi/`, and untracked `.pi-subagents/`. They are not part of the declared implementation and nothing is staged, but they must be explicitly excluded from the eventual delivery.
2. **Assertion quality:** the repeated-polygon-closing parser acceptance test uses only `NotBeNull`; downstream validator coverage limits the risk.
3. **External solution-build limitation:** Linux cannot resolve Autodesk Inventor COM interop. Focused candidate projects build cleanly.

### Critical implementation blockers

None.

### Archive/delivery blocker

The actual delivery diff must be formed and checked so `.atl/`, `.pi/`, and `.pi-subagents/` are excluded. Until then, the remaining parent lifecycle checkbox must stay unchecked and archive readiness should not be claimed.

## Final disposition

**PASS WITH WARNINGS.** The Onshape orchestrator is functionally and structurally verified, strict-TDD evidence is adequate, all implementation tasks are complete, and candidate tests/builds are green. Resolve the delivery-scope exclusion check, then proceed to archive/sync according to the parent lifecycle. The Linux Inventor interop failure should be carried as an external platform limitation rather than attributed to this change.
