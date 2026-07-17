# Design: onshape-tool-parity

## Context

Onshape integration (`onshape-provider` lineage) exposes `ICadWebProvider`, navigation, export polling, and ~20 `onshape_*` MCP atomics. Part Studio **modeling** is not production-ready:

| Layer | Current behavior | Spec requirement |
|-------|------------------|------------------|
| `OnshapeProvider.SketchReplace` | Delegates to `PartStudioManager.SketchReplaceStub` | Real BTMSketch POST |
| `FeatureManager.FeatureAdd` | Serializes flat `{ featureType, parameters }` | Wrapped `{ feature, rejectMicroversionSkew }` |
| `OnshapeAtomicTools` | Legacy `create_sketch` / `add_extrude` only; no P0 `onshape_*` modeling tools | P0 catalog + deprecation text |

Agents trained on Inventor atomics cannot run rectangle → extrude → STEP on cloud without this change. v1 scope is **Onshape atomic MCP tools only**; `SkillTools` parity is deferred.

**Session delivery (authoritative):** `auto-chain`, `feature-branch-chain`, ≤400 changed lines per child PR unless `size:exception`.

**Reference sandbox (live smoke / golden capture):** doc `a2e3cbf0909c00279ce5d80b`, workspace `05605c0cafe63902d2a627a0`, Part Studio `dce1127c54f4f7f7a620dc26`.

---

## Goals / Non-Goals

### Goals

- Replace sketch stub and fix feature POST so Onshape accepts BT payloads.
- Introduce server-side builders/templates so MCP tools stay intent-shaped (cm, `address_json`) while REST stays Onshape-native (meters, BT arrays).
- Register P0 `onshape_*` atomics; wire legacy tools to the same paths with deprecation descriptions.
- Prove behavior with WireMock golden structural tests; optional live smoke behind env flag.
- Ship parity matrix + `docs/providers.md` updates in the final chain slice.

### Non-Goals (v1)

- `SkillTools` parity, unified cross-backend tool names, COM-style active sketch session.
- Renaming legacy MCP names to Inventor names; unprefixed Inventor names on Onshape server.
- P3+ Inventor atomics beyond P0–P2 unless explicitly pulled into a slice.

---

## Architecture Overview

```
┌─────────────────────────────────────────────────────────────────┐
│ OnshapeAtomicTools (McpCad.Tools)                               │
│  onshape_sketch_* / onshape_extrude / onshape_feature_list      │
│  legacy: create_sketch, add_extrude → same provider seam        │
└────────────────────────────┬────────────────────────────────────┘
                             │ ICadWebProvider
┌────────────────────────────▼────────────────────────────────────┐
│ OnshapeProvider                                                 │
│  SketchReplace / FeatureAdd / (FeatureList via new seam)        │
└─────┬───────────────────────────────┬─────────────────────────┘
      │                                 │
┌─────▼──────────────┐         ┌────────▼─────────────┐
│ PartStudioManager  │         │ FeatureManager       │
│  SketchReplace     │         │  FeatureList (GET)   │
│  (+ sketch path)   │         │  FeatureAdd (POST)   │
└─────┬──────────────┘         └────────┬─────────────┘
      │ uses                            │ uses
┌─────▼──────────────┐         ┌────────▼─────────────┐
│ OnshapeSketchBuilder│        │ OnshapeFeatureTemplates│
│  BTMSketch JSON    │         │  extrude (P0), …     │
└─────┬──────────────┘         └────────┬─────────────┘
      │                                 │
      └────────────┬────────────────────┘
                   │ OnshapeClient.SendJsonAsync
                   │ OnshapeDriver (LastMicroversion)
                   ▼
            Onshape REST (WireMock / live)
```

**Design principles**

1. **Declarative sketches:** One BTMSketch replace per agent intent (rectangle / line / circle), not incremental COM sketch edits.
2. **Single unit seam:** `OnshapeUnitConversion` (G9) at template/builder boundary only; MCP args in cm.
3. **Microversion discipline:** Every modeling POST includes `rejectMicroversionSkew: true`; success responses update `OnshapeDriver.LastMicroversion` (existing `UpdateMicroversionFromJson`).
4. **Interface minimalism:** Extend `ICadWebProvider` only if atomics cannot express required inputs through existing `SketchReplace` / `FeatureAdd` signatures.

---

## Component Design

### 1. `OnshapeSketchBuilder` (new: `src/McpCad.Onshape/Builders/OnshapeSketchBuilder.cs`)

**Responsibility:** Build BTMSketch-compatible `JsonDocument` or serializable DTO graphs from agent-friendly inputs (cm coordinates, plane reference, entity list).

**P0 builders (methods or static factories):**

| Method | MCP mapping | Output |
|--------|-------------|--------|
| `BuildRectangle(...)` | `onshape_sketch_rectangle` | Four `BTMSketchLine` entities, closed loop |
| `BuildLine(...)` | optional `onshape_sketch_line` | Single line segment |
| `BuildCircle(...)` | optional `onshape_sketch_circle` | Circle entity |

**Inputs (design-level):**

- Corner/center coordinates in **meters** (caller converts from cm via `OnshapeUnitConversion` before calling builder, keeping builders REST-pure).
- Sketch plane: default **Top** (XY) for P0 E2E unless `address_json` + feature tree implies an existing sketch feature id (see wiring).

**Output shape:** JSON matching golden fixtures under `tests/McpCad.Tests/Onshape/Fixtures/Sketch/` (structural keys: entities, constraints as required by CSWP capture—not necessarily byte-identical IDs).

**Non-responsibility:** HTTP, microversion headers, feature id resolution (manager layer).

### 2. `OnshapeFeatureTemplates` (new: `src/McpCad.Onshape/Builders/OnshapeFeatureTemplates.cs`)

**Responsibility:** Produce the **POST body root** for `FeatureManager.FeatureAdd`:

```json
{
  "feature": {
    "featureType": "extrude",
    "name": "...",
    "parameters": [ { "btType": "...", "parameterId": "...", ... } ],
    "suppressed": false
  },
  "rejectMicroversionSkew": true
}
```

**P0:** `ExtrudeFromSketch(sketchFeatureId, depthMeters, direction, booleanOp)` using parameter array shape from CSWP spike (not the loose `distanceCm` / `direction` dictionaries `add_extrude` uses today).

**P1 extension points:** `Revolve`, `Fillet`, `Chamfer`, `Hole` stubs or full templates in slice 3—same class/file family to avoid scatter.

**Regression guard:** `FeatureManager` must never serialize flat `{ featureType, parameters }` again; unit test asserts serialized root has top-level `feature` object.

### 3. `OnshapeApiPaths` (extend)

Add sketch-replace path helper when confirmed from OpenAPI / CSWP (e.g. Part Studio sketch endpoint or features sub-route for sketch feature). **Apply slice 1 deliverable:** path constant + one integration test URL match.

Existing `PartStudioFeatures(...)` remains for GET list and POST non-sketch features.

### 4. `PartStudioManager` wiring

| Change | Detail |
|--------|--------|
| Remove / stop using | `SketchReplaceStub`, `FeatureAddStub` from provider path |
| Add | `SketchReplace(CadElementAddress address, JsonElement sketchPayload)` **or** overload accepting builder output |
| HTTP | POST sketch payload; `Content-Type: application/json`; include skew rejection per spec |
| Success | `OnshapeJsonHelper.SuccessEnvelope` + `_driver.UpdateMicroversionFromJson` |

**Provider routing:** `OnshapeProvider.SketchReplace` today takes no payload—**interface extension required for P0** unless sketch is always implied (empty stub). Spec and atomic catalog require rectangle with geometry → extend `ICadWebProvider`:

```csharp
Dictionary<string, object?> SketchReplace(
    CadElementAddress address,
    IReadOnlyDictionary<string, object?>? sketchRequest = null);
```

- `null` / empty: preserve backward compat for legacy `create_sketch` only if product accepts no-op error; **preferred:** legacy tool builds default rectangle from tool args and calls overload.
- P0 atomics pass structured `sketchRequest` (type `rectangle`, corners in cm) deserialized in `OnshapeAtomicTools`, converted, passed to builder in provider or manager.

Alternative (if interface change rejected in apply): add `SketchReplaceRectangle(CadElementAddress, double x1, ...)` only—design **recommends** generic sketch request dict to limit interface churn for P1 entities.

### 5. `FeatureManager` wiring

| Method | Change |
|--------|--------|
| `FeatureList` | No change to GET; expose on provider if not already (`FeatureList` not on `ICadWebProvider` today) |
| `FeatureAdd` | Accept optional pre-built envelope from `OnshapeFeatureTemplates`, or `(featureType, agentParams)` internally mapped to template for `extrude` |

**Provider:** Add `FeatureList(CadElementAddress)` to `ICadWebProvider` delegating to `_features.FeatureList`—required for `onshape_feature_list` MCP tool (spec: feature discovery).

### 6. `OnshapeAtomicTools` — P0 registration

| Tool | Delegation | Notes |
|------|------------|-------|
| `onshape_sketch_rectangle` | `SketchReplace` + builder | `address_json`, corner coords cm, optional plane |
| `onshape_sketch_line` | same | P0 if needed for matrix; else slice 2 optional |
| `onshape_sketch_circle` | same | optional P0 |
| `onshape_extrude` | `FeatureAdd` via extrude template | `depth_cm`, direction, profile/sketch feature id |
| `onshape_feature_list` | `FeatureList` | Inventor `sketch_profiles` analog: parse features for sketch regions in tool layer or dedicated helper |

**Legacy:**

- `create_sketch`: description `DEPRECATED: use onshape_sketch_rectangle`; implement as thin wrapper with minimal default rectangle or require args (product choice in tasks).
- `add_extrude`: `DEPRECATED: use onshape_extrude`; map `distance_cm` → template.

**Collision:** Re-run `ToolRegistrationCollisionTests`; all new names prefixed `onshape_`.

### 7. `ICadWebProvider` delta (recommended)

| Member | Action |
|--------|--------|
| `SketchReplace` | Add optional sketch payload parameter (see §4) |
| `FeatureList` | **Add** — returns feature tree envelope |
| `FeatureAdd` | Keep signature; implementation uses templates internally |
| Assembly methods | Unchanged until slice 4 (`onshape_asm_*`) |

Update `MockOnshapeProvider` and conformance tests in the same PR as interface change.

---

## Data Flow

### P0 E2E: rectangle → extrude → STEP

1. Agent calls `onshape_sketch_rectangle(address_json, x1_cm, y1_cm, x2_cm, y2_cm)`.
2. `OnshapeAtomicTools` → `CadElementAddress.Deserialize`; validate required fields.
3. Provider converts cm → m; `OnshapeSketchBuilder.BuildRectangle` → JSON.
4. `PartStudioManager.SketchReplace` POST; driver caches new microversion `mv2`.
5. Agent calls `onshape_feature_list(address_json)` → obtains sketch `featureId` (and profile ids if exposed in response).
6. Agent calls `onshape_extrude(address_json, sketch_feature_id, depth_cm, ...)`.
7. `OnshapeFeatureTemplates.ExtrudeFromSketch` → envelope POST via `FeatureManager.FeatureAdd`; driver updates microversion.
8. Agent calls existing `onshape_export_step(address_json)` (unchanged export path).

### Error handling

- Validation errors at MCP layer → `ToolHelpers.Error` (no HTTP).
- 409 skew → existing `OnshapeMicroversionSkewException` path on writes.
- WireMock tests assert request body structure on POST matchers.

---

## Test Layout

```
tests/McpCad.Tests/Onshape/
├── Fixtures/
│   ├── Sketch/
│   │   ├── rectangle_btmsketch.golden.json      # structural reference from CSWP
│   │   └── line_btmsketch.golden.json
│   └── Features/
│       └── extrude_envelope.golden.json
├── OnshapeSketchBuilderTests.cs                 # builder output vs golden (normalize ids)
├── OnshapeFeatureTemplatesTests.cs              # extrude envelope shape
├── PartStudioModelingWireMockTests.cs           # POST body capture for sketch + extrude
├── OnshapeProviderConformanceTests.cs           # extend stubs for new paths
├── OnshapeAtomicToolsModelingTests.cs           # mock provider: P0 tools call correct seam
└── (optional) OnshapeLiveSmokeTests.cs          # `[Fact(Skip = "...")]` unless env ONshape_LIVE=1
```

**Strict TDD:** `dotnet test tests/McpCad.Tests/` per project policy.

**Golden rules:**

- Compare JSON structure (required keys, `btType`/`parameterId` presence) not volatile ids.
- WireMock: `Request.Create().WithBody(...)` or custom matcher asserting `feature` wrapper.

**Existing harness:** Reuse `WireMockServer` pattern from `OnshapeProviderConformanceTests` / `ExportPollingTests`.

---

## PR Slice Mapping (feature-branch-chain)

Tracker: draft PR on feature branch `feature/onshape-tool-parity` (exact name in tasks). Child PRs target tracker, then immediate parent.

| Slice | PR focus | Primary paths | Budget guard |
|-------|----------|---------------|--------------|
| **1** | Sketch builder + real `SketchReplace` + sketch golden/WireMock | `Builders/OnshapeSketchBuilder.cs`, `PartStudioManager.cs`, `OnshapeApiPaths.cs`, `ICadWebProvider` + mock, `tests/.../Sketch*` | No P0 MCP yet except interface/mock updates |
| **2** | Extrude template + fixed `FeatureAdd` + P0 atomics + E2E WireMock (rectangle→extrude) | `OnshapeFeatureTemplates.cs`, `FeatureManager.cs`, `OnshapeAtomicTools.cs`, `OnshapeProvider.cs`, legacy deprecation strings, collision tests | Largest slice—split atomics vs templates if >400 lines |
| **3** | P1 features + parameters where REST supports | Extend `OnshapeFeatureTemplates`, new `onshape_*` tools, provider tests | One feature family per sub-PR if needed |
| **4** | P2 `onshape_asm_*`, parity matrix, `docs/providers.md` | `OnshapeAtomicTools`, `docs/onshape-parity-matrix.md` (or `docs/providers.md` section), assembly MCP wrappers over existing `Assembly*` provider methods | Matrix can be generated script in follow-up if line-heavy |

Each PR body (chained-pr skill): start/end scope, dependencies, follow-up, out-of-scope, dependency diagram with `📍`, review budget estimate.

**Rollback:** Revert children reverse order; P0 tools are additive except stub removal.

---

## Parity Matrix (artifact)

- **Location:** `docs/onshape-parity-matrix.md` (or JSON + generated table)—slice 4.
- **Columns:** Inventor atomic | Onshape MCP | REST endpoint | Status | Notes.
- **P0 rows:** `sketch_rectangle` → `onshape_sketch_rectangle`; extrude → `onshape_extrude`; profiles → `onshape_feature_list`; export → existing `onshape_export_step`.
- **Status values:** `implemented` | `declarative-composite` | `not_supported` per spec.

---

## Risks / Trade-offs

| Risk | Mitigation in design |
|------|----------------------|
| BT payload drift | Golden files from CSWP; structural tests in CI |
| `ICadWebProvider` break | Single PR updates interface + `MockOnshapeProvider` + both providers |
| Sketch endpoint uncertainty | Slice 1 blocks on OpenAPI/spike; path helper isolated in `OnshapeApiPaths` |
| Slice 2 >400 lines | Sub-split: (2a) FeatureAdd envelope only, (2b) MCP P0 tools |
| Profile discovery gap | `onshape_feature_list` returns raw tree; agent parses or thin helper in Tools layer documents expected JSON paths |

---

## Open Questions (for tasks phase, not blockers for design)

1. Exact sketch POST URL and whether replace targets sketch **feature id** vs Part Studio element only.
2. Default rectangle parameters for deprecated `create_sketch` when called with only `address_json`.
3. Whether `onshape_extrude` accepts profile id vs sketch feature id (spike JSON from sandbox).

---

## Verification Checklist (design acceptance)

- [ ] Builders/templates isolated from HTTP
- [ ] Feature POST envelope documented and testable
- [ ] P0 MCP tools mapped to provider seams
- [ ] Test directories and golden strategy defined
- [ ] Four delivery slices aligned with proposal and chained-pr rules
- [ ] No `SkillTools` or Inventor name collision in new tools