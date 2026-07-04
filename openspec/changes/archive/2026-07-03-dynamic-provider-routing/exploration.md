# Exploration: dynamic-provider-routing

**Change**: `dynamic-provider-routing`  
**Date**: 2026-07-03  
**Store**: OpenSpec (+ Engram topic `sdd/dynamic-provider-routing/explore` when available)

## User intent (product)

1. **End users must not use terminals** — clone, `dotnet publish`, `user-secrets`, and branch-specific READMEs are unacceptable for the target audience.
2. **Single user config file** — e.g. `~/.config/mcp-cad/config.json` (Linux) and `%APPDATA%\mcp-cad\config.json` (Windows). Not duplicated `scripts/tui/state.json` + server `appsettings.json` + env vars as the primary story.
3. **Installer TUI** — extend `McpCad.Installer` (already `net8.0` + Spectre.Console) with `SelectionPrompt`-style flows: pick CAD (Inventor / SolidWorks / Onshape), optional detection on Windows, Onshape API keys, register MCP agents (OpenCode, Claude, Pi, VS Code).
4. **Cross-platform installer** — same TUI on Linux and Windows; fix server discovery (today hardcodes `McpCad.Server.exe`; Linux needs `McpCad.Server` or `dotnet` + published path).
5. **Agent skill (project start)** — when the user asks to create/model something **without** naming a provider, the agent runs an ask-style menu (Inventor / SolidWorks / Onshape), persists choice to config/session, then continues with MCP tools.
6. **North star: Option B — dynamic provider routing per tool call** — one MCP server process can route operations to Inventor, SolidWorks, or Onshape without restart. This **supersedes** the onshape-provider proposal out-of-scope item "Multi-provider server".

## Current state (verified)

### Server bootstrap (branch `feature/onshape-provider`)

- `ProviderBootstrap.ResolveActiveProvider` reads `Provider:Active` (default `inventor`).
- **Single provider per process**: DI registers one backend + one tool set (`AtomicTools`/`SkillTools` vs `OnshapeAtomicTools`/`OnshapeSkillTools`).
- Linux: `inventor` throws `ConfigurationException`; `onshape` works.
- Credentials: Onshape from config/env; Inventor COM on Windows with `Inventor:AutoConnect`.

### Remote SolidWorks branch (`feature/solidworks-provider`)

- Uses **`Cad:Provider`** (`Inventor` | `SolidWorks`), not `Provider:Active`.
- SolidWorks loaded via reflection when assembly present.
- **Config schema divergence** must be unified in this change.

### Installer (`McpCad.Installer`)

- `net8.0`, Spectre.Console — **runs on Linux** (build verified).
- UX: agent list only; subtitle "Autodesk Inventor"; no provider wizard, no unified config, no secrets.
- `FindServerPath()` → `dist/mcp-cad/McpCad.Server.exe` only.
- `ConfigManager` merges MCP entries into agent JSON files; `State` in `scripts/tui/state.json`.

### Interfaces

- `ICadProvider` — connection, documents, export (13 methods).
- `IMechanicalCadProvider : ICadProvider` — sketch, features, assembly, etc. (Inventor, future SW).
- `ICadWebProvider : ICadProvider` — web CAD (Onshape); sibling, not mechanical.
- Onshape mode: `NullMechanicalCadProvider` for mechanical surface.

### Prior SDD (`onshape-provider`)

- Explicit **out of scope**: multi-provider server, async Task tools.
- This change **intentionally** revisits that boundary for product reasons.

## Approaches considered

| Approach | Summary | Pros | Cons |
| ---------- | --------- | ------ | ------ |
| **A — Setup only** | Unified config + TUI + skill; keep single `Provider:Active` at startup | Small, shippable fast | User must re-run installer to switch CAD; skill only helps before boot |
| **B — Router (target)** | `IProviderRouter` / execution context; unified or parametric tools; lazy connect per backend | Matches dynamic product story; one MCP registration | Large; COM+HTTP same process; tool naming unification; SW branch merge |
| **C — Multi-process** | One MCP server executable per provider | Isolation | 3x agent config, worse UX, not single mcp-cad |

**Recommendation**: Deliver **A in early slices** (config + TUI + skill) without blocking **B** design. Implement **B** as the architectural spine in design/spec; apply in chained PRs after config contract is frozen.

## Phase split (delivery)

### Phase 1 — End-user setup (no router yet)

- Config schema + `UserConfigStore` read/write (cross-platform paths).
- Installer wizard: provider → credentials/detection → agents → write config + point server at config.
- Server: load unified config (fallback: existing `appsettings.json` + `Provider:Active` for migration).
- Skill `mcp-cad-select-provider`: trigger on modeling requests without provider keyword.
- Linux: Onshape-only menu by default; desktop CAD options disabled with clear copy.

**Rough size**: ~600–900 LOC — chained PRs.

### Phase 2 — Dynamic routing (Option B)

- Unify config: `active_provider` + per-provider sections.
- Register all available providers in DI (lazy connect).
- Router layer: `CadExecutionContext` per tool call.
- Merge SolidWorks branch onto unified bootstrap.

**Rough size**: ~1,000–1,400 LOC — feature-branch-chain.

## Risks

| Risk | Mitigation |
| ------ | ------------ |
| COM crash kills whole MCP | Document v1; future out-of-proc |
| Config migration | user config → appsettings fallback |
| SW config divergence | single schema in design |
| Tool collisions | router + naming policy |
| Review budget | chained PRs |

## Open decisions (proposal round)

1. Default provider when multiple CADs — last used vs ask each session?
2. Skill persistence — session only vs update `config.json`?
3. SolidWorks in Phase 1 wizard or Phase 2 only?
4. Ship published setup binary in releases?

## Next recommended phase

`sdd-propose`
