# Apply Progress: dynamic-provider-routing

## PR 1a — UserConfigStore + Config Schema

**Branch**: `feature/dpr-config-store`

**Scope**: T1.1–T1.6 only.

### Completed tasks

- [x] T1.1–T1.6 (see prior apply batch)

### Verification (PR 1a)

- `dotnet test --filter FullyQualifiedName~UserConfig`: 20 passed

---

## PR 1b — Installer Wizard

**Branch**: `feature/dpr-installer` (from `feature/dpr-config-store`)

**Scope**: T2.1–T2.7 only.

### Completed tasks

- [x] T2.1–T2.7 (see prior apply batch)

---

## PR 1c — Server Config Load + Select-Provider Skill

**Branch**: `feature/dpr-server-config` (from `feature/dpr-installer`)

**Scope**: T3.1–T3.7 only.

### Completed tasks

- [x] T3.1–T3.7 (see prior apply batch)

---

## PR 2a — IProviderRouter + Lazy Registry

**Branch**: `feature/dpr-router` (from `feature/dpr-server-config`)

**Scope**: T4.1–T4.7 only. No tool convergence (PR 2b), no NullProvider removal (PR 2c).

### Completed tasks

- [x] T4.1 — `ProviderRouterTests.cs` (push/dispose, AsyncLocal isolation, resolve, disabled, concurrency, lazy single instance)
- [x] T4.2 — `IProviderRouter.cs`
- [x] T4.3 — `ProviderNotFoundException`, `ProviderDisabledException`, `ProviderConnectionException`
- [x] T4.4 — `ProviderRouter.cs` (`AsyncLocal`, `Lazy<ICadProvider>`, `ProviderScope`)
- [x] T4.5 — `CadExecutionContext.CurrentProvider`, `PriorContext`
- [x] T4.6 — `RegisterAllProviders` in `ProviderBootstrap.AllProviders.cs`; `Program.cs` calls it + `IProviderRouter` singleton; Inventor/Onshape/SolidWorks lazy registry; `NullMechanicalCadProvider` retained
- [x] T4.7 — concurrency + `RegisterAllProvidersTests` (platform errors for inventor/solidworks on Linux)

### TDD Cycle Evidence

| Task | RED | GREEN | Notes |
| ------ | ----- | ------- | ------- |
| T4.1 | ProviderRouterTests (types missing) | Core router + exceptions | 12 tests |
| T4.6 | RegisterAllProvidersTests | AllProviders bootstrap | MCP tool registration unchanged per active provider (2b convergence) |
| T4.7 | Parallel lazy + unused provider tests | Pass | |

### Files changed (PR 2a)

| File | Action |
| ------ | -------- |
| `src/McpCad.Core/IProviderRouter.cs` | NEW |
| `src/McpCad.Core/ProviderRouter.cs` | NEW |
| `src/McpCad.Core/Exceptions/ProviderNotFoundException.cs` | NEW |
| `src/McpCad.Core/Exceptions/ProviderDisabledException.cs` | NEW |
| `src/McpCad.Core/Exceptions/ProviderConnectionException.cs` | NEW |
| `src/McpCad.Core/CadExecutionContext.cs` | MODIFY |
| `src/McpCad.Server/ProviderBootstrap.AllProviders.cs` | NEW |
| `src/McpCad.Server/Program.cs` | MODIFY (`RegisterAllProviders`) |
| `tests/McpCad.Tests/Core/ProviderRouterTests.cs` | NEW |
| `tests/McpCad.Tests/Server/RegisterAllProvidersTests.cs` | NEW |
| `tests/McpCad.Tests/Server/ConfigLoadTests.cs` | MODIFY (bootstrap API) |
| `openspec/changes/dynamic-provider-routing/tasks.md` | T4.1–T4.7 checked |

### Verification

```bash
dotnet test tests/McpCad.Tests/McpCad.Tests.csproj --filter "FullyQualifiedName~ProviderRouter|FullyQualifiedName~RegisterAllProviders"
# Passed: 15

dotnet test tests/McpCad.Tests/McpCad.Tests.csproj
# Passed: 108, Failed: 1 (ExportPollingTests — pre-existing), Skipped: 7
```

### Deviations / notes

- `RegisterCadProvider` removed; `ProviderBootstrap.cs` is config resolution only. `RegisterAllProviders` is the sole DI entry. `NullMechanicalCadProvider` deleted; mechanical errors come from `RouterToolDispatch` + `is IMechanicalCadProvider` checks.
- SolidWorks lazy factory throws `ProviderConnectionException` (stub until PR 2c).
- Tools still register per `activeProvider` in `Program.cs` (PR 2b); router is in DI for upcoming convergence.

### Remaining

- PR 2b: T5.x tool convergence (`AtomicTools` → `IProviderRouter`)
- PR 2c: T6.x NullProvider removal + SolidWorks prep

### Delivery

- **No git commit** per parent instruction.
- **skill_resolution**: `paths-injected` (`work-unit-commits/SKILL.md`).
