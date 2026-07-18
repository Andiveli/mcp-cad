# Proposal: onshape-tool-parity

**Outcome:** Agents can run a real Part Studio modeling loop on Onshape—BTMSketch rectangle → extrude → STEP—using stable `onshape_*` atomic MCP tools that are **semantically** aligned with Inventor `AtomicTools`, without COM emulation or unprefixed name collisions.

## Quick path

1. Replace `SketchReplaceStub` and fix `FeatureAdd` to POST valid Onshape **BT** payloads (not `featureType` + loose parameters).
2. Ship P0 atomics (`onshape_sketch_rectangle`, `onshape_extrude`, feature/profile discovery) plus WireMock golden tests.
3. Verify optional live smoke on sandbox CSWP `a2e3cbf0909c00279ce5d80b`; document parity matrix and delivery slices for chained PRs.

## Intent

Archived **`onshape-provider`** delivered cloud plumbing (`ICadWebProvider`, HMAC/Basic client, navigation, export polling) but left **modeling broken or stubbed** and only **20** Onshape MCP atomics versus **~83** Inventor atomics in `AtomicTools.cs`. Agents trained on Inventor flows cannot do comparable cloud work today: `create_sketch` calls `SketchReplaceStub`; `add_extrude` hits `FeatureAdd` with a body Onshape rejects (needs wrapped `feature` BT JSON, not `{ featureType, parameters }`).

This change expands **Onshape atomic MCP tools only** (v1). **`SkillTools` parity is explicitly deferred** until atomics are stable.

### Agreed parity strategy (product)

| Decision | Choice |
|----------|--------|
| Mental model | **Agent intents + atomic tools** where Inventor is already atomic |
| Not doing | COM-style “active sketch” session emulation on the web provider |
| Naming | **`onshape_*` prefix** for new/canonical tools; no unprefixed Inventor names on the Onshape server (DPR collision rules) |
| Addressing | Part Studio / assembly operations require **`address_json`** (`CadElementAddress`); no fake “active document” in `ICadWebProvider` |
| North star E2E | Rectangle sketch (BTMSketch) → extrude (BT feature) → `onshape_export_step` on sandbox Part Studio |

## Scope

### In scope

- **Provider modeling:** real Part Studio REST for sketch replace and feature POST; retire stubs in `PartStudioManager` / wire `OnshapeProvider.SketchReplace` to implementation.
- **Payload layer:** `OnshapeSketchBuilder`, `OnshapeFeatureTemplates` (extrude first; templates extensible for revolve/fillet/hole).
- **Atomic tools (phased):**
  - **P0:** `onshape_sketch_rectangle` (and line/circle as needed), `onshape_extrude`, `onshape_feature_list` / region or profile discovery analogous to Inventor `sketch_profiles`, reuse existing export atomics in the E2E path.
  - **P1:** revolve, fillet, chamfer, hole, `onshape_param_*` where REST supports.
  - **P2:** `onshape_asm_*` instance atomics backed by existing `ICadWebProvider` assembly methods (provider already has list/create/delete; no MCP exposure yet).
- **Parity matrix artifact:** Inventor atomic → Onshape atomic → Onshape API → status (`implemented` | `declarative-composite` | `not_supported`).
- **Tests:** WireMock/golden JSON for BT bodies; table-driven provider tests; optional live smoke behind env flag.
- **Docs:** `docs/providers.md` — `address_json`, **cm** at MCP boundary, sandbox doc IDs.

### Out of scope (v1)

- **`SkillTools` parity** (follow-up change after atomics).
- Renaming legacy `create_sketch` / `add_extrude` to Inventor names; mark deprecated in descriptions and point to `onshape_*` canonical tools.
- Inventor-only: local `doc_save` / `doc_close`, iProperties, COM work features, PDF/DXF unless translation path is proven low-cost.
- Single unified MCP tool name across Inventor + Onshape backends.
- New backends (Fusion, SolidWorks).

## Capabilities

### New

| Capability | Purpose |
|------------|---------|
| `part-studio-modeling` | BTMSketch replace, feature add, microversion-safe writes |
| `onshape-tool-parity-matrix` | Traceability Inventor ↔ Onshape MCP ↔ REST |
| `onshape-atomic-catalog` | Registered atomics, naming, `CadElementAddress` contract |

### Modified

| Capability | Change |
|------------|--------|
| `onshape-integration` | Modeling endpoints and BT payload shapes; auth doc matches runtime (Basic overlay + HMAC config) |
| `web-cad-provider` | Extend `ICadWebProvider` only if atomics cannot map to existing `SketchReplace` / `FeatureAdd` |

## Approach

1. **Declarative sketches, not COM:** Onshape replaces whole sketch payloads (BTMSketch). Inventor’s many incremental sketch atomics collapse into a **smaller** Onshape set that builds BT server-side, plus list/read tools where agents need Inventor-like profile discovery.
2. **Naming:** new canonical tools use `onshape_`; validator tests must show **no cross-provider MCP name collisions** with `AtomicTools`.
3. **Units:** MCP arguments in **cm**; convert at `OnshapeClient` / managers (existing G9 policy).
4. **Legacy tools:** `create_sketch` / `add_extrude` either delegate to real BT paths or stay as thin wrappers with deprecation text; new work uses `onshape_*`.

## Delivery (session-bound)

Preflight choices (authoritative for tasks/apply):

| Setting | Value |
|---------|--------|
| `delivery_strategy` | **auto-chain** |
| `chain_strategy` | **feature-branch-chain** |
| `review_budget` | **400 lines** per PR (additions + deletions) |

Expect **>>400** total changed lines. Use **gentle-ai-chained-pr** at tasks phase. Feature-branch-chain rules:

- Open a **draft tracker PR** on the feature/tracker branch (no merge until integration complete).
- **Child PR #1** targets the tracker branch; later children target the **immediate parent** branch so diffs stay reviewable (~≤60 min each).
- Each PR states start/end, dependencies, follow-up, out-of-scope, and a dependency diagram with current PR marked.

**Suggested work units (map to child PRs on the tracker chain):**

| Slice | Contents |
|-------|----------|
| 1 | `OnshapeSketchBuilder` + real `SketchReplace` + WireMock/golden sketch tests |
| 2 | `OnshapeFeatureTemplates` (extrude) + real `FeatureAdd` + P0 atomics + E2E WireMock (rectangle → extrude) |
| 3 | P1 features + parameters where supported |
| 4 | P2 `onshape_asm_*` + parity matrix + `docs/providers.md` updates |

## Affected areas

| Area | Impact |
|------|--------|
| `src/McpCad.Onshape/Managers/PartStudioManager.cs` | Replace stubs |
| `src/McpCad.Onshape/Managers/FeatureManager.cs` | BT `feature` envelope for POST |
| `src/McpCad.Onshape/` (builders/templates) | New |
| `src/McpCad.Tools/Onshape/OnshapeAtomicTools.cs` | P0–P2 tools |
| `src/McpCad.Core/ICadWebProvider.cs` | Extend only if needed |
| `tests/McpCad.Tests/Onshape/` | Golden + conformance |
| `openspec/changes/onshape-tool-parity/specs/` | New specs (next phase) |
| `docs/providers.md` | Modeling + tool catalog |

## Assumptions

- Onshape connection/auth is operational (Basic overlay + config); live verify uses CSWP sandbox when credentials exist.
- Strict TDD: `dotnet test tests/McpCad.Tests/`.
- Codebase builds on **`onshape-provider`** lineage (archived 2026-07-03); modeling is the remaining gap.
- Reference Part Studio: doc `a2e3cbf0909c00279ce5d80b`, workspace Main `05605c0cafe63902d2a627a0`, Part Studio `dce1127c54f4f7f7a620dc26` (spike extrude JSON from live features list).

## Risks

| Risk | Likelihood | Mitigation |
|------|------------|------------|
| BT payload drift vs Onshape API | High | Golden JSON from CSWP; pin examples in tests |
| Parity scope creep (~83 Inventor tools) | Med | Matrix marks `declarative-composite` / `not_supported`; v1 = P0–P2 only |
| MCP latency on export poll | Med | Document blocking behavior; unchanged from provider change |
| Review budget / polluted diffs | High | Feature-branch-chain; one deliverable unit per PR; retarget if base wrong |

## Rollback

Revert child PRs in reverse order on the tracker chain. P0 tools are mostly additive; default is **replace stubs outright** (no feature flag unless apply introduces one).

## Dependencies

- Archived `onshape-provider` implementation in tree
- WireMock.Net (existing test harness)
- Onshape OpenAPI + CSWP feature JSON spike (apply slice 1–2)

## Success criteria

- [ ] P0 atomics listed in parity matrix as `implemented`
- [ ] WireMock tests pass for sketch replace + extrude POST bodies (valid BT shape)
- [ ] Optional live smoke: rectangle → extrude → STEP on sandbox
- [ ] No new MCP tool name collisions with Inventor tools in validator tests
- [ ] `create_sketch` / `add_extrude` documented deprecated or wired to real BT implementation; canonical path documented as `onshape_*`
- [ ] Each chained PR ≤400 changed lines unless maintainer `size:exception`

## Next step

Run **spec** phase: requirements per capability (`part-studio-modeling`, parity matrix, atomic catalog) and acceptance tests aligned to P0 E2E.