# Tasks: onshape-provider

## Review Workload Forecast

| Field | Value |
| ------- | ------- |
| Estimated changed lines | ~1,400 additions across 21 new files + 3 modified |
| 400-line budget risk | High |
| Chained PRs recommended | Yes |
| Suggested split | PR-1 → PR-2 → PR-3 → PR-4 (stacked-to-main or feature-branch-chain) |
| Delivery strategy | ask-on-risk |
| Chain strategy | pending |

Decision needed before apply: Yes
Chained PRs recommended: Yes
Chain strategy: pending
400-line budget risk: High

### Suggested Work Units

| Unit | Goal | Likely PR | Base branch | Notes |
| ------ | ------ | ----------- | ------------- | ------- |
| 1 | Sibling interface + exception base + element address type | PR-1 | `main` | Core only; adds `ICadWebProvider`, `CadElementAddress`, `CadProviderException`; ~200 lines |
| 2 | HTTP layer: HMAC client + driver + csproj + Polly + WireMock test | PR-2 | `main` (or PR-1 branch in chain) | New `McpCad.Onshape` project; signing + polling + microversion; ~350 lines |
| 3 | `OnshapeProvider` + 5 managers delegating to client/driver | PR-3 | `main` (or PR-2 branch) | Implements `ICadWebProvider`; mirrors Inventor manager shape; ~450 lines (largest slice — split `AssemblyManager`/`ExportManager` into PR-3a if it overshoots) |
| 4 | Tools + DI switch + appsettings + solution wiring + conformance tests | PR-4 | `main` (or PR-3 branch) | `OnshapeAtomicTools`, `OnshapeSkillTools`, `Program.cs` branch, `appsettings.json`, `mcp-cad.sln`; ~250 lines |

### Chain Strategy Options (orchestrator must ask user)

- **stacked-to-main**: each PR merges to main in order. Fastest iteration; one rollback per slice.
- **feature-branch-chain**: feature/`onshape-provider` tracker accumulates; each child PR targets the prior branch. Cleanest diffs, harder rebase.
- **size:exception**: collapse into one PR with maintainer approval. Not recommended — quota burn + skew risk need separate reviews.

## Phase 1: Core Interface + Exceptions (PR-1)

- [x] 1.1 Create `src/McpCad.Core/Models/CadElementAddress.cs` — record with `DocumentId`, `WorkspaceOrVersionId`, `MicroversionId?`, `ElementId`; equality + round-trip serialization (round-trip scenario in spec).
- [x] 1.2 Create `src/McpCad.Core/Exceptions/CadProviderException.cs` — abstract base, `protected CadProviderException(string, Exception?)`.
- [x] 1.3 Create `src/McpCad.Core/Exceptions/OnshapeApiException.cs` — `: CadProviderException`; carries `HttpStatusCode` + `Reason` string.
- [x] 1.4 Create `src/McpCad.Core/Exceptions/OnshapeAuthException.cs` — `: CadProviderException`; thrown on 401/403.
- [x] 1.5 Create `src/McpCad.Core/Exceptions/OnshapeQuotaException.cs` — `: CadProviderException`; carries `RetryAfter` (TimeSpan?).
- [x] 1.6 Create `src/McpCad.Core/Exceptions/OnshapeMicroversionSkewException.cs` — `: CadProviderException`; carries `ServerMicroversion`.
- [x] 1.7 Create `src/McpCad.Core/ICadWebProvider.cs` — `interface ICadWebProvider : ICadProvider` with 13 methods (`Authenticate`, `WsList`, `WsCurrent`, `ElementList/Get/Create/Rename`, `DocOpen/Copy/Delete`, `VersionCreate`, `MicroversionGet`, `ExportStep`, `ExportStl`).
- [x] 1.8 Verify `McpCad.Core.csproj` stays dep-free (zero NuGet refs); only `using McpCad.Core.Models` cross-proj changes.
- [x] 1.9 Unit tests: `tests/McpCad.Tests/Core/CadElementAddressTests.cs` — round-trip with all 4 fields + null microversion.
- [x] 1.10 Unit tests: `tests/McpCad.Tests/Core/ExceptionHierarchyTests.cs` — verify 401 → `OnshapeAuthException`, 429 → `OnshapeQuotaException`, 409 → `OnshapeMicroversionSkewException` (no `HttpRequestException` leaks).

## Phase 2: HTTP Layer + HMAC Client (PR-2)

- [x] 2.1 Create `src/McpCad.Onshape/McpCad.Onshape.csproj` — `net8.0`, project ref `McpCad.Core`; NuGet: `Microsoft.Extensions.Http`, `Polly`, `WireMock.Net` (test only).
- [x] 2.2 Add `src/McpCad.Onshape/` to `src/mcp-cad.sln`.
- [x] 2.3 Create `src/McpCad.Onshape/OnshapeClient.cs` — `SendAsync(method, path, body?, query?)` adds HMAC headers (`Authorization`, `On-Nonce`, `On-Date`); canonical = `METHOD\nURI\nnonce\ndate\ncontentType\nbody`; throws typed exceptions on non-2xx.
- [x] 2.4 Implement HMAC signing helper `Sign(string canonical)` — HMAC-SHA256 with secret, base64 encoded.
- [x] 2.5 Implement `PollTranslationAsync(tid, ct)` — 2s interval, 120s timeout, throws `OnshapeApiException("export_timeout")` on expiry.
- [x] 2.6 Implement unit conversion helpers (`CmToMeters`/`MetersToCm`) — single site, `CultureInfo.InvariantCulture`, `"G9"`.
- [x] 2.7 Create `src/McpCad.Onshape/OnshapeDriver.cs` — holds `AccessKey`, default `ws`/`m`, last microversion; `Authenticate` POSTs to `/oauth/keys`.
- [x] 2.8 Add `rejectMicroversionSkew=true` on every write; omit on reads (spec scenario).
- [x] 2.9 Unit tests: `tests/McpCad.Tests/Onshape/HmacSigningTests.cs` — canonical string matches Onshape docs vector; GET and POST signing scenarios.
- [x] 2.10 Unit tests: `tests/McpCad.Tests/Onshape/UnitConversionTests.cs` — `5.0cm → 0.05m` write, `0.030m → 3.0cm` read, `G9` round-trip.
- [x] 2.11 Unit tests: `tests/McpCad.Tests/Onshape/ExportPollingTests.cs` — DONE returns URL, PENDING > 120s throws `export_timeout`.

## Phase 3: Provider + 5 Managers (PR-3)

- [x] 3.1 Create `src/McpCad.Onshape/Managers/DocumentManager.cs` — `DocOpen/Copy/Move/Delete`; constructor `(OnshapeClient, OnshapeDriver)`; cm↔m at `OnshapeClient` boundary.
- [x] 3.2 Create `src/McpCad.Onshape/Managers/PartStudioManager.cs` — `ElementList/Create/Rename`, sketch/feature stubs.
- [x] 3.3 Create `src/McpCad.Onshape/Managers/AssemblyManager.cs` — assembly-level CRUD + instance ops.
- [x] 3.4 Create `src/McpCad.Onshape/Managers/ExportManager.cs` — `ExportStep`/`ExportStl` synchronous wrappers over `OnshapeClient.PollTranslationAsync`.
- [x] 3.5 Create `src/McpCad.Onshape/Managers/FeatureManager.cs` (mirrors `McpCad.Inventor/Managers/FeatureManager.cs`) — feature spec/result wrappers.
- [x] 3.6 Create `src/McpCad.Onshape/OnshapeProvider.cs` — implements `ICadWebProvider`; ctor `(OnshapeClient, OnshapeDriver, 5 managers)`; one-line delegations; no business logic.
- [x] 3.7 Integration tests: `tests/McpCad.Tests/Onshape/OnshapeProviderConformanceTests.cs` — WireMock stand-in for `cad.onshape.com`; covers all 5 managers end-to-end.
- [x] 3.8 Integration tests: microversion skew scenario — WireMock returns 409 → `OnshapeMicroversionSkewException.ServerMicroversion` populated.
- [x] 3.9 Mock: `tests/McpCad.Tests/Mocks/MockOnshapeProvider.cs` — same shape as `MockInventorProvider`; reflection conformance.
- [x] 3.10 Conformance test: `OnshapeProvider` reflection test satisfies `ICadWebProvider` shape.

> **If PR-3 overshoots 400 lines**, split: PR-3a = `OnshapeProvider` + `DocumentManager` + `PartStudioManager`; PR-3b = `AssemblyManager` + `ExportManager` + `FeatureManager` + integration tests.

## Phase 4: Tools + DI + Config + Docs (PR-4)

- [x] 4.1 Create `src/McpCad.Tools/Onshape/McpCad.Tools.Onshape.csproj` — `net8.0`; refs `McpCad.Core`, `McpCad.Onshape`; add to `mcp-cad.sln`.
- [x] 4.2 Create `src/McpCad.Tools/Onshape/OnshapeAtomicTools.cs` — snake_case MCP tools consuming `ICadWebProvider` (`create_sketch`, `add_extrude`, `export_step`, …); catch `CadProviderException` → `ToolHelpers.Error(...)`.
- [x] 4.3 Create `src/McpCad.Tools/Onshape/OnshapeSkillTools.cs` — composed skills (sketch-and-extrude, etc.).
- [x] 4.4 Modify `src/McpCad.Server/Program.cs` — read `Provider:Active`; branch DI: Onshape → register `OnshapeClient`/`Driver`/`Provider`/`AtomicTools`/`SkillTools` + `NullMechanicalCadProvider`; unknown value throws `ConfigurationException` (fail-fast, no fallback).
- [x] 4.5 Modify `src/McpCad.Server/appsettings.json` — add `Provider:Active` (default `"inventor"`), `Onshape:{BaseUrl, AccessKey, SecretKeyId}` (keys via User Secrets/env, never committed).
- [x] 4.6 Modify `src/mcp-cad.sln` — add both new projects; verify `dotnet build` succeeds in Inventor mode (regression guard).
- [x] 4.7 Create `docs/providers.md` — Onshape vs Inventor side-by-side; auth, units, sync latency caveat.
- [x] 4.8 Conformance test: tool name collision — registering `create_sketch` from both providers in test throws `ToolRegistrationException`.
- [x] 4.9 Verify `Provider:Active = "inventor"` path remains byte-identical (DI branch never fires).
- [x] 4.10 Run full test suite; confirm no `McpCad.Core` NuGet deps added (Core stays dep-free).
