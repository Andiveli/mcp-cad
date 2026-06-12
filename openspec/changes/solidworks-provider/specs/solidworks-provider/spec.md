# SolidWorks Provider (Provider-Agnostic Generalization) — Full Specification

**Change name**: solidworks-provider
**Status**: Spec complete (based on locked proposal + prior artifacts + codebase analysis)
**Strict TDD**: Explicitly enabled for entire change (override). All requirements and acceptance criteria mandate tests written first / in lockstep. Live CAD exercising restricted to verify phase.

This document consolidates the detailed requirements. Sub-specs under specs/{area}/spec.md provide focused slices.

## Intent (from Proposal)
Generalize the public MCP surface (rename the 3 inventor_* connection tools to neutral cad_*, update descriptions and any vendor strings in tools layer) while keeping a **single set of tools**. Make the server pluggable via config ("Cad:Provider"). Add McpCad.SolidWorks as the second concrete IMechanicalCadProvider implementation. Inventor remains fully functional reference + default. Use existing contracts without major changes. Backward compat via deprecated aliases. Strict TDD throughout. Base SW skeleton on engram #272 infrastructure (driver + managers + helpers) scoped to minimal viable loop.

## Overall Requirements

### 1. Generalization of Public Surface (see specs/generalization/spec.md)
- Rename in AtomicTools: inventor_connect → cad_connect (primary), similarly for disconnect/health.
- Neutral descriptions: "Connect to the running CAD application." etc. (mentions "Inventor, SolidWorks, etc." acceptable in some docs).
- Add [Obsolete] public alias methods that delegate (identical behavior).
- Update tests (ToolRegistration, AtomicToolsTests, DI tests) to cover both, prefer neutral.
- Update README.md (root) + docs/tools-reference.md + migration guidance.
- Update skills gradually (notes allowed; no full rewrite required in this increment).

**Acceptance (G/W/T + TDD)**:
- GIVEN post-generalization build
- WHEN tools are enumerated or old prompts executed
- THEN cad_* are the registered primary tools with neutral desc; inventor_* aliases succeed exactly (Obsolete present); tests asserting attributes/delegation pass (written before edit).

Edge: Alias calls work even when SolidWorks provider is active.

### 2. Pluggable Server + Config (see specs/pluggable-server/spec.md)
- "Cad:Provider" (default "Inventor") in IConfiguration selects the singleton registration of Driver + Provider impl.
- Legacy "Inventor:AutoConnect" honored for transition (provider-aware logic).
- Program.cs DI and startup auto-connect updated to be conditional/provider-aware.
- Clear error for unknown provider value.
- Server.csproj + .sln updated (ref to new SW project).
- appsettings.json updated with example Cad section + comments.

**Acceptance (G/W/T + TDD)**:
- GIVEN host built with mocked config "Cad:Provider":"SolidWorks"
- WHEN resolved IMechanicalCadProvider type inspected
- THEN SolidWorksProvider (or mock equivalent) active; same for "Inventor"; legacy AutoConnect path exercised only for Inventor default.
- Provider-agnostic connection tests pass regardless of concrete.

No simultaneous multi-provider.

### 3. McpCad.SolidWorks Implementation + Basic Loop (see specs/solidworks-basic-loop/spec.md)
- New project following Inventor structure exactly (Driver, Provider delegating to Managers, Helpers).
- Driver: ProgID "SldWorks.Application", P/Invoke GetActiveObject patterns (as in web sources + #272), health, COM release, idempotent.
- Provider + at minimum: DocumentManager (full doc_*), SketchManager (create/line/circle/profiles + basics), FeatureManager (extrude at minimum), InspectionManager (capture_viewport_image, get_feature_tree, get_bounding_box).
- Helpers: SW-specific selection/tagging (SelectionManager etc.) but surface strings (@tag, indices) unchanged and neutral.
- All methods return standard Dictionary<string, object?> success/error envelopes.
- Use CadComException / CadConnectionException (leverage existing generalized exceptions; no new contract changes).
- Interop HintPath (typical "C:\Program Files\SOLIDWORKS Corp\SOLIDWORKS\api\redist\SolidWorks.Interop.sldworks.dll" + swconst).
- Minimal loop verifiable end-to-end with neutral tools when configured.

**Acceptance (G/W/T + TDD)**:
- GIVEN SolidWorks running + "Cad:Provider":"SolidWorks"
- WHEN cad_connect + doc_new_part + sketch_create("XY") + sketch_line + sketch_circle + sketch_profiles + extrude(profile="1", distance=5) + get_feature_tree + capture_viewport_image + get_bounding_box + doc_close
- THEN every step succeeds with "success":true (or data); feature tree shows the extrusion; image_base64 present and non-empty; bbox has positive extents. Model appears in running SW.
- GIVEN same sequence with Inventor config (default)
- THEN identical client calls succeed with no regression.
- GIVEN provider-agnostic contract tests (Moq driver + interface assertions)
- THEN both InventorProvider and SolidWorksProvider (when available) pass the same test suite for the loop methods.
- All new SW code has unit tests (mocks first) before impl; live only verify.

Edge/error:
- No SW running → connect returns connected:false + clear error; modeling ops surface via Catch.
- Invalid profile selection → clear error dict (not crash).
- COM stale → health detects and recovers or reports safely.
- Tagging/index resolution for basic cases works cross-provider via string contract.

### 4. Contracts, Errors, Tagging, Build (cross-cutting)
- IMechanicalCadProvider / ICadProvider: **stable** (no changes required).
- Result envelopes and ToolHelpers.Catch: unchanged.
- Tagging: string-based (@tags, indices) provider-neutral at surface.
- Solution: add McpCad.SolidWorks project; Server refs it + Core/Inventor unchanged.
- Build on dev (with interop) and non-dev/CI (skeleton via conditions) succeeds for the provider sources.
- Existing generalized exceptions (Cad* + [Obsolete] Inventor* aliases) leveraged.

### 5. Documentation, Skills, State
- README, docs/tools-reference.md, appsettings: agnostic model + migration notes.
- Skills: examples updated or noted (legacy ok short-term).
- sdd state updated (engram).

### 6. Strict TDD (enforced in all above)
- Generalization changes: tests first for names, aliases, attributes, delegation, registration.
- Pluggability: DI/config host tests, type resolution, auto-connect, legacy keys.
- SW impl: mock driver/provider tests, contract tests against I*, per-method unit tests before real COM logic.
- Existing tests (Inventor path, ToolRegistration, integration) must continue passing.
- No production change merged without its tests green.
- Live SolidWorks exercising: verify phase only (as with current Inventor integration tests).

## Non-Goals (from Proposal, explicit)
- Full parity (only connect/health/docs/sketch basics + 1 feature + 3 inspection tools for SW).
- Complete skill or prompt overhaul.
- Core interface changes.
- Multi-CAD simultaneous.
- Other providers (KiCad etc.).
- Installer/packaging for SW interop.
- Advanced selection/tagging/viewport parity.

## Risks + Mitigations (reflected in requirements)
- Hardcoded surface: only 3 tools → aliases + docs.
- Tag/selection diffs (Inventor vs SW): encapsulated in Helpers; string contract stable; basic loop scoped; #272 challenges documented.
- COM: proven patterns reused; health discipline required.
- Viewport/inspection: basic impl + clear partials; verification exercised.
- Dual support duration: aliases + migration section.
- TDD + live CAD: heavy mocks + contract tests; verify isolated.
- Scope/LOC: explicit minimal loop + chained delivery per proposal.
- Health keys / other strings: minimal required generalization.

## Backward Compatibility Strategy
- 80+ tools already neutral.
- Only connection surface changed (with aliases).
- Config default + legacy AutoConnect = zero breakage for existing users/skills on Inventor.
- Aliases remain for transition window (documented; removal later).

## Delivery Notes
Chained / incremental recommended (generalization → pluggable wiring → SW skeleton). Respect review budget. Hybrid artifacts (engram primary + these openspec files). Update sdd/solidworks-provider/state.

## References (MUST)
- Proposal (openspec/changes/solidworks-provider/proposal.md)
- engram #272 (full SW plan), #275 (agnostic decision), #273 (state incl. Strict TDD), sdd-init #72, prior explore.
- Codebase: IMechanicalCadProvider.cs, ICadProvider.cs, AtomicTools.cs, Program.cs, Inventor* (pattern), ToolHelpers, ErrorResult, mocks, tests, csprojs, docs, README.
- Other specs: cad-provider-protocol, weld-feature (agnostic precedent).

**End of Consolidated Spec**
