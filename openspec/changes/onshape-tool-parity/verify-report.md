# Verification Report — onshape-tool-parity

**Mode:** Strict TDD active · full-slice verification (1–4)  
**Branch:** `feature/onshape-tool-parity/slice-4` at `b374a8e`  
**Verdict:** **FAIL**

## Completeness

| Slice | Required task headings | Marked complete | Result |
| --- | ---: | ---: | --- |
| 1 | 6 | 0 | ❌ |
| 2 | 9 | 0 | ❌ |
| 3 | 6 | 6 | ✅ |
| 4 | 6 | 6 | ✅ |
| **Total (1.1–4.6)** | **27** | **12** | **❌** |

`tasks.md` leaves every task in Slices 1 and 2 unchecked. Optional tasks 3.7 and 4.7 are excluded from this count.

## Build Evidence

| Command | Exit | Result | Output SHA-256 |
| --- | ---: | --- | --- |
| `dotnet build src/mcp-cad.sln --no-restore` | 1 | ❌ 11 errors, 1 warning | `5ca0b2ad9f130994b3b2d7e686b026a7a3101d881432e45a3f4910c5b3ec2cc0` |

The solution build fails in `McpCad.Inventor` because the Linux verifier does not have the `Inventor` assembly. The Onshape/server/test projects compiled; `MockOnshapeProvider._errorResult` also emits CS0649.

## Test Evidence

| Command | Exit | Passed | Failed | Skipped | Output SHA-256 |
| --- | ---: | ---: | ---: | ---: | --- |
| `dotnet test tests/McpCad.Tests/` | 1 | 95 | 1 | 7 | `b012a76c9058c319df6117f1f0ec7dd49eb35efa2aa1f5df0a5dd1ce509d29cd` |

Failing test: `ExportPollingTests.PollTranslationAsync_Transient500ThenDone_Succeeds`; `OnshapeClient.SendJsonAsync` attempts to parse the transient 500 empty body as JSON. It was recorded as pre-existing, but the current full-suite gate is not green.

## Coverage Evidence

Coverage analysis skipped — no cached testing-capabilities artifact or configured coverage collector was available.

## Spec Compliance Matrix

| Requirement / scenario | Evidence | Result |
| --- | --- | --- |
| BTMSketch rectangle replace | `OnshapeSketchBuilderTests.BuildRectangle_ReturnsFourLineEntities`; WireMock write test | ⚠️ Builder/write path passes, but no golden fixture exists and closed geometry is not asserted. |
| BTMSketch line replace | `BuildLine_ReturnsSingleLineEntity` | ⚠️ Unit shape only; no POST conformance test. |
| FeatureAdd extrude envelope | Template and WireMock regression tests | ✅ Wrapped `feature` body and skew flag are asserted. |
| Flat payload regression | `FeatureAdd_FlatPayload_RegressionGuard_UsesFeatureEnvelope` | ✅ |
| Feature discovery after extrude | No response-shape test | ❌ `FeatureList` forwards raw JSON and does not verify/normalize `featureId`, `featureType`, and `parameters`. |
| Empty Part Studio | Mock-only empty array test | ⚠️ No real manager/WireMock response test. |
| Sketch → extrude microversion sequence | Separate write tests only | ⚠️ No sequence verifies that `mv2` is used for the following write. |
| Matrix P0 rows | `docs/onshape-parity-matrix.md` rows 13–15 | ✅ |
| Matrix classification / refresh | Matrix includes P1/P2 rows and statuses | ⚠️ Status is claimed `implemented`; no live execution proves the required live API behavior. |
| `onshape_` naming and collision safety | `ToolRegistrationCollisionTests` | ✅ Onshape names are enumerated and prefixed. |
| Legacy deprecation | `OnshapeAtomicTools` lines 183–203 | ✅ Descriptions and delegation are present. |
| `address_json` validation | Invalid JSON tool test | ⚠️ Malformed JSON is covered; missing required fields are not tested. |
| cm → m request conversion | Tool calls templates through `OnshapeUnitConversion` | ⚠️ No precision test for the specified `0.1 cm → 0.001 m`; no tool-level body assertion for `5 cm → 0.05 m`. |
| m → cm response conversion | No implementation in provider/managers | ❌ Responses are returned without conversion. |
| Rectangle registration metadata | `onshape_sketch_rectangle` declaration | ❌ Contract requires `address_json: object` and `entities: array`; implementation exposes a string plus four coordinate parameters. |
| Basic auth header | `OnshapeClient.SendAsync` sets Basic Authorization | ✅ |
| Configured HMAC overlay | `OnshapeClient.SendAsync` | ❌ HMAC helper exists but request sending does not apply a configured HMAC signature. |
| Sketch POST content/type/skew | WireMock sketch write test and `OnshapeClient` | ✅ JSON content type and write query skew protection are present. |
| Golden JSON conformance | Fixture glob and tests | ❌ No `tests/McpCad.Tests/Onshape/Fixtures/` files exist. |

## Design Coherence

| Design decision | Result | Evidence |
| --- | --- | --- |
| Builder/template isolation from HTTP | ✅ | `OnshapeSketchBuilder` and `OnshapeFeatureTemplates` are pure JSON builders. |
| Generic `SketchReplace` provider seam | ✅ | `ICadWebProvider` accepts optional sketch request; provider delegates it. |
| Wrapped FeatureAdd envelope | ✅ | `FeatureManager` uses a template envelope or supplied body. |
| Microversion-safe writes | ✅ | `OnshapeClient` adds `rejectMicroversionSkew=true`; successful writes update driver cache. |
| P0 E2E WireMock flow | ❌ | The named P0 flow test uses `MockOnshapeProvider`, not the real provider/managers/WireMock chain specified by the design. |
| Golden structural strategy | ❌ | Required fixtures and structural golden comparisons are absent. |
| Final docs/matrix slice | ✅ | Matrix and provider modeling documentation are present. |

## TDD Compliance

| Check | Result | Details |
| --- | --- | --- |
| TDD Cycle Evidence reported | ❌ | `apply-progress.md` has no `TDD Cycle Evidence` table. |
| All tasks have test evidence | ❌ | 0/27 task rows provide the required RED/GREEN/TRIANGULATE/SAFETY-NET evidence. |
| RED confirmed | ❌ | Only aggregate Slice 3/4 RED commands are recorded; Slices 1/2 have none. |
| GREEN confirmed | ❌ | Current full suite fails. |
| Triangulation adequate | ❌ | No per-task triangulation record; required golden coverage is absent. |
| Safety net for modified files | ❌ | No safety-net evidence is recorded. |

**TDD Compliance:** 0/6 checks passed. Strict TDD evidence is a critical gate failure.

## Test Layer Distribution

| Layer | Tests | Files | Tools |
| --- | ---: | ---: | --- |
| Unit | 36 | 4 | xUnit + FluentAssertions |
| Integration | 7 | 1 | WireMock.Net |
| E2E | 0 | 0 | not installed / no real-provider flow |
| **Total change-related** | **43** | **5** | |

## Assertion Quality

**Assertion quality:** ⚠️ No tautologies or ghost loops found. The sketch tests assert counts/types but not loop closure or line endpoints; this is insufficient behavioral triangulation, not a trivial assertion.

## Quality Metrics

**Linter:** ➖ Not detected  
**Type checker:** ❌ Solution build failed (Inventor COM dependency unavailable on this Linux verifier).

## Issues

### CRITICAL

1. The required task checklist is incomplete: only 12/27 task headings are checked.
2. Strict TDD evidence is absent; no task-level TDD Cycle Evidence table exists.
3. The full required test command fails: 95 passed, 1 failed, 7 skipped.
4. Required golden fixtures and golden structural conformance tests are absent.
5. Required response conversion from meters to centimeters is not implemented.
6. Feature discovery's required feature shape is not verified.
7. HMAC overlay required by the integration delta is not applied to outgoing requests.

### WARNING

1. The solution build fails on Linux due to the unavailable Inventor COM assembly.
2. No live Onshape proof supports matrix entries marked `implemented`.
3. The stated P0 E2E is mock-provider only, not the designed real-provider/WireMock flow.
4. `address_json` missing-field validation and exact cm precision lack test coverage.
5. Sketch assertions do not prove closed-loop geometry or golden structure.

### SUGGESTION

1. Add changed-file coverage once a collector is configured.
2. Remove or initialize `_errorResult` to eliminate CS0649.

## Final Verdict

**FAIL** — completion metadata, strict-TDD evidence, spec gaps, and the full-suite failure prevent archive readiness.
