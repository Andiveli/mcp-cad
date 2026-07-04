# Tasks: Dynamic Provider Routing

## Chain Overview

6 chained PRs across 2 phases, **feature-branch-chain** (per design recommendation for rollback control):

```
main
  └── PR 1a (UserConfigStore)          → feature/dpr-config-store
        └── PR 1b (Installer Wizard)   → feature/dpr-installer
              └── PR 1c (Config+Skill) → feature/dpr-server-config
                    └── PR 2a (Router)  → feature/dpr-router
                          └── PR 2b (Convergence) → feature/dpr-tool-convergence
                                └── PR 2c (Null+SW) → feature/dpr-solidworks
                                      └── MERGE to main
```

Each PR targets ≤380 changed lines. All PRs share the same dependency chain. Strict TDD (tests before code) enforced per task.

---

## Review Workload Forecast

| Field | Value |
| ------- | ------- |
| Estimated changed lines | ~2,220 total across 6 PRs (320–380 LOC each) |
| 400-line budget risk | Low |
| Chained PRs recommended | Yes |
| Suggested split | PR 1a → PR 1b → PR 1c → PR 2a → PR 2b → PR 2c |
| Delivery strategy | auto-chain |
| Chain strategy | feature-branch-chain |

```
Decision needed before apply: No
Chained PRs recommended: Yes
Chain strategy: feature-branch-chain
400-line budget risk: Low
```

**Rationale**: Each PR under 400 LOC independently. Auto-chain activates automatically. Feature-branch-chain chosen per design (rollback control over speed). No size exception needed.

---

## Phase 1 — End-User Setup

### PR 1a: UserConfigStore + Config Schema

**Target branch**: `feature/dpr-config-store` (from `main`)
**Estimated Δ**: ~320 LOC

#### Tasks

- [x] **T1.1** [RED] Write unit tests for `UserConfig` model:
  - `UserConfig` deserializes from valid JSON with `version`, `active_provider`, `providers`, `agents`
  - `ProviderConfig` has `Enabled` and `Credentials` (nullable dict)
  - `AgentConfig` has `Name` and `Url`
  - Unknown provider keys preserved on round-trip (forward-compatible)
  - Unknown version loads without crash (deserialized, not thrown)
  - `CreateDefault()` returns `version=1`, `active_provider=null`, all providers present with `enabled=false`, empty agents
  - Round-trip: serialize → deserialize → identical
  - File: `tests/McpCad.Tests/Core/Models/UserConfigTests.cs`

- [x] **T1.2** [GREEN] Create model classes:
  - Add `McpCad.Core/Models/UserConfig.cs` with `UserConfig`, `ProviderConfig`, `AgentConfig` sealed classes
  - Default version = 1, `active_provider` nullable
  - Use `System.Text.Json` attributes for snake_case mapping (`active_provider`, `secret_key`, etc.)
  - All providers (`onshape`, `inventor`, `solidworks`) always present in default
  - File: `src/McpCad.Core/Models/UserConfig.cs`

- [x] **T1.3** [RED] Write unit tests for `UserConfigStore`:
  - `DefaultConfigPath` returns `~/.config/mcp-cad/config.json` on Linux (or `$XDG_CONFIG_HOME` when set)
  - `DefaultConfigPath` returns `%APPDATA%\mcp-cad\config.json` on Windows
  - `Load(path)` returns `UserConfig` for valid file
  - `Load(path)` returns `null` when file doesn't exist
  - `Load(path)` throws `JsonException` on corrupt file
  - `Save(config)` writes valid JSON
  - `Save` atomic: temp file → `File.Replace` → `.bak` created, no torn reads
  - `CreateDefault()` invariant check
  - Zero NuGet dependency: `McpCad.Core.csproj` has no new package references
  - File: `tests/McpCad.Tests/Core/UserConfigStoreTests.cs`

- [x] **T1.4** [GREEN] Implement `UserConfigStore` static class:
  - `DefaultConfigPath` — cross-platform via `Environment.OSVersion.Platform` + `Environment.SpecialFolder`
  - `Load(string path)` — read file, deserialize, return null on `FileNotFoundException`
  - `Save(UserConfig config, string path)` — serialize → write to `.tmp` → `File.Replace(path, path, path.bak)`
  - `Load()` / `Save(config)` overloads using `DefaultConfigPath`
  - `CreateDefault()` — factory with version=1, null active_provider, all providers disabled, empty agents
  - File: `src/McpCad.Core/UserConfigStore.cs`

- [x] **T1.5** [TRIANGULATE] Add edge-case tests:
  - Empty `agents` array is valid
  - `active_provider=null` is valid (means "not configured yet")
  - Write failure leaves no `.tmp` orphan on crash
  - Concurrent reads during write see old or new, never partial
  - File: `tests/McpCad.Tests/Core/UserConfigStoreTests.cs`

- [x] **T1.6** [REFACTOR] Review and inline `UserConfigStore` for zero-dependency compliance:
  - Verify no `Microsoft.Extensions.*` references leak into `McpCad.Core.csproj`
  - Confirm `System.Text.Json` is runtime-inbox (no NuGet needed)
  - Code cleanup, XML docs on public API
  - Files: `src/McpCad.Core/UserConfigStore.cs`, `src/McpCad.Core/Models/UserConfig.cs`, `src/McpCad.Core/McpCad.Core.csproj`

---

### PR 1b: Installer Wizard

**Target branch**: `feature/dpr-installer` (from `feature/dpr-config-store`)
**Estimated Δ**: ~380 LOC

#### Tasks

- [x] **T2.1** [RED] Write installer component tests:
  - Linux provider list shows Onshape selectable; Inventor/SW disabled with `(Windows only)`
  - Windows provider list shows all three selectable
  - `FindServerPath()` finds `McpCad.Server` on Linux (no `.exe`)
  - `FindServerPath()` finds `McpCad.Server.exe` on Windows
  - Server not found → warning printed, config still written
  - Agent list contains OpenCode, Claude Desktop, Pi, VS Code, Custom URL
  - All agents pre-selected by default
  - File: `tests/McpCad.Tests/Installer/InstallerWizardTests.cs`

- [x] **T2.2** [GREEN] Implement provider selection step:
  - Spectre.Console `SelectionPrompt<string>` with platform filtering
  - On Linux: Inventor/SW entries disabled with `(Windows only)` suffix; Onshape only selectable
  - On Windows: all three selectable via `SelectionPrompt`
  - Return chosen provider name string
  - File: `src/McpCad.Installer/Program.cs` (rewrite section)

- [x] **T2.3** [GREEN] Implement credential prompts per provider:
  - Onshape: `TextPrompt` for Access Key + Secret Key (masked) + optional Secret Key ID
  - Inventor (Windows): `ConfirmationPrompt` for auto-connect toggle
  - SolidWorks (Windows): info note only — *"SolidWorks provider is planned — selection is saved for future compatibility"*
  - Return structured credential dictionary per provider
  - File: `src/McpCad.Installer/Program.cs`

- [x] **T2.4** [GREEN] Implement agent registration step:
  - `MultiSelectionPrompt<string>` with known agents (OpenCode, Claude Desktop, Pi, VS Code, Custom URL)
  - Custom URL option prompts for manual URL entry
  - Each selected agent → `AgentConfig{Name, Url}` with default URL
  - File: `src/McpCad.Installer/Program.cs`

- [x] **T2.5** [GREEN] Implement server discovery (`FindServerPath`):
  - Walk up from assembly directory, search `dist/` subdirs
  - Linux: search `McpCad.Server` (no `.exe`)
  - Windows: search `McpCad.Server.exe`
  - Return full path or null (not found)
  - Modify `McpCad.Installer/McpAgent.cs` current discovery logic
  - File: `src/McpCad.Installer/McpAgent.cs` (or inline in Program.cs)

- [x] **T2.6** [GREEN] Wire wizard completion:
  - Build `UserConfig` from accumulated wizard data
  - Call `UserConfigStore.Save(config)`
  - On success: print *"mcp-cad configured. Restart your MCP host."*
  - On write failure: display error, retry prompt
  - Verify legacy `scripts/tui/state.json` is NOT modified
  - File: `src/McpCad.Installer/Program.cs`

- [x] **T2.7** [TRIANGULATE] Add edge-case tests:
  - `Ctrl+C` during any step → clean exit, no partial config file
  - Empty credential fields for Onshape → validation error, retry prompt
  - All agents deselected → empty `agents` array (valid config)
  - File: `tests/McpCad.Tests/Installer/InstallerWizardTests.cs`

---

### PR 1c: Server Config Load + Select-Provider Skill

**Target branch**: `feature/dpr-server-config` (from `feature/dpr-installer`)
**Estimated Δ**: ~380 LOC

#### Tasks

- [x] **T3.1** [RED] Write config load order tests:
  - Config file present with `active_provider="onshape"` → server uses `onshape`
  - Config file absent → server falls back to `Provider:Active` from `appsettings.json`
  - Config file `active_provider=null` → fallback to `appsettings.json`
  - Config file present + `Provider:Active` also set → config wins
  - File: `tests/McpCad.Tests/Server/ConfigLoadTests.cs`

- [x] **T3.2** [GREEN] Create `CadExecutionContext` (Phase 1 basic):
  - `AsyncLocal<CadExecutionContext>` static instance
  - `SelectedProvider` property (nullable string)
  - Session-only — no write to config
  - File: `src/McpCad.Core/CadExecutionContext.cs`

- [x] **T3.3** [GREEN] Modify `Program.cs` to load config before provider bootstrap:

  ```
  var userConfig = UserConfigStore.Load();
  if (userConfig?.ActiveProvider is not null)
      activeProvider = userConfig.ActiveProvider;
  else
      activeProvider = ProviderBootstrap.ResolveActiveProvider(configuration);
  ```

  - Pass `userConfig` to `RegisterCadProvider` for credential access
  - File: `src/McpCad.Server/Program.cs`

- [x] **T3.4** [GREEN] Modify `ProviderBootstrap.RegisterCadProvider`:
  - Accept `UserConfig? userConfig` optional parameter
  - Read `active_provider` from userConfig when present
  - Pass credentials from userConfig to provider registration (Onshape keys, etc.)
  - Keep existing `Provider:Active` fallback logic when userConfig is null
  - File: `src/McpCad.Server/ProviderBootstrap.cs`

- [x] **T3.5** [RED] Write `SelectProviderTool` tests:
  - `mcp_cad_select_provider("inventor")` sets `CadExecutionContext.SelectedProvider` to `"inventor"`
  - `mcp_cad_select_provider("unknown")` returns error response
  - Subsequent tool call reads context and dispatches to selected provider
  - Context is session-only (not persisted to config.json)
  - Tool registered regardless of active provider
  - File: `tests/McpCad.Tests/Tools/SelectProviderToolTests.cs`

- [x] **T3.6** [GREEN] Implement `SelectProviderTool`:
  - `[McpServerToolType]` class in `McpCad.Tools`
  - `mcp_cad_select_provider(string provider)` method
  - Validate provider is known (`inventor`, `onshape`, `solidworks`)
  - Set `CadExecutionContext.Current.SelectedProvider`
  - Return confirmation with provider name
  - Always registered (in both Inventor and Onshape branches)
  - File: `src/McpCad.Tools/SelectProviderTool.cs`

- [x] **T3.7** [GREEN] Register `SelectProviderTool` in `Program.cs`:
  - Add `mcp.WithTools<SelectProviderTool>()` to both registration branches
  - File: `src/McpCad.Server/Program.cs`

---

## Phase 2 — Dynamic Routing

### PR 2a: IProviderRouter + Lazy Registry

**Target branch**: `feature/dpr-router` (from `feature/dpr-server-config`)
**Estimated Δ**: ~380 LOC

#### Tasks

- [x] **T4.1** [RED] Write `ProviderRouter` tests:
  - `PushProvider("inventor")` → `CurrentProvider` = `"inventor"`
  - `PushProvider` returns `IDisposable` → dispose restores prior provider
  - Concurrent calls have isolated `AsyncLocal` context
  - Default provider from config when no push called
  - `Resolve("inventor")` returns `ICadProvider` instance
  - `Resolve("unknown")` throws `ProviderNotFoundException`
  - `Resolve("inventor")` with `enabled=false` throws `ProviderDisabledException`
  - `AvailableProviders` lists all registered names
  - File: `tests/McpCad.Tests/Core/ProviderRouterTests.cs`

- [x] **T4.2** [GREEN] Define `IProviderRouter` interface:
  - `string CurrentProvider`
  - `IDisposable PushProvider(string provider)`
  - `ICadProvider Resolve(string provider)`
  - `IReadOnlyCollection<string> AvailableProviders`
  - File: `src/McpCad.Core/IProviderRouter.cs`

- [x] **T4.3** [GREEN] Define router exception classes:
  - `ProviderNotFoundException(string provider, IEnumerable<string> available)`
  - `ProviderDisabledException(string provider)`
  - `ProviderConnectionException(string message, Exception? inner = null)`
  - All in `McpCad.Core/Exceptions/` namespace
  - Files: `src/McpCad.Core/Exceptions/ProviderNotFoundException.cs`, `src/McpCad.Core/Exceptions/ProviderDisabledException.cs`, `src/McpCad.Core/Exceptions/ProviderConnectionException.cs`

- [x] **T4.4** [GREEN] Implement `ProviderRouter`:
  - `AsyncLocal<CadExecutionContext>` for per-flow isolation
  - Constructor accepts `IReadOnlyDictionary<string, Lazy<ICadProvider>>` + default provider name
  - `PushProvider` creates new context with `CurrentProvider`, saves prior as linked list
  - `ProviderScope` disposable restores prior context
  - `Resolve` triggers lazy construction on first access, caches subsequent
  - Thread-safe via `Lazy<T>` for each provider
  - File: `src/McpCad.Core/ProviderRouter.cs`

- [x] **T4.5** [GREEN] Extend `CadExecutionContext` for Phase 2:
  - Add `CurrentProvider` property
  - Add `PriorContext` property (linked list for stack semantics)
  - Keep `SelectedProvider` from Phase 1
  - File: `src/McpCad.Core/CadExecutionContext.cs`

- [x] **T4.6** [GREEN] Rewrite `ProviderBootstrap` to `RegisterAllProviders`:
  - Replace `RegisterCadProvider` switch with `RegisterAllProviders(IServiceCollection, IConfiguration, UserConfig?)`
  - Register Inventor, Onshape, SolidWorks all as lazy singletons
  - SolidWorks always registered: on Linux resolves with clear error
  - Build `Dictionary<string, Lazy<ICadProvider>>` registry
  - Register `IProviderRouter` as singleton with the registry
  - Register `AtomicTools`, `SkillTools`, `SelectProviderTool` always (not per-provider)
  - Keep `NullMechanicalCadProvider` registration for now (removed in PR 2c)
  - File: `src/McpCad.Server/ProviderBootstrap.cs`

- [x] **T4.7** [TRIANGULATE] Add router concurrency and edge-case tests:
  - Concurrent first access to same provider → single instance (thread-safe)
  - Unused provider never constructed
  - `RegisterAllProviders` registers all names without connecting
  - COM fail on Linux → other providers unaffected
  - File: `tests/McpCad.Tests/Core/ProviderRouterTests.cs`

---

### PR 2b: Tool Convergence

**Target branch**: `feature/dpr-tool-convergence` (from `feature/dpr-router`)
**Estimated Δ**: ~380 LOC

#### Tasks

- [x] **T5.1** [RED] Write tool convergence tests:
  - `AtomicTools` accepts `IProviderRouter` (not `ICadProvider`)
  - `fillet` call with current provider = Onshape → `{"success": false, "error": "onshape does not support fillet"}`
  - `fillet` call with current provider = Inventor → dispatches to `InventorProvider.Fillet()`
  - Same tool name routes to different provider per call context
  - No duplicate tool names in MCP registry
  - Web-specific operations (`ws_list`, `element_list`) available via `WebCadTools`
  - Backward-compat aliases work in transition
  - Tool statelessness: no field of type `ICadProvider` or concrete provider
  - File: `tests/McpCad.Tests/Tools/AtomicToolsRouterTests.cs`

- [x] **T5.2** [GREEN] Modify `AtomicTools` to accept `IProviderRouter`:
  - Constructor changes from `IMechanicalCadProvider` to `IProviderRouter`
  - Each method calls `_router.Resolve(_router.CurrentProvider)`
  - Add interface check: `if (provider is not IMechanicalCadProvider mech) return error`
  - File: `src/McpCad.Tools/AtomicTools.cs`

- [x] **T5.3** [GREEN] Modify `SkillTools` to accept `IProviderRouter`:
  - Same pattern as `AtomicTools` — constructor takes `IProviderRouter`, resolve per call
  - Interface check for `IMechanicalCadProvider` on mechanical-only operations
  - File: `src/McpCad.Tools/SkillTools.cs`

- [x] **T5.4** [GREEN] Create `WebCadTools` for web-CAD-specific operations:
  - Constructor takes `IProviderRouter`
  - Methods: `ws_list`, `element_list`, `feature_add`, `sketch_replace`
  - Each checks `_router.Resolve(_router.CurrentProvider) is ICadWebProvider web`
  - If not web CAD → return error: *"This operation requires a web CAD provider (Onshape)"*
  - File: `src/McpCad.Tools/WebCadTools.cs`

- [x] **T5.5** [GREEN] Deprecate `OnshapeAtomicTools` and `OnshapeSkillTools`:
  - Add `[Obsolete("Converged into AtomicTools with IProviderRouter")]` to both classes
  - Remove from MCP registration in `Program.cs`
  - Keep source file for one PR cycle (callers migrate)
  - Files: `src/McpCad.Tools/Onshape/OnshapeAtomicTools.cs`, `src/McpCad.Tools/Onshape/OnshapeSkillTools.cs`

- [x] **T5.6** [GREEN] Update `Program.cs` for unified tool registration:
  - Remove `if (activeProvider == "onshape")` conditional tool registration
  - Register `AtomicTools`, `SkillTools`, `WebCadTools`, `SelectProviderTool` always
  - Remove `OnshapeAtomicTools` and `OnshapeSkillTools` from `WithTools<>()` calls
  - Remove provider-conditional `#if WINDOWS` tool registration branches
  - File: `src/McpCad.Server/Program.cs`

- [x] **T5.7** [GREEN] Add backward-compat tool aliases:
  - Register `inventor_connect` as `[McpServerTool(Name = "inventor_connect")]` that delegates to `connect`
  - Same for `inventor_health`, `inventor_disconnect`, `onshape_connect`, `onshape_health`
  - Aliases marked with `[Obsolete("Use connect instead")]`
  - Removed in PR 2c
  - File: `src/McpCad.Tools/AtomicTools.cs` (alias methods section)

---

### PR 2c: NullProvider Removal + SW Prep

**Target branch**: `feature/dpr-solidworks` (from `feature/dpr-tool-convergence`)
**Estimated Δ**: ~380 LOC

#### Tasks

- [x] **T6.1** [RED] Write provider registration and interface check tests:
  - All providers registered in DI (Inventor, Onshape, SolidWorks)
  - SolidWorks resolver on Linux throws clear error
  - NullMechanicalCadProvider not registered in DI
  - Tool interface checks (`is IMechanicalCadProvider`) work for all mechanical operations
  - Error messages include active provider name (not hardcoded "Inventor")
  - SolidWorks implements `IMechanicalCadProvider`
  - `Cad:Provider` from SolidWorks branch maps to unified config keys
  - Backward-compat aliases removed (only unified names exist)
  - File: `tests/McpCad.Tests/Server/ProviderRegistrationTests.cs`

- [x] **T6.2** [GREEN] Remove `NullMechanicalCadProvider` from DI:
  - Delete `services.AddSingleton<IMechanicalCadProvider, NullMechanicalCadProvider>()` in `ProviderBootstrap`
  - Tools already use `is IMechanicalCadProvider` check (from PR 2b)
  - Update `NullMechanicalCadProvider` to return active-provider-aware error messages if kept, or remove class
  - Decision: remove class entirely — tool-level interface check supersedes it
  - File: `src/McpCad.Core/NullMechanicalCadProvider.cs` (delete or degrade to deprecated shim)

- [x] **T6.3** [GREEN] Register SolidWorks as `IMechanicalCadProvider`:
  - Add `SolidWorksProvider` registration in `RegisterAllProviders` (lazy singleton)
  - Platform-gated: always registered, Linux resolve throws `ProviderConnectionException("SolidWorks requires Windows")`
  - File: `src/McpCad.Server/ProviderBootstrap.cs`

- [x] **T6.4** [GREEN] Implement `Cad:Provider` config fallback:
  - In config load, check for `Cad:Provider` key when `config.json` is absent
  - Map `Cad:Provider = SolidWorks` → treat as `active_provider = solidworks`
  - Map `Cad:Provider = Inventor` → treat as `active_provider = inventor`
  - File: `src/McpCad.Server/Program.cs` (config loader section)

- [x] **T6.5** [GREEN] Clean up backward-compat aliases:
  - Remove `inventor_connect`, `inventor_health`, `onshape_connect`, etc. alias methods from `AtomicTools`
  - Remove `[Obsolete]` shim methods
  - Only unified names remain (`connect`, `health`, `disconnect`, etc.)
  - File: `src/McpCad.Tools/AtomicTools.cs`

- [x] **T6.6** [TRIANGULATE] Run full test suite and verify:
  - Existing 50 tests pass (0 regression)
  - All new Phase 2 tests pass
  - No duplicate MCP tool names in registry
  - Verify SolidWorks not required for test execution on Linux
  - File: verification (no file change)

---

## Task Dependency Graph

```
PR 1a                PR 1b         PR 1c          PR 2a           PR 2b            PR 2c
T1.1 ──→ T1.2
         T1.3 ──→ T1.4                   
                  T1.5 ──→ T1.6
                              \
                               → T2.1 ──→ T2.2
                                        → T2.3
                                        → T2.4
                                        → T2.5
                                        → T2.6
                                        → T2.7
                                              \
                                               → T3.1 ──→ T3.2
                                                        → T3.3
                                                        → T3.4
                                               → T3.5 ──→ T3.6
                                                        → T3.7
                                                              \
                                                               → T4.1 ──→ T4.2
                                                                        → T4.3
                                                                        → T4.4
                                                                        → T4.5
                                                                        → T4.6
                                                                        → T4.7
                                                                              \
                                                                               → T5.1 ──→ T5.2
                                                                                        → T5.3
                                                                                        → T5.4
                                                                                        → T5.5
                                                                                        → T5.6
                                                                                        → T5.7
                                                                                              \
                                                                                               → T6.1 ──→ T6.2
                                                                                                        → T6.3
                                                                                                        → T6.4
                                                                                                        → T6.5
                                                                                                        → T6.6
```

---

## File Change Summary

| Phase | PR | New Files | Modified Files | Removed Files | Est. Δ |
| ------- | ---- | ----------- | ---------------- | --------------- | -------- |
| P1 | 1a | `McpCad.Core/Models/UserConfig.cs`, `McpCad.Core/UserConfigStore.cs`, `McpCad.Tests/Core/Models/UserConfigTests.cs`, `McpCad.Tests/Core/UserConfigStoreTests.cs` | — | — | ~320 |
| P1 | 1b | `McpCad.Tests/Installer/InstallerWizardTests.cs` | `McpCad.Installer/Program.cs` (rewrite), `McpCad.Installer/McpAgent.cs` | — | ~380 |
| P1 | 1c | `McpCad.Core/CadExecutionContext.cs`, `McpCad.Tools/SelectProviderTool.cs`, `McpCad.Tests/Server/ConfigLoadTests.cs`, `McpCad.Tests/Tools/SelectProviderToolTests.cs` | `McpCad.Server/Program.cs`, `McpCad.Server/ProviderBootstrap.cs` | — | ~380 |
| P2 | 2a | `McpCad.Core/IProviderRouter.cs`, `McpCad.Core/ProviderRouter.cs`, `McpCad.Core/Exceptions/*.cs`, `McpCad.Tests/Core/ProviderRouterTests.cs` | `McpCad.Core/CadExecutionContext.cs`, `McpCad.Server/ProviderBootstrap.cs` | — | ~380 |
| P2 | 2b | `McpCad.Tools/WebCadTools.cs`, `McpCad.Tests/Tools/AtomicToolsRouterTests.cs` | `McpCad.Tools/AtomicTools.cs`, `McpCad.Tools/SkillTools.cs`, `McpCad.Server/Program.cs`, `McpCad.Tools/Onshape/OnshapeAtomicTools.cs`, `McpCad.Tools/Onshape/OnshapeSkillTools.cs` | — | ~380 |
| P2 | 2c | `McpCad.Tests/Server/ProviderRegistrationTests.cs` | `McpCad.Server/ProviderBootstrap.cs`, `McpCad.Server/Program.cs`, `McpCad.Tools/AtomicTools.cs` | `McpCad.Core/NullMechanicalCadProvider.cs` (or degrade) | ~380 |
| **Total** | **6** | **14 new files** | **~10 modified** | **1 removed** | **~2,220** |

---

## Verification Criteria

Each PR independently verifiable:

- **PR 1a**: `dotnet test --filter "Category=UserConfigStore"` passes; no NuGet added to `McpCad.Core`
- **PR 1b**: Installer wizard runs via `dotnet run --project src/McpCad.Installer`; produces valid `config.json`
- **PR 1c**: Server starts with `config.json`; skill tool responds to `mcp_cad_select_provider`
- **PR 2a**: Router resolves/pushes/pops providers; concurrent calls isolated
- **PR 2b**: Same `fillet` tool routes to Inventor or Onshape per context; no duplicate tool names
- **PR 2c**: SolidWorks registered; NullProvider removed; all 50+ existing tests green

---

## Rollback Boundaries

| PR | Rollback |
| ---- | ---------- |
| 1a | Revert → server falls back to `Provider:Active` + `appsettings.json` (no file change) |
| 1b | Revert → old `scripts/tui/state.json` workflow still works |
| 1c | Revert → server returns to single-source `Provider:Active` switch |
| 2a | Set `active_provider` in config → router becomes pass-through (same behavior as pre-router) |
| 2b | Revert → `OnshapeAtomicTools`/`AtomicTools` separate registration restored |
| 2c | Revert → NullProvider returns, aliases return, SolidWorks unregistered |

---

## Risks per PR

| PR | Risk | Mitigation |
| ---- | ------ | ------------ |
| 1a | Path resolution wrong on one platform | Test both platforms in CI; use `Environment.OSVersion` branch |
| 1b | Spectre.Console version mismatch (current 0.55.2) | Verify existing dep; use only basic `SelectionPrompt`/`TextPrompt` |
| 1c | Config + appsettings merge complexity | Keep merge minimal: config wins, appsettings fallback only |
| 2a | AsyncLocal isolation missed in a tool path | Concurrency test + code review on all tool methods |
| 2b | Tool name collision in MCP registry | `ToolRegistrationValidator.EnsureNoCollisions` called at startup |
| 2c | SolidWorks `Cad:Provider` key not fully handled | Document fallback behavior; test both key paths |
