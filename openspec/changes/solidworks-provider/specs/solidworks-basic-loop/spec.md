# SolidWorks Provider — Basic Modeling Loop Implementation — Spec

**Change**: solidworks-provider
**Related**: generalization, pluggable-server
**Strict TDD**: Enabled (all new types: driver, provider, managers, helpers developed test-first with mocks + provider-agnostic contract tests; live SolidWorks only in verify phase)

## Purpose
Create the McpCad.SolidWorks project implementing the full IMechanicalCadProvider contract (via delegation) sufficient for a minimal viable modeling loop in SolidWorks. Inventor remains the complete reference implementation and default. Tagging/entity references stay string-based and provider-neutral. Infrastructure follows the detailed plan from prior artifact #272 (driver + managers + helpers) but scoped to the basic loop only.

## Scope (In for this Increment)
- New project McpCad.SolidWorks (csproj, added to solution).
- SolidWorksDriver: COM activation (ProgID "SldWorks.Application"), GetActiveObject/Create patterns, ModelDoc2 handling, lifetime management (Marshal.ReleaseComObject), health, version reporting, stale detection. Mirror proven InventorDriver patterns (P/Invoke for GetActiveObject, idempotent Connect, etc.).
- SolidWorksProvider: thin delegating implementation of IMechanicalCadProvider + ICadProvider. Constructor takes driver; instantiates the minimal set of managers.
- Minimal managers (in Managers/):
  - DocumentManager: DocNewPart, DocNewAssembly, DocOpen, DocSave, DocSaveAs, DocClose. Uses SldWorks + ModelDoc2 + appropriate document types (swDocPART=1, swDocASSEMBLY=2).
  - SketchManager: SketchCreate (on planes), SketchLine, SketchCircle (and minimal other sketch primitives needed by basic extrude flow), SketchProfiles (or equivalent to discover closed profiles), basic support for tags via provider-neutral string mechanism.
  - FeatureManager: At minimum Extrude (profile by index or tag, distance, direction, operation). Use Insert* or FeatureManager patterns after profile selection via SelectionManager / SelectByID2 / Mark.
  - InspectionManager: CaptureViewportImage (basic reliable path, e.g. via SaveAs or SW view capture to temp + Base64), GetFeatureTree (traverse FeatureManager or GetFeatureTree API or recursive model tree), GetBoundingBox (for whole model / body / tagged entity using GetBoundingBox or mass properties).
- Minimal Helpers/: Adaptations of tagging/selection for SW (different from Inventor AttributeSets + 1-based collections). Support @tag strings and simple numeric indices ("1", "1,2") for the entities used in the basic loop. TagStore may be generalized or SW-specific (e.g. using persistent IDs, Attribute or persistent reference storage). ComDispatchHelper-like utilities if dynamic dispatch needed.
- All public methods return Dictionary<string, object?> with "success": true + data or "success": false + "error".
- Use CadComException / CadConnectionException (not the obsolete Inventor* aliases in new code).
- Project references only McpCad.Core. Interop via HintPath (typical: C:\Program Files\SolidWorks Corp\SolidWorks (or 20xx)\api\redist\SolidWorks.Interop.sldworks.dll and siblings like SolidWorks.Interop.swconst.dll; dev-machine only, documented).
- Solution entry + Server reference (see pluggable spec).

## Requirements

### Requirement: Follow Existing Provider Pattern Exactly
SolidWorksProvider SHALL implement IMechanicalCadProvider in full (all methods). For methods beyond the minimal viable loop, it MAY throw CadComException("Not yet implemented for SolidWorks provider") or return a clear error dict via the normal path; the interface contract is satisfied.

#### Scenario: Provider contract satisfied (compile + basic runtime)
- GIVEN SolidWorksProvider registered via config
- WHEN any IMechanicalCadProvider method is called (via tool or direct)
- THEN the call reaches the provider (no MissingMethod or cast failure)
- AND for basic loop methods, real SW operation occurs or clear error

### Requirement: Minimal Viable SolidWorks Loop Succeeds
The following end-to-end flow SHALL work when SolidWorks is running and "Cad:Provider":"SolidWorks":
1. cad_connect (or inventor_connect alias) → success, version info.
2. doc_new_part() → success, new part active.
3. sketch_create(plane="XY") → success.
4. sketch_line(...) and/or sketch_circle(...) to create a closed profile → success, with optional tag.
5. sketch_profiles() → returns usable profile list (indices or descriptors).
6. extrude(profile="1", distance=10, ...) → success, feature created.
7. get_feature_tree() → returns structured tree including the new extrude feature.
8. capture_viewport_image(view="Iso") → returns {success:true, image_base64: "...", mime_type, ...} (and MCP content block where supported).
9. get_bounding_box() → returns min/max/center/size data.
10. doc_save_as(...) or doc_close() → success.

All steps use the neutral tool names post-generalization.

#### Scenario: Basic part creation + verification in SolidWorks
- GIVEN SolidWorks running, Cad:Provider = SolidWorks, cad_connect succeeded
- WHEN the sequence doc_new_part + sketch_create + sketch_line + sketch_circle + extrude + get_feature_tree + capture_viewport_image + get_bounding_box is executed
- THEN every step returns success=true (or equivalent data)
- AND get_feature_tree contains an entry for the extrusion
- AND capture_viewport_image contains a non-empty base64 png
- AND get_bounding_box contains numeric extents > 0
- AND the model is visible/usable in the running SolidWorks session

#### Scenario: Tagging works at string level (basic)
- GIVEN a sketch entity created with tag="@my_circle"
- WHEN extrude uses a profile that references the tagged geometry (or indices after profiles())
- THEN the feature is created using the intended geometry (no "selection failed" error for basic cases)

### Requirement: Tagging / Entity Reference Contract Remains Neutral
- The surface (parameters to sketch_*, extrude profile, etc.) remains string-based: "@name", "1", "1,2,3", "e1", etc.
- SolidWorks-specific resolution (SelectionManager, persistent IDs, SelectByID2 with mark, etc.) lives entirely inside McpCad.SolidWorks/Helpers and Managers.
- No changes to IMechanicalCadProvider signatures.
- For basic loop, full @tag propagation (e.g. TagFacesFromSketch) may be stubbed or minimally supported; advanced tagging is follow-up per #272.

#### Scenario: Index-based profile selection works cross-provider
- GIVEN a part with a single closed profile after sketch_profiles()
- WHEN extrude(profile="1", ...) is called on Inventor or on SolidWorks
- THEN both succeed and create the expected solid

### Requirement: Error Handling and Result Envelopes Consistent
- All SW code uses/throws CadConnectionException and CadComException.
- ToolHelpers.Catch (in Tools layer) catches them uniformly → {success:false, error: msg}.
- SW driver/manager errors produce actionable messages (e.g., "SolidWorks not running", "No active document", "Profile selection failed: ...").
- Health always safe (never throws).

#### Scenario: SolidWorks not installed/running
- GIVEN no SldWorks COM registered or no running instance
- WHEN cad_connect()
- THEN returns {connected:false, error: "SolidWorks is not installed or ..."} (or equivalent clear message)
- AND subsequent modeling calls surface connection error via the normal envelope

### Requirement: COM Discipline and Lifecycle
- Driver mirrors InventorDriver: P/Invoke for GetActiveObject (or equivalent), CLSIDFromProgID.
- Proper Marshal.ReleaseComObject on Disconnect and best-effort in finalizers / health stale detection (RPC_E_DISCONNECTED handling).
- Idempotent Connect/Health.
- Supports both GetActiveObject (attach to running) and launch if needed for skeleton (but prefer attach per common MCP-CAD usage).

#### Scenario: Multiple connect/disconnect cycles
- GIVEN repeated cad_connect / cad_disconnect / cad_health
- THEN no COM leaks or stale references crash the server or leave SolidWorks in bad state (best-effort, observable via health)

### Requirement: Build and Interop Reference
- McpCad.SolidWorks.csproj modeled on McpCad.Inventor.csproj (net8.0-windows, nullable, etc.).
- Reference to SolidWorks interop assemblies via HintPath (dev provides path; common locations documented in README or a SW-README.md in the project).
- No new NuGet beyond what Core/Tools require.
- dotnet build of solution succeeds; the project builds even if interop not present at compile time? (use <Private>false or conditional, but typically dev-only with the assemblies present).

### Requirement: Provider-Agnostic Contract Tests
- New or extended tests (in McpCad.Tests) SHALL verify that SolidWorksProvider (when constructible) satisfies IMechanicalCadProvider for the basic loop methods (using Moq for driver or integration with real when available).
- A provider-agnostic test base or theory can be used: "for any IMechanicalCadProvider, doc_new_part + sketch_create + ... returns success shape".
- Mocks updated/extended as needed (current MockInventorProvider can be renamed conceptually to MockCadProvider or kept; new tests prefer interface).

### Requirement: Documentation of SW-Specifics and Challenges
- Code comments in driver/managers reference the challenges from #272 (sw-01 etc. for COM, selection model differences, viewport capture reliability in SW vs Inventor SaveAs trick, etc.).
- Known limitations of the skeleton (advanced features not implemented, viewport capture may use SaveAs or basic capture and note TODOs) are documented.

## Acceptance Scenarios (GIVEN/WHEN/THEN)

#### Scenario: Config switch yields working basic loop on SolidWorks with same client calls
- GIVEN server launched with "Cad:Provider":"SolidWorks" (and SolidWorks running)
- WHEN client uses only cad_connect, doc_new_part, sketch_create, sketch_line, sketch_circle, sketch_profiles, extrude, get_feature_tree, capture_viewport_image, get_bounding_box
- THEN the full loop succeeds with useful verification data
- AND switching the config to "Inventor" and repeating the identical calls succeeds against Inventor (no client change)

#### Scenario: Legacy inventor_* names continue to drive SolidWorks when configured
- GIVEN "Cad:Provider":"SolidWorks"
- WHEN client (or old skill) calls inventor_connect + inventor_health + doc_new_part etc.
- THEN calls succeed (aliases delegate through the neutral surface to the active provider)

#### Scenario: No regression for default Inventor path
- GIVEN default config (or explicit Inventor)
- WHEN any existing test suite or client flow using either name set is executed
- THEN all prior Inventor behaviors and results are identical (bitwise where possible for deterministic ops)

#### Scenario: Provider-agnostic contract tests pass for both implementations
- GIVEN the test project references both McpCad.Inventor and McpCad.SolidWorks (or uses DI/config)
- WHEN provider-agnostic tests (mocked driver + contract assertions on the I* surface) are run for InventorProvider and for SolidWorksProvider
- THEN all basic loop methods are exercised and return correctly shaped dictionaries; errors are caught uniformly

#### Edge Case: Tagging/index differences between Inventor and SW
- GIVEN a flow that relies on "1" indices or @tags for profile selection in extrude
- WHEN executed on both providers
- THEN both succeed for the basic cases covered by the skeleton (differences in underlying selection are encapsulated)

#### Error Condition: Live CAD dependency for verification
- GIVEN SolidWorks not running when capture_viewport_image or get_feature_tree is called after connect failure
- THEN clear error envelope (not unhandled COM exception or crash)

## Non-Goals / Out of Scope (this increment)
- Full 80+ feature parity for SolidWorks (only the minimal loop listed).
- Complete rewrite of all skills or removal of inventor-named skills.
- Advanced tagging, assembly ops, complex selection, full viewport parity, param/property/export/work features, patterns, etc. in SW managers.
- Changes to core I* contracts.
- Installer updates or redist of SW interop (skeleton + docs only).
- KiCad or other providers.
- Simultaneous multi-backend in one process.

## Risks and How Addressed in Requirements
- **Tagging/selection model differences** (Inventor AttributeSets + static TagStore vs SW SelectionManager / persistent refs / Mark): Addressed by per-provider Helpers in McpCad.SolidWorks; contracts remain string-based; basic loop starts with simple indices + minimal @tag; advanced unification is later work. Explicit scenario for index/tag compatibility.
- **COM activation/lifetime differences**: Reuse exact patterns from InventorDriver (P/Invoke, release discipline, health probes); requirement for idempotent + safe health; documented in code per #272 challenges.
- **Viewport / feature tree / bbox reliability on SW**: Skeleton provides basic working impl (SaveAs trick or SW-native where reliable); clear "not yet" or partial results documented; verification tools exercised in acceptance; full parity later.
- **Live CAD dependency for TDD/verify**: Strict TDD mandates mocks + unit/contract tests first; live SolidWorks only in explicit verify phase (as current Inventor integration tests). Provider-agnostic tests runnable without either CAD.
- **Scope creep vs 400 LOC / review load**: Scoped explicitly to basic loop + infrastructure skeleton from #272; chained delivery (generalization first, then pluggable, then this impl); ask-on-risk for any live-CAD pieces.
- **Health response vendor keys and other strings**: Minimal generalization in tools layer; driver-specific keys acceptable for diagnostics in this increment.

## Protocol Clarifications (No Contract Changes)
- IMechanicalCadProvider and ICadProvider remain stable (no signature changes).
- Result envelope contract (Dictionary<string, object?> success/error) and exception hierarchy (Cad* with Inventor* obsolete aliases) are unchanged.
- Entity reference syntax (@tags, numeric indices) is part of the documented contract and stays provider-neutral at the MCP surface.

## References / Prior
- engram #272: Original detailed SolidWorks migration plan (driver + 9 managers + helpers, sw-01.. challenges, API notes for SldWorks/ModelDoc2/SketchManager/FeatureManager/SelectionManager, viewport, tagging).
- engram #275, #273, sdd-init #72, prior explore (contract analysis, risks).
- Proposal (this change).
- Existing: InventorDriver/InventorProvider/Manager/Helper structure, I* interfaces, ToolHelpers, ErrorResult, Mock*Provider, integration test patterns.
- cad-provider-protocol and weld-feature specs (precedent for agnostic + stable contracts).

## Success Criteria
- Clean build of solution with new project.
- Provider-agnostic + SW-specific tests (mock-first) all pass.
- With SolidWorks running + correct config: basic loop produces verifiable model (feature tree + image + bbox useful to LLM).
- Inventor default path shows zero regression (all prior tests + manual flows pass).
- Aliases + new names both work; docs updated.
- All SDD artifacts (this spec, design, tasks) complete per envelope contract.
