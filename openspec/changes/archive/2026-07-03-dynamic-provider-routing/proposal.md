# Proposal: dynamic-provider-routing

**Change**: `dynamic-provider-routing`  
**Date**: 2026-07-03  
**Store**: OpenSpec (`openspec/changes/dynamic-provider-routing/proposal.md`) + Engram (`sdd/dynamic-provider-routing/proposal`)

---

## Intent

mcp-cad binds **one provider per process** at startup via `Provider:Active` — if you want Onshape, you restart; if you want SolidWorks, you reinstall. The prior onshape-provider proposal explicitly excluded multi-provider. This change **supersedes** that boundary for product reasons.

End users should:

- Install once with a **cross-platform TUI** that detects or configures their CAD(s).
- Use a **single config file** — not env vars + user-secrets + state.json split across the project.
- Switch CAD provider **per MCP tool call** without restarting the server or re-registering agents.
- Get **ask-style skill guidance** when they say "create a part" without naming a provider.

**North star: Option B — dynamic router** (one process, N backends, execution context per tool call). Phase 1 delivers the end-user setup pain relief first; Phase 2 delivers the router.

---

## Scope

### In Scope

- **Config schema + `UserConfigStore`**: cross-platform paths, JSON schema, read/write with atomic save. Single source of truth for provider selection, credentials, and agent registration.
- **Installer wizard (Phase 1)**: Spectre.Console TUI that steps through provider pick → credential entry → agent registration → config write. Cross-platform (Linux Onshape-first, Windows all providers). Server auto-discovers config.
- **Server config loading (Phase 1)**: `UserConfigStore` becomes primary config source; `appsettings.json` + `Provider:Active` become fallback for migration (backward compat during transition).
- **`mcp-cad-select-provider` skill (Phase 1)**: triggered on modeling requests without a provider keyword. Prompts user (Inventor / SolidWorks / Onshape), persists choice to session or config, then routes the tool call.
- **IProviderRouter (Phase 2)**: execution context per tool call. Resolves which registered backend to dispatch to based on config, skill selection, or session state.
- **Lazy provider registration (Phase 2)**: DI registers all backends at startup; each connects on first use. No crash if Inventor COM isn't available — the router returns a clear error for the inactive provider.
- **Tool naming unification (Phase 2)**: mechanical/atomic tools share one registration with a router-aware factory. `OnshapeAtomicTools` and `AtomicTools` converge behind the router.
- **SolidWorks branch merge (Phase 2)**: resolve `Cad:Provider` vs `Provider:Active` divergence; SolidWorks registers as a `IMechanicalCadProvider` alongside Inventor.

### Out of Scope

- Out-of-process COM isolation (documented risk, future concern).
- Async `Task<>` tool signatures (can be layered on top later; not blocked).
- Fusion 360 / 3DExperience / KiCad providers (router pattern doesn't preclude them, but no implementation here).
- Web UI dashboard or GUI installer — TUI only.
- Migration of existing per-branch config files to the unified schema (server reads both with fallback; manual migration first).

---

## Capabilities

### New Capabilities

| Capability | Description | Phase |
| --- | --- | --- |
| `user-config-store` | Cross-platform JSON config read/write with atomic save, default values, and schema validation. | P1 |
| `installer-wizard` | Spectre.Console wizard: provider pick → credentials → agents. Writes unified config. | P1 |
| `server-config-loader` | Server reads unified config first; falls back to `appsettings.json` + `Provider:Active`. | P1 |
| `select-provider-skill` | Agent skill that asks which CAD when no provider is named. | P1 |
| `provider-router` | `IProviderRouter` with `CadExecutionContext` — dispatches tool calls to the right backend per call. | P2 |
| `lazy-provider-registry` | All providers registered at DI startup; each connects lazily on first use. | P2 |

### Modified Capabilities

| Capability | Description | Phase |
| --- | --- | --- |
| `ProviderBootstrap` | From single-provider switch to multi-registration + routing awareness. | P2 |
| `McpCad.Installer` | From agent-list-only TUI to full provider wizard. Cross-platform server discovery. | P1 |
| `McpCad.Tools.*` | Tool classes unified behind router; `OnshapeAtomicTools` and `AtomicTools` converge. | P2 |
| Provider interfaces | SolidWorks registers as `IMechanicalCadProvider`; `NullMechanicalCadProvider` follows the router contract. | P2 |

---

## Approach

### Architecture Decision: UserConfigStore + IProviderRouter not IProviderRouter alone

The end-user setup pain (no config, no installer, terminal-only) is the blocker today. The router is the long-term value. We deliver **Phase 1** first as a standalone shippable improvement that doesn't require the router. Phase 1's config schema **is the contract** Phase 2 routes against. This lets us ship value early without blocking on the harder routing design.

### Phase 1 — End-User Setup (chained PRs, ~600–900 LOC)

**Config schema (`McpCad.Core`)**:

```
~/ .config/mcp-cad/config.json
{
  "active_provider": "onshape",         // last-used; skill can change
  "providers": {
    "onshape": {
      "enabled": true,
      "credentials": { "access_key": "...", "secret_key": "..." }
    },
    "inventor": {
      "enabled": false                   // greyed on Linux
    }
  },
  "agents": [
    { "name": "OpenCode", "url": "http://localhost:51777" }
  ]
}
```

- `UserConfigStore` class in `McpCad.Core` (zero deps). Cross-platform paths via `Environment.SpecialFolder`.
- Atomic write: temp file + `File.Replace`. Schema version field for future migration.

**Installer wizard (`McpCad.Installer`)**:

- `SelectionPrompt` for provider (Onshape only on Linux; Inventor/SolidWorks/Onshape on Windows).
- Onshape: prompt Access Key, Secret Key, optional Secret Key ID.
- Windows Inventor: optional auto-detect COM + `AutoConnect` toggle.
- Agent registration: same multi-select list as today, but writes to unified config instead of `scripts/tui/state.json`.
- Server discovery: fix `FindServerPath()` — try `McpCad.Server` (no `.exe`) on Linux; search publish directory.
- On completion: write config + print "mcp-cad configured. Restart your MCP host."

**Server config loading (`McpCad.Server`)**:

- At startup: try `UserConfigStore.Load()`. If found, use `active_provider` → DI branch (same as today's switch, but from config file).
- If not found: fall back to `Provider:Active` from `appsettings.json` for migration compat.
- `Provider:Active` still works for existing users until they run the installer.

**Skill `mcp-cad-select-provider`**:

- Agent skill file + MCP tool `mcp_cad_select_provider`.
- Triggers on keywords: "create", "make", "model", "design", but NO "inventor", "onshape", "solidworks".
- Prompts: "Which CAD should I use? 1) Inventor 2) SolidWorks 3) Onshape"
- Persists choice to session (config file optional — see open decisions).
- Session-scoped routing: the server tool stores selected provider in an `AsyncLocal<CadExecutionContext>`, so the next tool call dispatches there.

**Linux constraints**:

- Wizard shows Inventor + SolidWorks as disabled entries with "(Windows only)" suffix.
- Server `ProviderBootstrap` still throws for `inventor`/`solidworks` on Linux (no change).

### Phase 2 — Dynamic Routing (chained PRs, ~1,000–1,400 LOC)

**`IProviderRouter` interface**:

```csharp
public interface IProviderRouter
{
    string CurrentProvider { get; }
    IDisposable PushProvider(string provider);  // scope per tool call
    ICadProvider Resolve(string provider);
}
```

- `AsyncLocal<CadExecutionContext>` for per-call scope.
- Middleware in MCP tool invocation sets context before the tool handler runs.

**Unified DI registration**:

- `RegisterAllProviders()`: registers Inventor, Onshape, SolidWorks (future) in DI.
- Each provider gets a lazy singleton — no connection at startup.
- `IProviderRouter` resolves from the registry by name.
- Tool classes become stateless: they ask `IProviderRouter.Resolve()` for the active provider, get `ICadProvider`, dispatch.

**Tool convergence**:

- `AtomicTools` and `OnshapeAtomicTools` merge into one `AtomicTools` that takes `IProviderRouter`.
- Tools validate: "does this provider support this operation?" (e.g., Onshape doesn't do sketches — returns clear error, not crash).
- `NullMechanicalCadProvider` follows the router: if the current provider is Onshape, mechanical tools return readable errors instead of crashing.

**SolidWorks merge**:

- `feature/solidworks-provider` branch merges into main after unified config and router exist.
- SolidWorks registers as `IMechanicalCadProvider` alongside Inventor.
- Config schema unified: `Cad:Provider` → `providers.solidworks.enabled`.

---

## Affected Areas

| Area | Impact | Phase |
| --- | --- | --- |
| `src/McpCad.Core/UserConfigStore.cs` | New — config read/write, schema, paths | P1 |
| `src/McpCad.Core/IProviderRouter.cs` | New — router interface + context | P2 |
| `src/McpCad.Installer/Program.cs` | Rewrite — provider wizard, agent list, server discovery | P1 |
| `src/McpCad.Server/Program.cs` | Modify — config loading, DI registration | P1+P2 |
| `src/McpCad.Server/ProviderBootstrap.cs` | Rewrite — multi-registration, router awareness | P2 |
| `src/McpCad.Tools/AtomicTools.cs` | Modify — router-based dispatch | P2 |
| `src/McpCad.Tools.Onshape/OnshapeAtomicTools.cs` | Deprecate — converge into AtomicTools | P2 |
| `src/McpCad.Tools/SkillTools.cs` | Modify — router-based dispatch, add select-provider | P1 (skill) + P2 |
| `src/McpCad.Core/NullMechanicalCadProvider.cs` | Modify — clear router errors | P2 |
| `docs/providers.md` | New — config reference, provider setup | P1 |
| `docs/config-schema.md` | New — `config.json` schema documentation | P1 |

---

## Risks

| Risk | L | Mitigation |
| --- | --- | --- |
| **COM crash kills MCP process** (Inventor crash → whole server down) | H | Document in v1; out-of-proc COM is a follow-up project. Router lets us at least catch the provider crash and fail one tool call instead of silent corruption. |
| **Config migration gap** — users with `appsettings.json` + env vars don't run installer and miss new config | M | Server falls back to `Provider:Active` transparently. Old setups keep working. Config file is additive — no breaking change. |
| **SolidWorks config divergence** — branch uses `Cad:Provider`, not `Provider:Active` | M | Unified schema in Phase 1 design absorbs both. Phase 2 merge resolves the branch. |
| **Tool collisions after convergence** — two atomic tools with same name registered | H | Router registers tools once; provider-specific validation at dispatch. Tests verify no MCP tool-name collisions. |
| **Review workload >400 lines per PR** | H | Chained PRs (stacked-to-main or feature-branch-chain per user choice). Phase 1 estimated ~600–900 LOC across 2–3 PRs. Phase 2 estimated ~1,000–1,400 LOC across 3–4 PRs. |
| **Skill fires when user actually meant a specific CAD** (false positive) | L | Skill heuristic: only fires when no provider keyword is present. If user says "create a part in Onshape", the skill skips. Users can also suppress via config. |

---

## Rollback Plan

| Case | Action |
| --- | --- |
| **Phase 1 config store breaks server startup** | Delete/move `config.json` → server falls back to `Provider:Active` + `appsettings.json` — no code revert needed. |
| **Phase 1 installer broken** | `git revert` the installer PR(s). Old `scripts/tui/state.json` workflow still works. |
| **Phase 2 router regression** | Set `active_provider` in config to one provider → router becomes a pass-through (same behavior as today). Fix router in follow-up. |
| **Full rollback** | `git revert` Phase 2 PRs. Server returns to `Provider:Active` branch. Phase 1 config store stays as optional source. |

---

## Success Criteria

- [ ] Phase 1: `UserConfigStore` reads/writes config file on Linux and Windows (cross-platform path test).
- [ ] Phase 1: Installer wizard completes provider + agent setup and leaves valid `config.json`.
- [ ] Phase 1: Server starts from config file (no `appsettings.json` changes) on both platforms.
- [ ] Phase 1: `mcp_cad_select_provider` tool returns provider options and sets execution context for next tool call.
- [ ] Phase 1: Installer detects server correctly on Linux (`McpCad.Server` without `.exe`).
- [ ] Phase 2: Router dispatches to correct provider per tool call — switch within same process without restart.
- [ ] Phase 2: All 6+ chained PRs under 400 changed lines each.
- [ ] Phase 2: Existing tests pass (50 passed, 1 known failing, 7 skipped on Linux).
- [ ] Phase 2: SolidWorks tools register alongside Inventor without collision.
- [ ] Strict TDD: every PR has tests before code.

---

## Dependencies

| Dependency | Type | Phase |
| --- | --- | --- |
| `feature/onshape-provider` merge to `main` | Required before Phase 1 | P1 |
| `feature/solidworks-provider` merge to `main` | Required before Phase 2 | P2 |
| Engram persistence (`sdd/dynamic-provider-routing/proposal`) | OpenSpec + Engram hybrid | Now |
| Chained PR skill (`chained-pr`) | Required for PR planning | P1 + P2 |
| Confirmed strict TDD mode | Required for apply + verify | P1 + P2 |

---

## Product decisions (approved 2026-07-03)

Captured via `ask_user_question` in the orchestrator session:

| # | Topic | Decision |
| --- | --- | --- |
| 1 | Default provider when ambiguous | **Always ask** — skill/menu on every modeling request without an explicit provider keyword. |
| 2 | Skill persistence | **Session only** — do not write `active_provider` from the skill; new conversations use installer/config default. |
| 3 | SolidWorks in Phase 1 wizard | **Yes on Windows** — selectable in installer; sets `active_provider` even before full router (Phase 2 completes routing). |
| 4 | Installer distribution | **`dotnet run` for now** — no release binaries in Phase 1; document one-command setup. |
| 5 | Config migration | **Fallback, no auto-migrate** — server keeps reading `appsettings.json` + `Provider:Active` when `config.json` is absent; user runs installer when ready. |

Implications for spec/design:

- Skill + router context must support **per-request** provider selection (aligns with “always ask”).
- Phase 1 wizard on Windows lists **Inventor, SolidWorks, Onshape**; Linux remains Onshape-first with desktop CAD disabled.
- Phase 1 must document that choosing SolidWorks before Phase 2 merge may be **config-only** until server supports that backend.

---

**Next recommended phase**: `sdd-spec`
