# Design: Dynamic Provider Routing

**Change**: `dynamic-provider-routing`
**Date**: 2026-07-03
**Store**: OpenSpec (`openspec/changes/dynamic-provider-routing/design.md`) + Engram (`sdd/dynamic-provider-routing/design`)

One MCP server process that routes CAD operations to Inventor, SolidWorks, or Onshape **per tool call** — no restarts, no separate installations. Delivered in two chained phases: **Phase 1** (end-user setup pain relief: unified config, installer TUI, select-provider skill) and **Phase 2** (dynamic router, lazy DI, tool convergence).

Product decisions (pre-approved, reflected throughout):

- **Always ask** — when no provider keyword is present, the skill prompts the user
- **Session-only skill persistence** — skill choice does not write `active_provider` to config
- **SolidWorks in Phase 1 wizard** — selectable on Windows, saved for future compatibility
- **`dotnet run` distribution** — no release binaries in Phase 1
- **Config fallback** — `appsettings.json` + `Provider:Active` remain when `config.json` is absent

---

## Architecture Overview

```
┌──────────────────────────────────────────────────────────────────┐
│                       MCP Server Process                          │
│                                                                   │
│  ┌───────────────────┐   ┌──────────────┐   ┌─────────────────┐  │
│  │   Program.cs       │   │  Config       │   │  ProviderBootstrap│ │
│  │  (entry, DI setup) │──▶│  Loader       │──▶│  (rewritten)     │ │
│  └───────────────────┘   └──────┬───────┘   └────────┬────────┘  │
│                                 │                     │           │
│  ┌──────────────────────────────▼─────────────────────▼────────┐  │
│  │                    Config Source Chain                       │  │
│  │  1. ~/.config/mcp-cad/config.json  (UserConfigStore.Load())  │  │
│  │  2. appsettings.json  (legacy — Provider:Active)             │  │
│  │  3. Fallback default → "inventor"                            │  │
│  └──────────────────────────────────────────────────────────────┘  │
│                                                                   │
│  ┌──────────────────────────────────────────────────────────────┐  │
│  │              Phase 2: IProviderRouter + AsyncLocal            │  │
│  │  ┌──────────────┐  ┌──────────────┐  ┌────────────────────┐  │  │
│  │  │  Inventor     │  │  Onshape      │  │  SolidWorks       │  │  │
│  │  │  Provider     │  │  Provider     │  │  Provider (lazy)  │  │  │
│  │  │  (lazy COM)   │  │  (lazy HTTP)  │  │  (lazy — P2)     │  │  │
│  │  └──────┬───────┘  └──────┬───────┘  └──────┬─────────────┘  │  │
│  │         └─────────────────┼──────────────────┘                │  │
│  │                           │ All implement ICadProvider        │  │
│  └───────────────────────────┼──────────────────────────────────┘  │
│                              │                                    │
│  ┌───────────────────────────▼──────────────────────────────────┐  │
│  │            Unified Tools Registration                         │  │
│  │  ┌────────────────────────────────────────────────────────┐  │  │
│  │  │  AtomicTools(IProviderRouter)   SkillTools(router)     │  │  │
│  │  │  SelectProviderTool (additional skill tool)            │  │  │
│  │  └────────────────────────────────────────────────────────┘  │  │
│  │  Each tool call: router.Resolve(router.CurrentProvider)       │  │
│  │  + interface check (is IMechanicalCadProvider) for dispatch   │  │
│  └──────────────────────────────────────────────────────────────┘  │
│                                                                   │
│  ┌──────────────────────────────────────────────────────────────┐  │
│  │    P1: CadExecutionContext (AsyncLocal — basic)                │  │
│  │    P2: ProviderRouter (full — push/pop scope per call)        │  │
│  └──────────────────────────────────────────────────────────────┘  │
└──────────────────────────────────────────────────────────────────┘
```

### Data Flow Per Tool Call (Phase 2)

```
MCP tool call arrives
  │
  ▼
Middleware/routing layer:
  1. Read CadExecutionContext.Current.SelectedProvider
     (set by select-provider skill, or null)
  2. If null → use active_provider from config
  3. Call router.PushProvider(resolvedProvider)
  4. Execute tool handler
  5. Dispose scope → restores previous context
  │
  ▼
Tool handler:
  provider = router.Resolve(router.CurrentProvider)
  if operation requires IMechanicalCadProvider:
      if provider is not IMechanicalCadProvider → return error
  result = provider.Method(...params...)
  │
  ▼
Return result to MCP client
```

---

## Phase 1 Design

### 1. UserConfigStore API

**Location**: `McpCad.Core/UserConfigStore.cs`
**Dependencies**: Zero NuGet — uses `System.Text.Json` (runtime inbox)
**Lifetime**: Static utility class (no DI needed for simple read/write)

```csharp
namespace McpCad.Core;

public static class UserConfigStore
{
    // Returns platform-appropriate path:
    //   Linux:   $XDG_CONFIG_HOME/mcp-cad/config.json or ~/.config/mcp-cad/config.json
    //   Windows: %APPDATA%\mcp-cad\config.json
    public static string DefaultConfigPath { get; }

    // Loads config from DefaultConfigPath.
    // Returns null (not throw) when file doesn't exist.
    // Throws JsonException on corrupt file.
    public static UserConfig? Load();

    // Loads from explicit path (for testing).
    public static UserConfig? Load(string path);

    // Atomic write: serialize → temp file → File.Replace (creates .bak)
    public static void Save(UserConfig config);

    // Explicit path save.
    public static void Save(UserConfig config, string path);

    // Factory for default config: version=1, active_provider=null,
    // all providers present with enabled=false, empty agents list.
    public static UserConfig CreateDefault();
}
```

**Atomic write contract**:

1. Serialize to `path.tmp`
2. `File.Replace(path.tmp, path, path.bak)` — readers see old or new, never partial
3. On failure: clean up temp file, throw

**Model classes** (`McpCad.Core/Models/UserConfig.cs`):

```csharp
namespace McpCad.Core.Models;

public sealed class UserConfig
{
    public int Version { get; set; } = 1;
    public string? ActiveProvider { get; set; }
    public Dictionary<string, ProviderConfig> Providers { get; set; } = new();
    public List<AgentConfig> Agents { get; set; } = new();
}

public sealed class ProviderConfig
{
    public bool Enabled { get; set; }
    public Dictionary<string, string>? Credentials { get; set; }  // provider-specific keys
}

public sealed class AgentConfig
{
    public string Name { get; set; } = "";
    public string Url { get; set; } = "";
}
```

**Design decision — `active_provider` can be null**: This signals "not yet configured." The server falls back to `Provider:Active` from `appsettings.json`. The skill can set a session-level override, but the config file's `active_provider` is only written when the user explicitly runs the installer or sets a default.

**Design decision — credentials as flat dict**: Each provider has different credential shapes (Onshape: access_key+secret_key; Inventor: auto-connect COM). Using `Dictionary<string, string>?` avoids a complex discriminated union while keeping the schema flexible. Provider-specific validation lives in the credential prompt step, not in the model.

### 2. Config Schema (JSON)

```json
{
  "version": 1,
  "active_provider": "onshape",
  "providers": {
    "onshape": {
      "enabled": true,
      "credentials": {
        "access_key": "YOUR_ACCESS_KEY",
        "secret_key": "YOUR_SECRET_KEY",
        "secret_key_id": "OPTIONAL_SKID"
      }
    },
    "inventor": {
      "enabled": false,
      "credentials": null
    },
    "solidworks": {
      "enabled": false,
      "credentials": null
    }
  },
  "agents": [
    { "name": "OpenCode", "url": "http://localhost:51777" }
  ]
}
```

**Schema invariants**:

- Unknown provider keys are preserved (forward-compatible)
- Unknown `version` is tolerated with a warning
- `active_provider` null is valid (means "not configured yet")
- All three provider entries always present (even if disabled)
- Empty `agents` array is valid

### 3. Installer Wizard Flow

**Location**: `McpCad.Installer/Program.cs` (rewrite)
**Framework**: Spectre.Console 0.55.2 (existing dependency)
**Distribution**: `dotnet run --project src/McpCad.Installer` (no pre-built binaries)

```
┌────────────────────────────────────────────┐
│          mcp-cad Setup Wizard              │
│                                            │
│  ┌──────────────────────────────────────┐  │
│  │ Step 1: Select CAD Provider          │  │
│  │                                      │  │
│  │  ○ Autodesk Inventor  (Windows)     │  │
│  │  ○ SolidWorks         (Windows)     │  │
│  │  ● Onshape                         │  │
│  │                                      │  │
│  │  [Linux: Inventor/SW disabled with   │  │
│  │   "(Windows only)" suffix]          │  │
│  └──────────────────────────────────────┘  │
│                      ▼                      │
│  ┌──────────────────────────────────────┐  │
│  │ Step 2: Credentials                  │  │
│  │                                      │  │
│  │  If Onshape:                        │  │
│  │    Access Key: [______________]     │  │
│  │    Secret Key: [______________]     │  │
│  │    Secret Key ID: [__________] (opt) │  │
│  │                                      │  │
│  │  If Inventor (Windows):             │  │
│  │    Auto-connect to Inventor? [Yes]  │  │
│  │                                      │  │
│  │  If SolidWorks (Windows):           │  │
│  │    Note: Phase 2 — saved for future │  │
│  └──────────────────────────────────────┘  │
│                      ▼                      │
│  ┌──────────────────────────────────────┐  │
│  │ Step 3: Agent Registration           │  │
│  │                                      │  │
│  │  [x] OpenCode  localhost:51777      │  │
│  │  [ ] Claude    (auto-detect)        │  │
│  │  [ ] Pi        (auto-detect)        │  │
│  │  [x] VS Code   (auto-detect)        │  │
│  │  [ ] Custom URL [______________]    │  │
│  │                                      │  │
│  │  Server: /home/user/mcp-cad/dist/   │  │
│  │          McpCad.Server              │  │
│  └──────────────────────────────────────┘  │
│                      ▼                      │
│  ┌──────────────────────────────────────┐  │
│  │ Step 4: Write Config                 │  │
│  │                                      │  │
│  │  ✓ mcp-cad configured.              │  │
│  │    Restart your MCP host.            │  │
│  └──────────────────────────────────────┘  │
└────────────────────────────────────────────┘
```

**Server discovery fix** (`McpCad.Installer/McpAgents.cs`):

```csharp
// Current (broken on Linux):
//   "dist/mcp-cad/McpCad.Server.exe" hardcoded

// New logic (FindServerPath):
//   1. Walk up from assembly dir (same as today)
//   2. In dist/ subdirs, search for:
//      - Linux: "McpCad.Server" (no .exe)
//      - Windows: "McpCad.Server.exe"
//   3. Also search for "McpCad.Server" on Linux in dist/mcp-cad/
//   4. If not found: warn with expected path, still write config
```

**Agent registration change**: Instead of writing to per-app config files (OpenCode, Claude, VS Code), the wizard writes the agent list to `config.json`. The server exposes the list via an MCP tool, and agents read it from there. This decouples agent configuration from the installer — one write, many consumers.

**Legacy state file**: `scripts/tui/state.json` is NOT modified. The new `config.json` is the sole output. Old workflow still works for rollback.

### 4. Server Config Load Order

**Location**: `McpCad.Server/Program.cs` (modified)
**Logic**:

```
1. Try UserConfigStore.Load()
   │
   ├── Found → use config.ActiveProvider as active provider
   │           Pass provider credentials to registration
   │           (overrides appsettings for that provider)
   │
   └── Not found → fall back to current behavior:
                    ProviderBootstrap.ResolveActiveProvider(configuration)
                    which reads Provider:Active from appsettings.json
   
2. Provider registration
   ├── Phase 1: single provider switch (same as today, but source is config file)
   └── Phase 2: RegisterAllProviders() (all providers, lazy)
```

**Code sketch (Phase 1)**:

```csharp
// Program.cs — added before ProviderBootstrap call
var userConfig = UserConfigStore.Load();
string activeProvider;

if (userConfig is not null && userConfig.ActiveProvider is not null)
{
    activeProvider = userConfig.ActiveProvider;
    // Merge userConfig into IConfiguration for credential access
    // (or pass directly to registration)
}
else
{
    activeProvider = ProviderBootstrap.ResolveActiveProvider(builder.Configuration);
}

ProviderBootstrap.RegisterCadProvider(builder.Services, builder.Configuration, userConfig);
```

**Migration path**: Users who never run the installer keep working — `appsettings.json` + `Provider:Active` is the fallback. No breaking change. When they run the installer, the config file appears and the server picks it up on next restart.

### 5. Select-Provider Skill + AsyncLocal Context (Phase 1)

#### Skill: `mcp_cad_select_provider`

**Type**: MCP tool (not a middleware hook)
**Location**: `McpCad.Tools/SelectProviderTool.cs`
**Registration**: Always registered (regardless of active provider)

```csharp
[McpServerToolType]
public class SelectProviderTool
{
    [McpServerTool, Description(
        "Select which CAD provider to use for subsequent tool calls. " +
        "Triggered automatically when you ask to create/model something " +
        "without naming a provider (Inventor, SolidWorks, or Onshape).")]
    public Dictionary<string, object?> mcp_cad_select_provider(string provider)
    {
        // Validate provider is known
        // Set CadExecutionContext.Current.SelectedProvider = provider
        // Return confirmation
    }
}
```

**Trigger**: Agent-side (OpenCode or MCP host) detects missing provider keyword and calls this tool before the actual modeling tool. The skill is an MCP tool — the AI calls it when it infers ambiguity.

**Agent prompt heuristic** (documented, not enforced in code):
> "When the user says 'create a part', 'make a model', 'design something', and does NOT mention 'inventor', 'onshape', or 'solidworks', call `mcp_cad_select_provider` first."

#### AsyncLocal Context (Phase 1 — Basic)

```csharp
// McpCad.Core/CadExecutionContext.cs  (Phase 1 version)

namespace McpCad.Core;

public sealed class CadExecutionContext
{
    private static readonly AsyncLocal<CadExecutionContext> _current = new();

    public static CadExecutionContext Current
    {
        get => _current.Value ??= new CadExecutionContext();
        internal set => _current.Value = value;
    }

    /// <summary>
    /// Provider selected by the skill for this session/tool call.
    /// Null means "use config default."
    /// Phase 2 extends this with PushProvider/CurrentProvider.
    /// </summary>
    public string? SelectedProvider { get; set; }
}
```

**Phase 1 usage**:

1. `SelectProviderTool` sets `CadExecutionContext.Current.SelectedProvider = "inventor"`
2. Next tool call reads `CadExecutionContext.Current.SelectedProvider`
3. If set → use it (overrides config `active_provider`)
4. If null → use config fallback
5. **Session-only**: `SelectedProvider` persists in memory for the MCP session but is NOT written to config

**Not thread-isolated in Phase 1**: Single `AsyncLocal` value per async flow. For single-user MCP with sequential calls, this is sufficient. Phase 2 adds explicit `PushProvider`/dispose scoping.

---

## Phase 2 Design

### 6. IProviderRouter + AsyncLocal Execution Context (Full)

```csharp
// McpCad.Core/IProviderRouter.cs

namespace McpCad.Core;

public interface IProviderRouter
{
    /// <summary>Active provider for the current execution context.</summary>
    string CurrentProvider { get; }

    /// <summary>All registered (and enabled) provider names.</summary>
    IReadOnlyCollection<string> AvailableProviders { get; }

    /// <summary>
    /// Scope execution to the specified provider.
    /// Returns IDisposable that restores previous context on dispose.
    /// </summary>
    IDisposable PushProvider(string provider);

    /// <summary>Resolve a provider backend by name.</summary>
    ICadProvider Resolve(string provider);
}
```

```csharp
// McpCad.Core/ProviderRouter.cs  (Phase 2 implementation)

namespace McpCad.Core;

public sealed class ProviderRouter : IProviderRouter, IDisposable
{
    private readonly AsyncLocal<CadExecutionContext> _context = new();
    private readonly IReadOnlyDictionary<string, Lazy<ICadProvider>> _registry;

    public string CurrentProvider =>
        _context.Value?.CurrentProvider ?? _defaultProvider;

    public IReadOnlyCollection<string> AvailableProviders =>
        _registry.Keys;

    public IDisposable PushProvider(string provider)
    {
        if (!_registry.ContainsKey(provider))
            throw new ProviderNotFoundException(provider, _registry.Keys);

        // Snapshot current context, push new scope
        var prior = _context.Value;
        _context.Value = new CadExecutionContext
        {
            CurrentProvider = provider,
            PriorContext = prior,
        };

        return new ProviderScope(this, prior);
    }

    public ICadProvider Resolve(string provider)
    {
        if (!_registry.TryGetValue(provider, out var lazy))
            throw new ProviderNotFoundException(provider, _registry.Keys);

        try { return lazy.Value; }
        catch (Exception ex)
        {
            throw new ProviderConnectionException(
                $"Failed to connect {provider}: {ex.Message}", ex);
        }
    }

    // DisposeScope restores prior context
    private sealed class ProviderScope : IDisposable
    {
        private readonly ProviderRouter _router;
        private readonly CadExecutionContext? _prior;
        // ...
    }
}
```

**Key properties**:

- `AsyncLocal<CadExecutionContext>` — per-async-flow isolation
- `PushProvider` returns `IDisposable` → `using` in middleware guarantees restore even on exception
- `Registry` is `Dictionary<string, Lazy<ICadProvider>>` — immutable after DI freeze, thread-safe
- Provider resolution triggers lazy construction + connection

### 7. Lazy Provider DI Registration

**Location**: `McpCad.Server/ProviderBootstrap.cs` (rewrite from switch to multi-registration)

```csharp
// Phase 2 — RegisterAllProviders

public static void RegisterAllProviders(IServiceCollection services, IConfiguration configuration, UserConfig? userConfig)
{
    // Build registry dictionary
    var registry = new Dictionary<string, Lazy<ICadProvider>>();
    var providerNames = new List<string>();
    var providers = new Dictionary<string, IcadProviderRegistration>();

    // Always register Inventor (lazy — fails gracefully on Linux)
    providers["inventor"] = new LazyProviderRegistration<IMechanicalCadProvider>(
        sp => InventorProviderFactory(sp, configuration));

    // Always register Onshape (lazy — fails on missing creds)
    providers["onshape"] = new LazyProviderRegistration<ICadWebProvider>(
        sp => OnshapeProviderFactory(sp, configuration, userConfig));

    #if WINDOWS
    providers["solidworks"] = new LazyProviderRegistration<IMechanicalCadProvider>(
        sp => SolidWorksProviderFactory(sp, configuration));
    #else
    // SolidWorks registered even on Linux — resolve fails with clear error
    providers["solidworks"] = new LazyProviderRegistration<IMechanicalCadProvider>(
        sp => throw new ProviderConnectionException("SolidWorks requires Windows"));
    #endif

    // Register router with constructed registry
    services.AddSingleton<IProviderRouter>(sp =>
        new ProviderRouter(providers, userConfig?.ActiveProvider ?? "inventor"));

    // Register tools (single set, router-aware)
    services.AddSingleton<AtomicTools>();     // now takes IProviderRouter
    services.AddSingleton<SkillTools>();      // now takes IProviderRouter
    services.AddSingleton<SelectProviderTool>();
}

// Each provider is wrapped in Lazy<T> — not created until first Resolve()
```

**Phase 1 → Phase 2 transition**: In Phase 1, `RegisterCadProvider` remains a single-provider switch but reads from config. In Phase 2, `RegisterCadProvider` is replaced by `RegisterAllProviders`. The Phase 2 PR removes the old single-provider bootstrap entirely.

### 8. Tool Convergence Plan

**Objective**: Single `AtomicTools` + `SkillTools` registration set, both accepting `IProviderRouter`.

**Current state** (as-is):

| Tool Class | Constructor | Registration Condition |
| ----------- | ----------- | ---------------------- |
| `AtomicTools(IMechanicalCadProvider)` | Inventor provider | Only when active=inventor |
| `SkillTools(IMechanicalCadProvider)` | Inventor provider | Only when active=inventor |
| `OnshapeAtomicTools(ICadWebProvider)` | Onshape provider | Only when active=onshape |
| `OnshapeSkillTools(ICadWebProvider)` | Onshape provider | Only when active=onshape |

**Convergence target** (Phase 2):

| Tool Class | Constructor | Registration Condition |
| ----------- | ----------- | ---------------------- |
| `AtomicTools(IProviderRouter)` | Router | Always |
| `SkillTools(IProviderRouter)` | Router | Always |
| `SelectProviderTool` | (none) | Always |
| `OnshapeAtomicTools` | Removed/deprecated | N/A |
| `OnshapeSkillTools` | Removed/deprecated | N/A |

**Per-method change pattern**:

```csharp
// Before (Inventor):
[McpServerTool]
public Dictionary<string, object?> fillet(string edges, double radius, string mode = "constant")
    => Catch(() => _provider.Fillet(edges, radius, mode));

// After (converged):
[McpServerTool]
public Dictionary<string, object?> fillet(string edges, double radius, string mode = "constant")
{
    var provider = _router.Resolve(_router.CurrentProvider);
    if (provider is not IMechanicalCadProvider mech)
        return ToolHelpers.Error(
            $"{_router.CurrentProvider} does not support fillet operations");
    return Catch(() => mech.Fillet(edges, radius, mode));
}
```

**Web-CAD-specific operations**: Onshape has tools not in `ICadProvider` (`element_list`, `ws_list`, `feature_add`, `sketch_replace`). These are **not covered** by `ICadProvider` or `IMechanicalCadProvider`. Strategy:

1. Keep a small `WebCadTools(IProviderRouter)` class registered always
2. Each method checks `_router.Resolve(_router.CurrentProvider) is ICadWebProvider web`
3. If not web CAD → return error: "This operation requires a web CAD provider (Onshape)"
4. If web CAD → dispatch
5. This avoids polluting `AtomicTools` with web-specific methods and keeps `OnshapeAtomicTools` removable

**Tool naming**:

| Tool | Before (Inventor) | Before (Onshape) | After (converged) |
| ------ | ------------------- | ------------------ | ------------------- |
| Connection | `inventor_connect` | `onshape_connect` | `connect` |
| Disconnect | `inventor_disconnect` | `onshape_disconnect` | `disconnect` |
| Health | `inventor_health` | `onshape_health` | `health` |
| Doc open | `doc_open` | `onshape_doc_open(doc_id) → doc_open(path)` | `doc_open` |
| ... | ... | ... | ... |

**Backward-compat aliases**: For one PR cycle, deprecated aliases (`inventor_connect` → `connect`) are registered. Removed in final Phase 2 PR.

### 9. SolidWorks Merge Strategy

**Context**: `feature/solidworks-provider` branch uses `Cad:Provider` (not `Provider:Active`) and loads SolidWorks via reflection.

**Merge steps** (Phase 2 PR):

1. **Resolve config key divergence**: Map `Cad:Provider` values to unified config:
   - `Cad:Provider = SolidWorks` → `providers.solidworks.enabled = true`
   - `Cad:Provider = Inventor` → `providers.inventor.enabled = true`
   - Fallback: if `Cad:Provider` is set but `config.json` absent, treat as `active_provider`

2. **SolidWorks registration**:
   - Implement `IMechanicalCadProvider` (SolidWorksProvider)
   - Register in `RegisterAllProviders()` with lazy singleton
   - Platform-gated: always registered, Linux resolve throws clear error

3. **Tool convergence**: SolidWorks tools go through the same `AtomicTools` + router path. No separate tool class needed.

4. **`NullMechanicalCadProvider` update** (Phase 2):

```csharp
// Phase 2 — dynamic error messages based on active provider
public sealed class NullMechanicalCadProvider : IMechanicalCadProvider
{
    private readonly string _activeProviderName;

    public NullMechanicalCadProvider(string activeProviderName)
    {
        _activeProviderName = activeProviderName;
    }

    private Dictionary<string, object?> NotAvailable(string operation) => new()
    {
        ["success"] = false,
        ["error"] = $"{_activeProviderName} does not support {operation} operations",
    };

    // Every method calls NotAvailable(methodName)
    public Dictionary<string, object?> Fillet(string edges, double radius, string mode = "constant")
        => NotAvailable("fillet");
    // ... same for all ~50 methods
}
```

**When is NullMechanicalCadProvider used?** When the current provider is Onshape (or any non-mechanical CAD) and a mechanical-only operation is called. The router resolves the provider, the tool checks `is IMechanicalCadProvider`, and if false, returns the error (or the NullMechanicalCadProvider is the resolved `IMechanicalCadProvider` for Onshape sessions).

Actually — let me reconsider. `NullMechanicalCadProvider` is today registered as `IMechanicalCadProvider` when Onshape is active. Under the router, we don't need it: the tool checks `provider is IMechanicalCadProvider`, and if the resolved provider doesn't implement it, returns an error. `NullMechanicalCadProvider` can be removed entirely in Phase 2.

**Removal path**:

- Phase 2 PR: Remove `NullMechanicalCadProvider` registration from `ProviderBootstrap`
- Update tool methods to use `is IMechanicalCadProvider` check instead of relying on the null object
- Remove class entirely (or keep as deprecated shim for one cycle)

---

## Config Schema (Complete Reference)

```json
{
  "$schema": "https://mcp-cad.dev/schemas/config-v1.json",
  "version": 1,
  "active_provider": "onshape",
  "providers": {
    "onshape": {
      "enabled": true,
      "credentials": {
        "access_key": "abc123",
        "secret_key": "xyz789",
        "secret_key_id": "optional_skid"
      }
    },
    "inventor": {
      "enabled": false,
      "credentials": null
    },
    "solidworks": {
      "enabled": false,
      "credentials": null
    }
  },
  "agents": [
    { "name": "OpenCode", "url": "http://localhost:51777" },
    { "name": "Claude", "url": "http://localhost:51778" }
  ]
}
```

| Field | Type | Required | Description |
| ------- | ------ | ---------- | ------------- |
| `version` | int | ✓ | Schema version (currently 1) |
| `active_provider` | string \| null | — | Default provider name. null = unset |
| `providers` | object | ✓ | Per-provider config map |
| `providers.{name}.enabled` | bool | ✓ | Whether provider is available for routing |
| `providers.{name}.credentials` | object \| null | — | Provider-specific key-value credential pairs |
| `agents` | array | ✓ | MCP host agent registrations |
| `agents[].name` | string | ✓ | Display name |
| `agents[].url` | string | ✓ | Agent endpoint URL |

---

## File-Level Change List

### Phase 1 — End-User Setup (chained PRs)

#### PR 1a: UserConfigStore + Config Schema (~320 lines)

| File | Action | Est. Δ |
| ------ | -------- | -------- |
| `src/McpCad.Core/Models/UserConfig.cs` | NEW | +60 |
| `src/McpCad.Core/UserConfigStore.cs` | NEW | +110 |
| `src/McpCad.Core/NullMechanicalCadProvider.cs` | no change | 0 (P2) |
| `tests/McpCad.Tests/Core/UserConfigStoreTests.cs` | NEW | +150 |

#### PR 1b: Installer Wizard (~380 lines)

| File | Action | Est. Δ |
| ------ | -------- | -------- |
| `src/McpCad.Installer/Program.cs` | REWRITE | +230 |
| `src/McpCad.Installer/McpAgent.cs` | MODIFY | +40 |
| `src/McpCad.Installer/State.cs` | no change | 0 |
| `src/McpCad.Installer/ConfigManager.cs` | no change | 0 |
| `src/McpCad.Installer/McpCad.Installer.csproj` | no change | 0 |
| `tests/McpCad.Tests/Installer/InstallerWizardTests.cs` | NEW | +110 |

#### PR 1c: Server Config Load + Select-Provider Skill (~380 lines)

| File | Action | Est. Δ |
| ------ | -------- | -------- |
| `src/McpCad.Core/CadExecutionContext.cs` | NEW | +45 |
| `src/McpCad.Server/Program.cs` | MODIFY | +50 |
| `src/McpCad.Server/ProviderBootstrap.cs` | MODIFY | +30 |
| `src/McpCad.Tools/SelectProviderTool.cs` | NEW | +80 |
| `tests/McpCad.Tests/Server/ConfigLoadTests.cs` | NEW | +80 |
| `tests/McpCad.Tests/Tools/SelectProviderToolTests.cs` | NEW | +95 |

#### Phase 1 Total: ~1,080 LOC across 3 chained PRs (~320 + ~380 + ~380)

### Phase 2 — Dynamic Routing (chained PRs)

#### PR 2a: IProviderRouter + Lazy Registry (~380 lines)

| File | Action | Est. Δ |
| ------ | -------- | -------- |
| `src/McpCad.Core/IProviderRouter.cs` | NEW | +25 |
| `src/McpCad.Core/ProviderRouter.cs` | NEW | +130 |
| `src/McpCad.Core/CadExecutionContext.cs` | EXTEND | +30 |
| `src/McpCad.Core/ProviderNotFoundException.cs` | NEW | +10 |
| `src/McpCad.Core/ProviderDisabledException.cs` | NEW | +10 |
| `src/McpCad.Core/ProviderConnectionException.cs` | NEW | +10 |
| `src/McpCad.Server/ProviderBootstrap.cs` | MODIFY | +100 |
| `tests/McpCad.Tests/Core/ProviderRouterTests.cs` | NEW | +65 |

#### PR 2b: Tool Convergence (~380 lines)

| File | Action | Est. Δ |
| ------ | -------- | -------- |
| `src/McpCad.Tools/AtomicTools.cs` | MODIFY | +100 |
| `src/McpCad.Tools/SkillTools.cs` | MODIFY | +100 |
| `src/McpCad.Tools/WebCadTools.cs` | NEW | +50 |
| `src/McpCad.Tools/Onshape/OnshapeAtomicTools.cs` | DEPRECATE | +5 (obsolete attr) |
| `src/McpCad.Tools/Onshape/OnshapeSkillTools.cs` | DEPRECATE | +5 (obsolete attr) |
| `src/McpCad.Server/Program.cs` | MODIFY | +30 |
| `tests/McpCad.Tests/Tools/AtomicToolsRouterTests.cs` | NEW | +90 |

#### PR 2c: NullMechanicalCadProvider Removal + SW Prep (~380 lines)

| File | Action | Est. Δ |
| ------ | -------- | -------- |
| `src/McpCad.Core/NullMechanicalCadProvider.cs` | MODIFY/REMOVE | -50/+20 |
| `src/McpCad.Server/ProviderBootstrap.cs` | MODIFY | +60 |
| `src/McpCad.Core/IMechanicalCadProvider.cs` | no change | 0 |
| `src/McpCad.Core/ICadProvider.cs` | no change | 0 |
| `src/McpCad.Core/ICadWebProvider.cs` | no change | 0 |
| `tests/McpCad.Tests/Core/NullProviderTests.cs` | MODIFY | +60 |
| `tests/McpCad.Tests/Server/ProviderRegistrationTests.cs` | NEW | +120 |

#### Phase 2 Total: ~1,140 LOC across 3 chained PRs (~380 + ~380 + ~380)

### Overall Summary

| Phase | PRs | Est. LOC | Main changes |
| ------- | ----- | ---------- | ------------- |
| Phase 1 | 3 | ~1,080 | Config store, installer, server config load, select-provider skill |
| Phase 2 | 3 | ~1,140 | Router, lazy DI, tool convergence, NullProvider removal, SW prep |
| **Total** | **6** | **~2,220** | All under 400 LOC/PR |

---

## Testing Strategy (Strict TDD)

Every PR follows strict TDD: **tests before code**. The existing test suite (50 passed, 1 known failing, 7 skipped on Linux) must remain green.

### Phase 1 Tests

**PR 1a — UserConfigStore**:

- `UserConfigStore.DefaultConfigPath` returns correct path per platform
- `LoadAsync(path)` returns `UserConfig` for valid file
- `LoadAsync(missing)` returns null (no crash)
- `LoadAsync(corrupt)` throws `JsonException`
- `SaveAsync(config)` writes valid JSON
- `SaveAsync` atomic: temp → replace, no torn reads
- `CreateDefault()` returns all providers present, `active_provider=null`
- Round-trip: serialize → deserialize → identical
- Unknown version loads safely (v2 → deserialized with warning)
- Unknown provider key preserved (forward-compat)
- Zero NuGet dependency check (`McpCad.Core.csproj` no new packages)

**PR 1b — Installer Wizard**:

- Linux provider list shows Onshape only; Inventor/SW disabled
- Windows provider list shows all three selectable
- Credential prompt for Onshape shows access+secret+optional skid
- Credential prompt for Inventor shows auto-connect toggle
- SolidWorks credential prompt shows info note only
- Agent list contains OpenCode, Claude, Pi, VS Code (pre-selected all)
- `FindServerPath()` finds `McpCad.Server` on Linux (no .exe)
- `FindServerPath()` finds `McpCad.Server.exe` on Windows
- Server not found → warning printed, config still written
- Successful completion → valid config.json, no legacy state.json mutation
- Write failure → error displayed, retry offered
- `dotnet run` starts without additional build steps

**PR 1c — Server Config Load + Skill**:

- Config file present → server uses `active_provider` from config
- Config file absent → server falls back to `Provider:Active` from appsettings
- Config file active_provider=null → fallback to appsettings
- `mcp_cad_select_provider("inventor")` sets `CadExecutionContext.SelectedProvider = "inventor"`
- `mcp_cad_select_provider("unknown")` returns error
- Subsequent tool call reads context and dispatches to selected provider
- Context is session-only (not written to config.json)
- Skill tool registered regardless of active provider

### Phase 2 Tests

**PR 2a — IProviderRouter**:

- `PushProvider("inventor")` → `CurrentProvider` = `"inventor"`
- `PushProvider` returns disposable → dispose restores prior provider
- Concurrent calls have isolated `AsyncLocal` context
- Default provider from config when no push called
- `Resolve("inventor")` returns `ICadProvider` instance
- `Resolve("unknown")` throws `ProviderNotFoundException`
- `Resolve("inventor")` with `enabled=false` throws `ProviderDisabledException`
- All providers registered at startup (no connection attempt)
- Unused provider never constructed
- Concurrent first access → single instance (thread-safe)
- COM fails on Linux → clear error, other providers unaffected
- `AvailableProviders` lists all registered names

**PR 2b — Tool Convergence**:

- `AtomicTools` accepts `IProviderRouter` (not `ICadProvider`)
- Each tool calls `router.Resolve(router.CurrentProvider)` per invocation
- Same tool name routes to different provider per call context
- `fillet` with Onshape provider → `{"success": false, "error": "onshape does not support fillet"}`
- `fillet` with Inventor provider → dispatches to `InventorProvider.Fillet()`
- No duplicate tool names in MCP registry
- `OnshapeAtomicTools` deprecated (no runtime registration)
- Web-specific tools (`ws_list`, `element_list`) available via `WebCadTools`
- Backward-compat aliases work in transition, removable in final PR

**PR 2c — NullProvider Removal + SW Prep**:

- `NullMechanicalCadProvider` removed from DI registration
- Tool interface checks (`is IMechanicalCadProvider`) work for all mechanical operations
- Error messages include active provider name (not hardcoded "Inventor")
- SolidWorks registered as `IMechanicalCadProvider` (lazy)
- SolidWorks resolve on Linux → clear error
- `Cad:Provider` from SolidWorks branch handled in config fallback

### Non-Testable Requirements

| Requirement | Verification Method |
| ------------ | ------------------- |
| AsyncLocal isolation | Code review + concurrency test |
| No-initialization-on-registration | Static analysis (constructors side-effect-free) |
| Thread-safe lazy init | Code review of `Lazy<T>` usage + concurrency test |
| Config schema forward-compat | Round-trip test with unknown fields |

---

## PR Slicing (Chained PRs)

**Strategy**: Feature-branch-chain (recommended for rollback control). Each PR targets the previous PR branch. The final PR merges to main.

```
main
  └── PR 1a (UserConfigStore)          → feature/dpr-config-store
        └── PR 1b (Installer)          → feature/dpr-installer
              └── PR 1c (Config+Skill) → feature/dpr-server-config
                    └── PR 2a (Router) → feature/dpr-router
                          └── PR 2b (Convergence) → feature/dpr-tool-convergence
                                └── PR 2c (Null+SW) → feature/dpr-solidworks
                                      └── MERGE to main
```

**Loosen the chain with stacked-to-main if the team prefers speed**: Each PR merges directly to main. Valid if Phase 1 PRs don't overlap files with Phase 2 PRs (they largely don't).

**400-line budget**: Each PR targets ≤380 lines of actual changed code (not counting blank/comments). The estimates above are rough — LOC will be verified during apply. If a PR exceeds 400, split it further.

---

## Rollout Sequence

```
Week 1:      PR 1a → UserConfigStore lands, no visible change to end users
Week 1-2:    PR 1b → Installer wizard lands, users can run dotnet run
Week 2:      PR 1c → Server reads config, skill available, migration transparent
Week 2-3:    PR 2a → Router lands, but tools still use old registration
Week 3:      PR 2b → Tools converge, old Onshape tools deprecated
Week 3-4:    PR 2c → NullProvider removed, SolidWorks merge path ready
Week 4:      feature/solidworks-provider rebases and merges
```

Each PR is independently deployable. Rollback at any point restores the previous behavior.

---

## Open Items (Validate During Apply)

| # | Question | Decision Needed By |
| --- | ---------- | ------------------- |
| 1 | Does `CadExecutionContext` need to be explicitly reset between tool calls, or is the middleware pattern sufficient? | PR 2a apply |
| 2 | Should `UserConfigStore` validate provider names against known providers on save? | PR 1a apply |
| 3 | `SelectProviderTool` — pass provider via string arg vs. separate boolean args per provider? | PR 1c apply |
| 4 | WebCadTools — keep in McpCad.Tools or separate project? | PR 2b apply |
| 5 | `dotnet run` command: `dotnet run --project src/McpCad.Installer` — verify no extra args needed on Linux | PR 1b apply |
| 6 | Server discovery on Linux: verify `McpCad.Server` without `.exe` in publish output | PR 1b apply |

---

## Risks and Mitigations

| Risk | L | Mitigation |
| ------ | --- | ----------- |
| COM crash kills entire MCP process | H | Router catches provider exception per call. Out-of-proc COM is follow-up (documented). |
| Config migration gap — users never run installer | M | Fallback to `appsettings.json` is transparent. Old setups keep working. |
| Tool name collisions after convergence | H | Router dispatches by name not by provider. Tests verify zero duplicate MCP tool names. |
| Review workload >400 lines per PR | H | Chained PRs, each at ~380 lines. Strict TDD enforces discipline. |
| AsyncLocal isolation missed in a tool method | M | Code review + concurrency test in PR 2b. |
| SolidWorks `Cad:Provider` divergence | M | Unified config schema in Phase 1 absorbs both. Merge PR resolves the key difference. |

---

## Next Recommended Phase

`sdd-tasks` — break Phase 1 PR 1a into granular implementation tasks.
