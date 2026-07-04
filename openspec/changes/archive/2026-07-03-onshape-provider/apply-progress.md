# Apply progress: onshape-provider

## Phase 4 (PR-4) — completed this run

| Task | Status | Notes |
| --- | --- | --- |
| 4.1 | done | `McpCad.Tools.Onshape.csproj`, solution entry |
| 4.2 | done | `OnshapeAtomicTools.cs` — `create_sketch`, `add_extrude`, `onshape_export_step`, `onshape_export_stl`, `onshape_*`; `CadProviderException` → `ToolHelpers.Error` |
| 4.3 | done | `OnshapeSkillTools.cs` — `skill_sketch_and_extrude`, `skill_create_and_export_step` |
| 4.4 | done | `Program.cs` + `ProviderBootstrap.cs` — `Provider:Active` branch, Onshape DI, `NullMechanicalCadProvider`, `ConfigurationException` on unknown |
| 4.5 | done | `appsettings.json` — `Provider:Active` default `inventor`, `Onshape` block |
| 4.6 | done | `mcp-cad.sln` — `McpCad.Tools.Onshape`; Server refs Onshape + Tools.Onshape |
| 4.7 | done | `docs/providers.md` |
| 4.8 | done | `ToolRegistrationCollisionTests.cs` — duplicate `create_sketch` → `ToolRegistrationException` |
| 4.9 | done | Inventor path: default/missing `Provider:Active` → inventor registration only; `ProviderBootstrapTests` |
| 4.10 | partial | `McpCad.Core` still has zero NuGet refs; **build/test not run in agent env** — `dotnet` not on PATH |

## 4R remediation slice (merge-blocking CRITICALs)

| ID | Fix | Status |
| --- | --- | --- |
| R1 secrets | Removed `onshape_authenticate` MCP tool (no `secret_key` in tool args). Keys from env / `Onshape:*` / User Secrets (`UserSecretsId` `mcp-cad-server-onshape`). `OnshapeDriver.Authenticate` calls `OnshapeClient.SetCredentials` for HMAC. | done |
| R1 paths | `Uri.EscapeDataString` on all `OnshapeApiPaths` segments; instance id + document move path encoded. | done |
| R1 delete | `onshape_doc_delete(document_id, confirm)` — requires `confirm=true` or `CadProviderException`; documented in `docs/providers.md`. | done |
| R2 assembly | `ICadWebProvider` + `OnshapeProvider` expose `AssemblyListInstances` / `AssemblyCreateInstance` / `AssemblyDeleteInstance` delegating to `AssemblyManager`. | done |
| R3 mock | `MockOnshapeProvider.WithError` throws `CadProviderException` on `SketchReplace` (and related paths). | done |
| R3 collision | Onshape exports renamed to `onshape_export_step` / `onshape_export_stl`. **Round 2:** `ToolRegistrationCollisionTests` — `AtomicTools` + `OnshapeAtomicTools` register together without collision; `GetToolMethodNames(OnshapeAtomicTools)` asserts `onshape_export_*` and no bare `export_*` on Onshape type. | done |
| R3 null mechanical | `NullMechanicalCadProvider_SketchCreate_Returns_SuccessFalse_In_Onshape_Mode` in `ProviderBootstrapTests`. | done |
| R4 poll | `PollTranslationAsync` catches transient 5xx / transport errors and continues until timeout; test `PollTranslationAsync_Transient500ThenDone_Succeeds`. | done |
| R4 429 | **v1:** single retry on HTTP 429 when `Retry-After` delta present in `SendAsync`; **Polly** package removed from `McpCad.Onshape.csproj` (deferred full policy to v2). | done |

### Files changed (remediation)

- `src/McpCad.Onshape/OnshapeApiPaths.cs`, `OnshapeClient.cs`, `OnshapeDriver.cs`, `OnshapeProvider.cs`, `Managers/AssemblyManager.cs`, `Managers/DocumentManager.cs`, `McpCad.Onshape.csproj`
- `src/McpCad.Core/ICadWebProvider.cs`
- `src/McpCad.Tools/Onshape/OnshapeAtomicTools.cs`
- `src/McpCad.Server/Program.cs`, `ProgramMarker.cs`, `McpCad.Server.csproj`
- `tests/.../MockOnshapeProvider.cs`, `ToolRegistrationCollisionTests.cs`, `ProviderBootstrapTests.cs`, `ExportPollingTests.cs`
- `docs/providers.md`

## Files changed (Phase 4)

- `src/McpCad.Core/Exceptions/ConfigurationException.cs`, `ToolRegistrationException.cs`
- `src/McpCad.Core/NullMechanicalCadProvider.cs`
- `src/McpCad.Core/ICadWebProvider.cs` (+ `SketchReplace`, `FeatureAdd`)
- `src/McpCad.Tools/ToolRegistrationValidator.cs`
- `src/McpCad.Tools/Onshape/*`
- `src/McpCad.Server/Program.cs`, `ProviderBootstrap.cs`, `appsettings.json`, `McpCad.Server.csproj`
- `src/McpCad.Onshape/OnshapeProvider.cs` (delegates for new interface methods)
- `tests/.../ToolRegistrationCollisionTests.cs`, `OnshapeAtomicToolsTests.cs`, `ProviderBootstrapTests.cs`
- `tests/McpCad.Tests/Mocks/MockOnshapeProvider.cs`
- `docs/providers.md`
- `openspec/changes/onshape-provider/tasks.md` (4.1–4.10 checked)

## Verification

```bash
cd src && dotnet build mcp-cad.sln && dotnet test ../tests/McpCad.Tests/McpCad.Tests.csproj
```

Not executed in remediation agent environment (`dotnet` missing on PATH). Run locally before PR / re-4R.

## 4R remediation round 2 (micro)

| Item | Change | Status |
| --- | --- | --- |
| R3 collision tests | Replaced stale `export_stl` collision expectation with no-collision registration test + positive `onshape_export_*` name assertions | done |
| R4 429 test | `ExportPollingTests.SendAsync_429WithRetryAfterDelta_RetriesOnceThenSucceeds` | done |

### Files changed (round 2)

- `tests/McpCad.Tests/Tools/ToolRegistrationCollisionTests.cs`
- `tests/McpCad.Tests/Onshape/ExportPollingTests.cs`
- `openspec/changes/onshape-provider/apply-progress.md`

## Prior phases

PR-1–PR-3 were already complete per orchestrator handoff (Core, Onshape HTTP, provider/managers).

## Remaining

Re-run targeted `review-risk` or full 4R after local `dotnet test` passes. Then `sdd-verify` / optional judgment-day. Do not `sdd-archive` until verified.

## TDD Cycle Evidence

Strict TDD not exercised for remediation slice (fixes driven by 4R CRITICAL list; tests added/updated alongside).
