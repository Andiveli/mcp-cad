# Tasks: onshape-tool-parity

## Review Workload Forecast

| Field | Value |
|-------|-------|
| Estimated changed lines | 1150–1600 total across 4 PRs; 250–450 per slice |
| 400-line budget risk | Medium |
| Chained PRs recommended | Yes |
| Suggested split | PR 1 (Sketch) → PR 2 (Extrude+P0) → PR 3 (P1 features) → PR 4 (P2 asm+docs) |
| Delivery strategy | auto-chain |
| Chain strategy | feature-branch-chain |

```text
Decision needed before apply: No
Chained PRs recommended: Yes
Chain strategy: feature-branch-chain
400-line budget risk: Medium
```

---

## Chain Strategy

- **Tracker branch:** `feature/onshape-tool-parity` — draft PR, no-merge until all child PRs integrated.
- **Child PR #1** targets tracker branch.
- **Child PR #2** targets child PR #1 branch (`feature/onshape-tool-parity/slice-1`).
- **Child PR #3** targets child PR #2 branch (`feature/onshape-tool-parity/slice-2`).
- **Child PR #4** targets child PR #3 branch (`feature/onshape-tool-parity/slice-3`).
- Each PR body (chained-pr skill): start/end scope, dependencies, follow-up, out-of-scope, dependency diagram with `📍` on current PR.
- **Rollback:** Revert child PRs in reverse order. All P0 tools are additive except stub removal (documented in PR body).

---

## Task Conventions

- `[RED]` — Write a failing test first (Strict TDD).
- `[GREEN]` — Minimum code to pass the test.
- `[TRIANGULATE]` — Add more test cases to generalise the solution.
- `[REFACTOR]` — Improve structure without changing behaviour.
- Where tests do not apply (docs, config, interface shape), tasks use `[ADD]` / `[MODIFY]` / `[REMOVE]`.
- Test runner: `dotnet test tests/McpCad.Tests/` after each GREEN phase.

## Pending-checkbox disposition (2026-07-17)

`[x]` entries below may record a completed **classification**, not implementation. Optional/non-goal work explicitly states that it was not implemented so the dispatcher does not schedule it as an implementation task.

| Pending item | Disposition | Evidence / action |
|---|---|---|
| 3.7 parameter research, tool, and test | Optional / non-goal | Not implemented; P1 parameter mutation remains outside v1 scope. |
| 4.7 live smoke add and both verification rows | Optional / non-goal | Not implemented or run; requires sandbox credentials and `ONSHAPE_LIVE=1`. |
| Full suite | Historical verification item | Completed: `dotnet test tests/McpCad.Tests/ --no-restore` exits 0 (105 passed, 7 skipped, 0 failed). |
| Collision, skew, and legacy rows | Historical verification items | Prior focused evidence is recorded in `apply-progress.md`. |
| Golden structural JSON row | Required implementation | Completed in this apply continuation with fixture-consuming conformance tests. |
| Matrix status row | Historical verification item | Completed: fillet, chamfer, and corrected revolve returned HTTP 200 with `featureStatus: OK`; assembly list/create/delete completed live with cleanup. Hole was removed from MCP registration and reclassified `not_supported` after every native V3 payload regenerated with `featureStatus: ERROR`. |
| Child-PR budget row | Stale task state for this apply batch | No PR was created by instruction; review-budget enforcement belongs to PR delivery. |

---

## Slice 1 — Sketch Builder + Real SketchReplace + Golden Tests

**Branch:** `feature/onshape-tool-parity/slice-1` (child PR #1)
**Primary paths:** `Builders/OnshapeSketchBuilder.cs`, `PartStudioManager.cs`, `OnshapeApiPaths.cs`, `ICadWebProvider.cs`, `MockOnshapeProvider.cs`, `tests/McpCad.Tests/Onshape/`

### Task 1.1 — Add sketch URL path constant
- [x] [ADD] Determine sketch POST URL from CSWP spike / OpenAPI; add path helper in `src/McpCad.Onshape/OnshapeApiPaths.cs` (e.g. `SketchFeatures(...)` if distinct from `PartStudioFeatures`, or confirm `PartStudioFeatures` is the correct target).
- [x] [REFACTOR] Add XML doc on the path method noting it is used for both GET (list) and POST (sketch replace).
- [x] [VERIFY] One integration test URL match in `ExportPollingTests` or `OnshapeProviderConformanceTests`: `dotnet test --filter "SketchUrlMatches"`.

### Task 1.2 — Create `OnshapeSketchBuilder`
- [x] [RED] Write `OnshapeSketchBuilderTests.cs` in `tests/McpCad.Tests/Onshape/`:
  - Test `BuildRectangle` returns JSON with 4 `BTMSketchLine` entities forming a closed loop.
  - Test `BuildLine` returns a single `BTMSketchLine` entity.
  - Test `BuildCircle` returns a `BTMSketchCircle` entity.
- [x] [GREEN] Create `src/McpCad.Onshape/Builders/OnshapeSketchBuilder.cs` with:
  - `BuildRectangle(double x1, double y1, double x2, double y2)` → `JsonDocument`.
  - `BuildLine(double x1, double y1, double x2, double y2)` → `JsonDocument`.
  - `BuildCircle(double cx, double cy, double radius)` → `JsonDocument`.
  - Coordinates in meters (caller converts cm → m before calling).
- [x] [TRIANGULATE] Add tests for zero-size rectangle, negative coordinates, single-point line (edge cases).
- [x] [REFACTOR] Extract shared entity serialisation helpers; ensure IDs are deterministic for golden matching.

### Task 1.3 — Add golden JSON fixtures
- [x] [ADD] Create `tests/McpCad.Tests/Onshape/Fixtures/Sketch/rectangle_btmsketch.golden.json` (structural reference from CSWP capture; normalise IDs to `{id}` placeholders).
- [x] [ADD] Create `tests/McpCad.Tests/Onshape/Fixtures/Sketch/line_btmsketch.golden.json`.
- [x] [ADD] Create `tests/McpCad.Tests/Onshape/Fixtures/Sketch/circle_btmsketch.golden.json`.
- [x] [VERIFY] Builder tests assert structural JSON shape (required keys, `btType`, `parameterId` presence) against golden fixtures, not byte-identical.

### Task 1.4 — Extend `ICadWebProvider.SketchReplace` signature
- [x] [RED] Write provider conformance test in `OnshapeProviderConformanceTests.cs`: `SketchReplace_WithSketchPayload_ReturnsSuccess` using `MockOnshapeProvider` with new overload.
- [x] [MODIFY] Update `src/McpCad.Core/ICadWebProvider.cs`:
  - Change `SketchReplace(CadElementAddress address)` to accept optional sketch payload:
    ```csharp
    Dictionary<string, object?> SketchReplace(
        CadElementAddress address,
        IReadOnlyDictionary<string, object?>? sketchRequest = null);
    ```
- [x] [MODIFY] Update `MockOnshapeProvider` in `tests/McpCad.Tests/Mocks/MockOnshapeProvider.cs` to match new signature; log sketch request params.
- [x] [MODIFY] Update `OnshapeProvider.cs` to pass through new overload to `PartStudioManager`.
- [x] [GREEN] Ensure existing `create_sketch` MCP tool compiles (passes `null` for sketchRequest → backward compat).

### Task 1.5 — Wire `PartStudioManager.SketchReplace` (real HTTP)
- [x] [RED] Write WireMock test in `PartStudioModelingWireMockTests.cs`: POST to features endpoint captures JSON body; assert `Content-Type: application/json` and `rejectMicroversionSkew=true` query present.
- [x] [GREEN] Implement `PartStudioManager.SketchReplace(CadElementAddress, JsonElement sketchPayload)`:
  - POST to `OnshapeApiPaths.PartStudioFeatures(...)` (or new sketch path from Task 1.1).
  - Serialize sketch payload as JSON body.
  - Include `rejectMicroversionSkew: true` in query string.
  - Call `_driver.UpdateMicroversionFromJson` on success response.
  - Return `OnshapeJsonHelper.SuccessEnvelope(...)`.
- [x] [TRIANGULATE] Test 409 skew → `OnshapeMicroversionSkewException` path in WireMock test.
- [x] [VERIFY] `dotnet test --filter "PartStudioModeling"` passes.

### Task 1.6 — Remove stubs
- [x] [REMOVE] `PartStudioManager.SketchReplaceStub` method (replace callers with real implementation).
- [x] [REMOVE] `PartStudioManager.FeatureAddStub` method (stays unreferenced until slice 2; remove if no other callers).
- [x] [VERIFY] `dotnet test` — all existing conformance tests still pass.

---

## Slice 2 — Extrude Template + Fixed `FeatureAdd` + P0 Atomics + E2E WireMock

**Branch:** `feature/onshape-tool-parity/slice-2` (child PR #2, parent: `slice-1`)
**Primary paths:** `Builders/OnshapeFeatureTemplates.cs`, `FeatureManager.cs`, `OnshapeAtomicTools.cs`, `OnshapeProvider.cs`, `tests/McpCad.Tests/Onshape/`

### Task 2.1 — Create `OnshapeFeatureTemplates` with extrude
- [x] [RED] Write `OnshapeFeatureTemplatesTests.cs` in `tests/McpCad.Tests/Onshape/`:
  - `ExtrudeFromSketch_ReturnsFeatureEnvelope`: asserts root object has `feature` key, `feature.featureType == "extrude"`, `feature.parameters` is array, `rejectMicroversionSkew: true` present.
  - Golden fixture match: `tests/McpCad.Tests/Onshape/Fixtures/Features/extrude_envelope.golden.json`.
- [x] [GREEN] Create `src/McpCad.Onshape/Builders/OnshapeFeatureTemplates.cs` with:
  - `ExtrudeFromSketch(string sketchFeatureId, double depthMeters, string direction, string booleanOp)` → `JsonDocument`.
  - Parameter array shape from CSWP spike (BT parameter types, `parameterId`, `value`).
- [x] [TRIANGULATE] Add tests for negative depth, reverse direction, joined boolean op.
- [x] [REFACTOR] Extract `BuildParameter(...)` helpers for reuse across P1 templates (Slice 3).

### Task 2.2 — Fix `FeatureManager.FeatureAdd` to send BT envelope
- [x] [RED] Write WireMock test `FeatureAdd_FlatPayload_RegressionGuard` in `PartStudioModelingWireMockTests.cs`:
  - Assert POST body starts with `{"feature":` not `{"featureType":`.
  - Assert `rejectMicroversionSkew: true` in body (not only query).
- [x] [GREEN] Modify `FeatureManager.FeatureAdd(CadElementAddress, string, IReadOnlyList<...>)`:
  - Accept optional pre-built envelope from `OnshapeFeatureTemplates`.
  - Serialize as `{ "feature": { "featureType": ..., "parameters": ..., "suppressed": false }, "rejectMicroversionSkew": true }`.
  - Default behaviour: if no template provided, build minimal envelope from `featureType` + `parameters` (backward compat for non-extrude callers until Slice 3).
- [x] [TRIANGULATE] WireMock test verifying `OnshapeProvider.FeatureAdd` with extrude template produces correct envelope.
- [x] [VERIFY] `dotnet test --filter "FeatureAdd"` passes.

### Task 2.3 — Register P0 `onshape_sketch_rectangle` MCP tool
- [x] [RED] Write mock-provider test in `OnshapeAtomicToolsModelingTests.cs`:
  - Call `onshape_sketch_rectangle` with `address_json`, `x1_cm`, `y1_cm`, `x2_cm`, `y2_cm`.
  - Assert provider `SketchReplace` was called with sketch request containing rectangle parameters.
- [x] [GREEN] Add tool to `OnshapeAtomicTools.cs`:
  ```csharp
  [McpServerTool, Description("Create a rectangle sketch in a Part Studio using BTMSketch. Coordinates in cm.")]
  public Dictionary<string, object?> onshape_sketch_rectangle(
      string address_json,
      double x1_cm, double y1_cm,
      double x2_cm, double y2_cm,
      string? plane = null)
  ```
  - Deserialize `address_json` → `CadElementAddress`.
  - Convert cm → m via `OnshapeUnitConversion.CmToMeters`.
  - Call `OnshapeSketchBuilder.BuildRectangle(...)`.
  - Call `provider.SketchReplace(address, sketchRequestDict)`.
- [x] [TRIANGULATE] Test error: invalid address JSON → `ToolHelpers.Error`.
- [x] [TRIANGULATE] Test error: invalid coordinates (x1 > x2 not necessarily error — document behaviour).

### Task 2.4 — Register P0 `onshape_extrude` MCP tool
- [x] [RED] Write mock-provider test:
  - Call `onshape_extrude` with `address_json`, `sketch_feature_id`, `depth_cm`, `direction`.
  - Assert provider `FeatureAdd` called with `featureType: "extrude"` and envelope from `OnshapeFeatureTemplates`.
- [x] [GREEN] Add tool to `OnshapeAtomicTools.cs`:
  ```csharp
  [McpServerTool, Description("Extrude a sketch profile by a given depth in cm.")]
  public Dictionary<string, object?> onshape_extrude(
      string address_json,
      string sketch_feature_id,
      double depth_cm,
      string direction = "positive",
      string boolean_op = "new")
  ```
  - Convert cm → m.
  - Build template via `OnshapeFeatureTemplates.ExtrudeFromSketch(...)`.
  - Call `provider.FeatureAdd(...)` with pre-built envelope.
- [x] [TRIANGULATE] Test negative depth → direction reversed? Document behaviour in tool description.

### Task 2.5 — Register P0 `onshape_feature_list` MCP tool
- [x] [RED] Write mock-provider test:
  - Call `onshape_feature_list` with `address_json`.
  - Assert provider `FeatureList` called (via new `ICadWebProvider` seam if added, or direct manager access).
- [x] [GREEN] Add tool to `OnshapeAtomicTools.cs`:
  ```csharp
  [McpServerTool, Description("List features in a Part Studio (analogous to Inventor sketch_profiles).")]
  public Dictionary<string, object?> onshape_feature_list(string address_json)
  ```
  - Wire to `provider.FeatureList(address)` if `ICadWebProvider` exposes it (Task 2.6).
  - Else access `FeatureManager` directly.
- [x] [VERIFY] Ensure return shape includes `featureId`, `featureType`, `parameters` per spec.

### Task 2.6 — Expose `FeatureList` on `ICadWebProvider` (if not already)
- [x] [CHECK] `ICadWebProvider` does NOT currently have `FeatureList` method — `OnshapeProvider` delegates to `_features.FeatureList` directly only via `FeatureGetResult`.
- [x] [ADD] Add to `ICadWebProvider`:
  ```csharp
  Dictionary<string, object?> FeatureList(CadElementAddress partStudio);
  ```
- [x] [ADD] Add mock implementation in `MockOnshapeProvider`.
- [x] [ADD] Wire in `OnshapeProvider.FeatureList` → `_features.FeatureList`.
- [x] [VERIFY] Conformance tests pass.

### Task 2.7 — Deprecate legacy `create_sketch` / `add_extrude`
- [x] [MODIFY] `create_sketch`: Change description to `"DEPRECATED: use onshape_sketch_rectangle. Creates a default rectangle sketch."`.
- [x] [MODIFY] `create_sketch`: Delegate to `onshape_sketch_rectangle` with default corner coordinates if only `address_json` provided; else pass through corner args (product choice for backward compat).
- [x] [MODIFY] `add_extrude`: Change description to `"DEPRECATED: use onshape_extrude. Extrudes using legacy flat parameters."`.
- [x] [MODIFY] `add_extrude`: Internally convert `distance_cm`, `direction` → `OnshapeFeatureTemplates.ExtrudeFromSketch(...)` and call the BT envelope path.
- [x] [VERIFY] Existing `OnshapeAtomicToolsTests` or integration tests still pass after delegation change.

### Task 2.8 — P0 E2E WireMock test (rectangle → extrude)
- [x] [RED] Write `PartStudioModelingWireMockTests.cs` E2E test:
  - Mock: sketch POST → success + microversion response.
  - Mock: feature list GET → returns sketch feature.
  - Mock: feature POST → success + microversion response.
  - Call `onshape_sketch_rectangle` → then `onshape_feature_list` → then `onshape_extrude` via `OnshapeAtomicTools` with real provider.
- [x] [GREEN] Wire full flow through `OnshapeProvider` → managers → HTTP.
- [x] [VERIFY] `dotnet test --filter "P0EndToEnd"` passes.

### Task 2.9 — Tool registration collision tests
- [x] [ADD] Ensure `ToolRegistrationCollisionTests` covers all new `onshape_*` names: no cross-provider collision with Inventor `AtomicTools`.
- [x] [VERIFY] `dotnet test --filter "ToolRegistrationCollision"` passes.

---

## Slice 3 — P1 Features (Revolve, Fillet, Chamfer, Hole) + Parameter Support

**Branch:** `feature/onshape-tool-parity/slice-3` (child PR #3, parent: `slice-2`)
**Primary paths:** `Builders/OnshapeFeatureTemplates.cs` (extend), `OnshapeAtomicTools.cs` (add), `tests/McpCad.Tests/Onshape/`

### Task 3.1 — Add `Revolve` template
- [x] [RED] Write template test: `RevolveFromSketch_ReturnsFeatureEnvelope` — assert `featureType == "revolve"`, angle parameter present.
- [x] [GREEN] Add `RevolveFromSketch(sketchFeatureId, angleDeg, direction, booleanOp)` to `OnshapeFeatureTemplates`.
- [x] [TRIANGULATE] Test edge cases: 0° angle, 360° full revolve, reverse direction.

### Task 3.2 — Add `Fillet` template
- [x] [RED] Write template test: `Fillet_ReturnsFeatureEnvelope` — assert `featureType == "fillet"`, radius parameter present.
- [x] [GREEN] Add `Fillet(edgeIds, radiusMeters)` to `OnshapeFeatureTemplates`.
- [x] [TRIANGULATE] Test multiple edges, zero radius (error expected — document behaviour).

### Task 3.3 — Add `Chamfer` template
- [x] [RED] Write template test: `Chamfer_ReturnsFeatureEnvelope` — assert `featureType == "chamfer"`.
- [x] [GREEN] Add `Chamfer(edgeIds, distanceMeters)` to `OnshapeFeatureTemplates`.
- [x] [TRIANGULATE] Test equal-distance chamfer; two-distance chamfer remains out of scope.

### Task 3.4 — Add `Hole` template
- [x] [RED] Write template test: `Hole_ReturnsFeatureEnvelope` — assert `featureType == "hole"`, diameter/depth parameters present.
- [x] [GREEN] Add `Hole(centerPoint, diameterMeters, depthMeters, holeType)` to `OnshapeFeatureTemplates`.
- [x] [TRIANGULATE] Test blind hole vs through-hole, threaded vs simple.

### Task 3.5 — Register P1 MCP tools
- [x] [RED] Write mock-provider tests for each P1 tool:
  - `onshape_revolve` → calls `FeatureAdd` with revolve envelope.
  - `onshape_fillet` → calls `FeatureAdd` with fillet envelope.
  - `onshape_chamfer` → calls `FeatureAdd` with chamfer envelope.
  - `onshape_hole` → calls `FeatureAdd` with hole envelope.
- [x] [GREEN] Add all four tools to `OnshapeAtomicTools.cs` with:
  - `address_json` → `CadElementAddress`.
  - Feature-specific parameters in **cm** (convert to m at template boundary).
  - Description referencing `(P1)` and Onshape API limitations (if any known from CSWP).
- [x] [TRIANGULATE] Add WireMock structural tests for each P1 envelope shape.
- [x] [VERIFY] `dotnet test --filter "P1"` passes.

### Task 3.6 — P1 tools in collision tests
- [x] [ADD] Add `ToolRegistrationCollisionTests` entries for `onshape_revolve`, `onshape_fillet`, `onshape_chamfer`, `onshape_hole`.
- [x] [VERIFY] `dotnet test --filter "ToolRegistrationCollision"` passes.

### Task 3.7 — (Optional) `onshape_param_*` tools where REST supports
 - [x] [OPTIONAL / NON-GOAL — NOT IMPLEMENTED] Determine which parameters Onshape REST supports updating post-creation (e.g. extrude depth, fillet radius) via API docs / CSWP.
 - [x] [OPTIONAL / NON-GOAL — NOT IMPLEMENTED] If supported: add `onshape_param_set` tool that PATCHes a feature parameter by `featureId` + `parameterId`.
 - [x] [OPTIONAL / NON-GOAL — NOT IMPLEMENTED] If implemented: WireMock test for parameter update PATCH body shape.

---

## Slice 4 — P2 Assembly Atomics + Parity Matrix + Documentation

**Branch:** `feature/onshape-tool-parity/slice-4` (child PR #4, parent: `slice-3`)
**Primary paths:** `OnshapeAtomicTools.cs` (extend), `docs/onshape-parity-matrix.md`, `docs/providers.md`, `tests/McpCad.Tests/`

### Task 4.1 — Register `onshape_asm_list_instances` MCP tool
- [x] [RED] Write mock-provider test:
  - Call `onshape_asm_list_instances(address_json)`.
  - Assert provider `AssemblyListInstances` called.
- [x] [GREEN] Add tool to `OnshapeAtomicTools.cs`:
  ```csharp
  [McpServerTool, Description("List instances in an Onshape assembly.")]
  public Dictionary<string, object?> onshape_asm_list_instances(string address_json)
  ```
  - Delegates to `provider.AssemblyListInstances`.
- [x] [TRIANGULATE] Test empty assembly → empty instances array.

### Task 4.2 — Register `onshape_asm_create_instance` MCP tool
- [x] [RED] Write mock-provider test:
  - Call `onshape_asm_create_instance(address_json, part_document_id, part_element_id, transform?)`.
  - Assert provider `AssemblyCreateInstance` called with correct params.
- [x] [GREEN] Add tool:
  ```csharp
  [McpServerTool, Description("Create an instance of a part in an Onshape assembly.")]
  public Dictionary<string, object?> onshape_asm_create_instance(
      string address_json, string part_document_id, string part_element_id)
  ```
- [x] [TRIANGULATE] Confirmed no optional transform is supported by `ICadWebProvider.AssemblyCreateInstance`.

### Task 4.3 — Register `onshape_asm_delete_instance` MCP tool
- [x] [RED] Write mock-provider test:
  - Call `onshape_asm_delete_instance(address_json, instance_id)`.
  - Assert provider `AssemblyDeleteInstance` called.
- [x] [GREEN] Add tool:
  ```csharp
  [McpServerTool, Description("Delete an instance from an Onshape assembly.")]
  public Dictionary<string, object?> onshape_asm_delete_instance(string address_json, string instance_id)
  ```
- [x] [TRIANGULATE] Test deleting non-existent instance → error handling.

### Task 4.4 — Create parity matrix document
- [x] [ADD] Create `docs/onshape-parity-matrix.md` with:
  - Columns: Inventor Atomic | Onshape MCP | REST endpoint | Status | Notes.
  - All P0 rows marked `implemented` (references slice 1–2 artifacts).
  - P1 rows (revolve, fillet, chamfer, hole) marked `implemented`.
  - P2 rows (assembly) marked `implemented`.
  - Remaining Inventor atomics (~83 total) marked `declarative-composite` or `not_supported` per spec.
  - Export rows (`onshape_export_step`, `onshape_export_stl`) referencing existing implementation.
  - Section on `address_json` contract and cm units at MCP boundary.
- [x] [VERIFY] matrix is readable and references match actual tool names in `OnshapeAtomicTools.cs`.

### Task 4.5 — Update `docs/providers.md` with modeling documentation
- [x] [MODIFY] `docs/providers.md`:
  - Add "Modeling" section under Onshape provider.
  - Document `address_json` format and required fields.
  - Document cm-at-MCP-boundary policy and `OnshapeUnitConversion` site.
  - Document P0 E2E flow: sketch → feature → export.
  - Document sandbox references: doc `a2e3cbf0909c00279ce5d80b`, workspace `05605c0cafe63902d2a627a0`, Part Studio `dce1127c54f4f7f7a620dc26`.
  - Reference parity matrix for full tool catalog.
- [x] [VERIFY] Markdown link is relative to `docs/` and targets the added matrix.

### Task 4.6 — Assembly tool collision tests
- [x] [ADD] `ToolRegistrationCollisionTests` entries for `onshape_asm_list_instances`, `onshape_asm_create_instance`, `onshape_asm_delete_instance`.
- [x] [VERIFY] `dotnet test --filter "ToolRegistrationCollision"` passes.

### Task 4.7 — (Optional) Live smoke test behind env flag
 - [x] [OPTIONAL / NON-GOAL — NOT IMPLEMENTED] Create `tests/McpCad.Tests/Onshape/OnshapeLiveSmokeTests.cs`:
  - `[Fact(Skip = "Set ONSHAPE_LIVE=1 to run")]`.
  - Execute P0 E2E: rectangle → extrude → STEP on reference sandbox.
  - Guard: skip unless `Environment.GetEnvironmentVariable("ONSHAPE_LIVE") == "1"`.
 - [x] [OPTIONAL / NON-GOAL — NOT RUN] `ONSHAPE_LIVE=0 dotnet test` skips live tests.
 - [x] [OPTIONAL / NON-GOAL — NOT RUN] `ONSHAPE_LIVE=1 dotnet test` runs live tests (requires valid credentials).

---

## Dependency Summary

```
Slice 1 (sketch path, builder, real SketchReplace, golden fixtures)
  │
  ▼
Slice 2 (extrude template, FeatureAdd fix, P0 atomics, E2E WireMock)
  │
  ▼
Slice 3 (P1 templates + tools, parameter support)
  │
  ▼
Slice 4 (P2 assembly tools, parity matrix, docs update, live smoke)
```

**No circular dependencies between slices.** Each slice depends only on its predecessor.

## Verification Gate (all slices)

 - [x] [HISTORICAL VERIFICATION] `dotnet test tests/McpCad.Tests/` — 105 passed, 7 skipped, 0 failed.
 - [x] [HISTORICAL VERIFICATION] No new MCP tool name collisions with Inventor `AtomicTools` in `ToolRegistrationCollisionTests`.
 - [x] [VERIFY] All golden fixture tests compare structural JSON (required keys, `btType`, `parameterId`), not volatile IDs.
 - [x] [HISTORICAL VERIFICATION] Every POST to Onshape includes `rejectMicroversionSkew=true` (verified in WireMock body matchers).
 - [x] [HISTORICAL VERIFICATION] `create_sketch` / `add_extrude` are documented `DEPRECATED` and delegate to real BT paths.
 - [x] [HISTORICAL VERIFICATION] Parity matrix reflects live-proven implementation status; unproven hole support is explicitly `not_supported`.
 - [x] [STALE TASK STATE — NO PR DELIVERY REQUESTED] Each child PR ≤400 changed lines unless `size:exception` documented in PR body.
