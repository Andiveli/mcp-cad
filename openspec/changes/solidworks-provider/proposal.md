# Proposal: SolidWorks Provider (Provider-Agnostic Generalization)

**Change name**: solidworks-provider

## Intent / Problem

User directive: pause deep Inventor focus ("pausamos el proyecto para Inventor") and bring up SolidWorks support while keeping the provider-agnostic vision alive. The locked decision ("Seguimos con la idea de provider agnostico, renombra para generalizar") commits to generalizing the public MCP surface instead of freezing inventor_* names and duplicating an entire new tool surface for SolidWorks.

Problem: The current public surface (especially connection tools) and server wiring are Inventor-hardcoded in names/descriptions/config despite the original design (IMechanicalCadProvider, ICadProvider, provider pattern in Core) being intentionally multi-CAD from sdd-init. This blocks adding McpCad.SolidWorks as a first-class peer without either (a) polluting the surface with vendor prefixes forever or (b) expensive duplication.

Who benefits: SolidWorks users (large installed base), any agent workflows wanting CAD engine choice, long-term project health (avoids lock-in and duplicated maintenance).

"Done" looks like: config selects active backend; tools are CAD-neutral (single surface); basic SolidWorks loop works (connect + new part + sketch + simple feature + verification); Inventor remains fully functional as reference impl; backward compat protects existing skills/prompts; Strict TDD followed throughout.

## Goals and Success Criteria

- Public MCP surface generalized and neutral: connection tools renamed (e.g. `cad_connect`, `cad_disconnect`, `cad_health`); descriptions neutral; all other 80+ tools already mostly neutral stay unchanged.
- Single set of tools work for the active backend via `IMechanicalCadProvider`.
- Pluggable, config-driven provider selection in McpCad.Server.
- McpCad.SolidWorks skeleton + driver + provider + core managers sufficient for a basic modeling loop.
- Existing users/skills continue to work (via config for backend; deprecated aliases for old connection names during transition).
- Docs, skills, README, tools-reference updated to present the agnostic model and provide migration guidance.
- Strict TDD enabled and followed (tests first for generalization and new SW code).
- Clean build, no regression for Inventor path, basic SW success path verified with live SolidWorks.

Success: Agent using neutral `cad_*` (or legacy aliases) + "Cad:Provider": "SolidWorks" can create a simple part in SolidWorks and verify with `get_feature_tree` / `capture_viewport_image`. Inventor path unchanged when selected. All SDD artifacts complete.

## Scope (In)

- Generalization of public surface: rename the three `inventor_*` connection tools in AtomicTools (and any references); make descriptions CAD-neutral ("the running CAD application"); update any other vendor-specific strings in the tools layer.
- Backward-compat aliases: keep `inventor_connect` etc. as deprecated thin wrappers (or dual registration) so old calls continue to function.
- Pluggable mechanism: update McpCad.Server (Program.cs DI, configuration) to select InventorProvider vs. SolidWorksProvider based on config (e.g. `Cad:Provider`). Support legacy `Inventor:AutoConnect` for transition.
- McpCad.SolidWorks new project: skeleton following Inventor structure (SolidWorksDriver for COM/SldWorks lifecycle; SolidWorksProvider delegating to managers; at minimum DocumentManager, SketchManager, FeatureManager + minimal helpers to support connect, doc_new_part, sketch_create/line/circle, extrude or equivalent basic feature, health, inspection basics).
- Updates: exceptions (already generalized to CadComException / CadConnectionException with obsolete Inventor* aliases — leverage), appsettings.json, *.csproj (new project + Server reference + solution), build configuration.
- Backward compat strategy + migration docs.
- Documentation / skill surface updates (README, docs/tools-reference.md, existing skills/*, any hard-coded references).
- Record and enforce Strict TDD for the change.
- Base the SolidWorks driver/managers/helpers work on the detailed infrastructure from prior artifact #272 (driver + 9 managers + tagging/selection/COM helpers; track challenges sw-01..).

## Scope (Out / Non-Goals)

- Full parity port of all 80+ features in this increment. Only a minimal viable basic loop (connect + documents + sketch basics + 1-2 features + verification tools). Full implementation is large; phased follow-ups.
- Complete rewrite or replacement of skills (update examples gradually; legacy inventor-named skills may remain with notes).
- Changes to the core IMechanicalCadProvider / ICadProvider contracts (keep stable; no or minimal additions).
- Simultaneous multi-CAD backends in one server process (config picks exactly one active provider).
- KiCad or other providers.
- Full installer / packaging / redistribution support for SolidWorks interop assemblies (skeleton sufficient).
- Advanced tagging/resolution, assembly features, viewport parity, error model extensions, etc. beyond what is required for the basic loop.
- Deletion or freezing of McpCad.Inventor code (it remains the reference and default).

## High-level Approach

Leverage the existing provider-agnostic contracts (`IMechanicalCadProvider : ICadProvider`) designed from the start for this (per sdd-init / cad-provider-protocol). No major interface changes expected.

1. **Generalize tool layer first (or parallel)**: In `AtomicTools.cs` (and SkillTools/Macro if needed) rename the three connection methods to neutral `cad_connect` etc. Update `[Description]`. Add deprecated alias methods that delegate to the same provider calls (or call the new method) so old names remain callable. Update internal tests/docs to use new names preferentially.

2. **Server wiring & config**: In `McpCad.Server/Program.cs` and DI, read provider selection from `IConfiguration` (new `Cad:Provider` key defaulting to "Inventor"). Conditionally register the concrete driver + provider (keep `AddSingleton<InventorDriver>()` etc. behind the flag). Update `appsettings.json` with documented `Cad` section. Auto-connect logic becomes provider-aware. Keep Inventor default so nothing breaks.

3. **SolidWorks implementation (skeleton + basic loop)**: New `McpCad.SolidWorks` project modeled exactly on `McpCad.Inventor`:
   - `SolidWorksDriver.cs`: COM activation (ProgID "SldWorks.Application"), GetActiveObject patterns (or Create), ModelDoc2 handling, lifetime, version, health. Mirror InventorDriver structure.
   - `SolidWorksProvider.cs`: holds driver + managers, delegates every interface method (identical signatures).
   - `Managers/`: DocumentManager (NewPart/Assembly using SldWorks, ActiveDoc cast to ModelDoc2, Save etc.), SketchManager (sketch creation, 2D entities via SketchManager or ModelDoc2 APIs), FeatureManager (at least basic extrude/revolve or Insert*Feature after profile selection).
   - `Helpers/`: Adapt or replicate minimal TagStore + selection resolvers. Note: SolidWorks selection model (SelectionManager, SelectByID2, persistent IDs, Mark, etc.) differs significantly from Inventor's index + AttributeSet tagging. For skeleton, support basic unnamed geometry + simple tags; generalize or per-provider store as needed in follow-ups. Use ComDispatchHelper patterns if dynamic required.
   - Reference SolidWorks interop assemblies via HintPath (analogous to Inventor csproj; dev machine has SW installed).

4. **Tagging / selection / interop**: Will require SolidWorks-specific helpers (different from Inventor FaceResolver/EdgeResolver/TagStore). Keep surface contracts string-based (@tags, "1,2,3" indices, etc.) so tool layer unchanged.

5. **Config & selection**: `"Cad:Provider": "Inventor" | "SolidWorks"`. Legacy `Inventor` section honored for transition.

6. **Keep Inventor pristine**: All existing Inventor code, managers, and behavior untouched except where shared generalization (e.g. exception names already done) helps.

7. **Strict TDD**: Tests written before (or with) implementation. Provider-agnostic contract tests + mock-based unit tests for new SW types. Live SolidWorks only in verify phase.

Base detailed tasks/challenges on the full original plan (engram #272).

## Risks and Mitigations

- **Hardcoded inventor_* surface**: Only three tools. Mitigated by aliases + docs. (Confirmed in explore analysis of full contract and tool registration.)
- **Tag / selection model differences**: Inventor AttributeSets + static TagStore + 1-based collections vs. SW SelectionManager / persistent refs / different indexing. Mitigation: per-provider helpers in McpCad.SolidWorks/Helpers; keep contracts in I* neutral. Start skeleton with basic (no heavy @tag reliance) and extend. Risk of duplication until a common abstraction emerges.
- **COM lifetime & activation**: Different ProgID, possible instance semantics, add-in interactions, Marshal/Release discipline. Mitigation: reuse proven InventorDriver patterns (P/Invoke, idempotent connect, health probe, stale detection). Requires live SW on dev.
- **Viewport / feature tree / inspection**: `capture_viewport_image`, `get_feature_tree`, `get_bounding_box` have CAD-specific implementations. Skeleton provides basic or throws clear "not yet for this provider"; full parity later.
- **Installer / packaging complexity**: Current portable + TUI copies Inventor interop. SW adds similar but different assemblies + possible license/runtime considerations. Out of this scope; document as follow-up.
- **Dual-support duration & user migration**: Skills/prompts and agent memory using old names. Mitigation: deprecated aliases (strong) + migration guide + update key skills/docs. Remove aliases only after transition window.
- **Strict TDD with live CAD**: Flaky or env-dependent integration. Mitigation: heavy use of mocks for unit/contract tests; isolate driver; only exercise real SW when explicitly verifying. Record in state.
- **Scope / review load vs 400 LOC budget**: New project + wiring + generalization + partial impl is substantial. Mitigation: phased/chained delivery (see below); small focused PRs; ask-on-risk where needed. Reference prior successful SDD changes (weld, template).
- Other from explore/#272: COM interop reliability, selection edge cases, hybrid engram+openspec tracking.

## Backward Compatibility & Migration Story

- Vast majority of tools (sketch_*, doc_*, extrude, revolve, asm_*, param_*, iproperty_*, export_*, work_*, inspection, skills, macros) are already neutral.
- Only connection surface (`inventor_connect` / `inventor_disconnect` / `inventor_health`) changes.
- Implementation: primary methods become `cad_connect` etc.; old names remain as `[Obsolete]` public methods in AtomicTools that delegate to the provider (identical behavior). Old calls succeed exactly as before (with possible deprecation note in future).
- Config: legacy `Inventor:AutoConnect` continues to work (mapped or dual-read).
- For users/skills: no immediate breakage. Recommend (and document) updating to `cad_connect` etc. for new work. Old names supported for a transition period (e.g. until next major or N months).
- Skills repo: existing `inventor-new-part` etc. can stay (or be aliased); new neutral-named skills can be added in parallel.
- Docs: prominent "Migration from inventor_*" section in README and tools-reference. Installer will deliver updated guidance on next run.
- Goal: zero (or near-zero) user-visible breakage during rename. Agents using old prompts continue working when Inventor is selected.

## Delivery Strategy Considerations

Size is non-trivial (new csproj + ~10-20 source files for skeleton + touches across Tools/Server/Core/docs/tests + csprojs). Respect 400-line changed budget and review workload protection.

Recommended: chained / incremental delivery rather than one giant PR.
- Phase A (small): Surface generalization + aliases + doc updates (low risk, protects users immediately).
- Phase B: Server pluggability + config + DI (small, high value, Inventor default preserved).
- Phase C: Create McpCad.SolidWorks skeleton + basic driver/provider + minimal managers sufficient for connect + simple modeling loop (core of the change).
- Later chained changes: flesh out remaining managers per #272 plan, advanced helpers, full parity, installer updates, skill refreshes.

Use `ask-on-risk` for any piece that risks the budget or requires live CAD feedback. Hybrid artifact store (engram primary + openspec/changes/solidworks-provider/*). Update `sdd/solidworks-provider/state` after each.

## Dependencies / Prerequisites

- SolidWorks installed on the development machine for apply/verify phases (equivalent to "Inventor running" requirement for prior work). Skeleton code can be authored without it; real behavior and COM signatures require it.
- SolidWorks interop assemblies (SolidWorks.Interop.sldworks, etc.) — referenced via HintPath in the new csproj, paths from a standard SW install (e.g. 202x Bin/Public Assemblies or API redist).
- No new external NuGet packages (COM interop only, matching Inventor pattern).
- Existing solution, .NET 8 Windows, MCP hosting, test infrastructure.
- Access to prior artifacts (#272 detailed plan, decision #275, state #273, explore output, sdd-init #72) for detailed manager/API mapping.

## Rollback / Pause Considerations

- Inventor code is untouched (beyond any shared generalization already done for exceptions). Default remains Inventor. User can continue or resume pure Inventor work at any time.
- Rollback path: revert rename (restore inventor_* primacy or keep both), drop SW project reference from Server, revert config to Inventor-only. Aliases mean even post-rename users on legacy names are protected short-term.
- If SolidWorks is unavailable: generalization + pluggable wiring deliver value independently (can ship a "SolidWorksProvider" that throws clear "Provider not yet fully implemented" for advanced methods while basic connection works or is stubbed).
- Pause after proposal/spec is acceptable; the decision record and this proposal capture the committed direction.

## Strict TDD

**Strict TDD is enabled for this entire change** (explicit override of project-level default per user directive and state #273). This must be recorded in all downstream artifacts (spec, design, tasks) and enforced:
- Write (or update) tests before or alongside the production change for generalization (aliases, renamed tool behavior, config selection).
- New SolidWorks types developed test-first (mocks for driver/provider interactions; contract tests against IMechanicalCadProvider that are provider-agnostic).
- Existing tests continue to pass for Inventor path (mocks updated only as needed for neutrality).
- Integration / live-CAD exercising happens in verify phase.

## References / Prior Artifacts

- engram #272: Full original "[SolidWorks] Backend Migration Plan" (requirements, infrastructure for driver+9 managers+helpers, challenges sw-01 to sw-08, detailed API notes).
- engram #275: Decision record "sdd/solidworks-provider/decision-provider-agnostic" (locked: provider-agnostic + rename to generalize).
- engram #273: Current sdd/solidworks-provider/state (includes Strict TDD flag and this decision).
- engram #72: sdd-init/mcp-cad (confirms provider pattern was designed from day one to support SolidWorks/KiCad; hybrid engram+openspec).
- Prior sdd-explore result for solidworks-provider (full contract analysis, Inventor impl structure, hardcoded surface, tool registration, server wiring, interop, tag system, risks around selection/tagging/COM/viewport, etc.).
- Existing patterns: weld-feature, template-system, cad-provider-protocol spec, current I* interfaces, AtomicTools delegation, InventorProvider/Driver/Manager structure, generalized exceptions.

---

**End of Proposal**