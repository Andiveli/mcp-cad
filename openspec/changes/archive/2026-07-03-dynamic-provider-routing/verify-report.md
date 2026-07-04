# Verify Report: dynamic-provider-routing (full chain)

## Status

**PASS WITH WARNINGS** — All tasks T1.1–T6.6 marked complete in `tasks.md`. Implementation matches Phase 1 + Phase 2 scope. **Not archive-clean** until warnings below are accepted or fixed.

---

## Scope

Full change verify: **PR 1a–1c** (Phase 1), **PR 2a–2c** (Phase 2), including tool convergence, `RegisterCadProvider` removal, and `NullMechanicalCadProvider` deletion.

---

## Artifacts Checked

| Artifact | Source | Status |
| -------- | -------- | ------ |
| Proposal | `openspec/changes/dynamic-provider-routing/proposal.md` | ✅ |
| Specs | `openspec/changes/dynamic-provider-routing/specs/` (8 capability dirs) | ✅ |
| Design | `design.md` | ✅ |
| Tasks | `tasks.md` (T1.1–T6.6) | ✅ all checked |
| Apply Progress | `apply-progress.md` | ✅ (partial PR 2b/2c narrative; code state current) |
| Source | `McpCad.Core`, `McpCad.Server`, `McpCad.Installer`, `McpCad.Tools` | ✅ |
| Tests | `tests/McpCad.Tests/` | ✅ |

---

## Task Completion Summary

| PR | Tasks | Status |
| ---- | ----- | ------ |
| 1a UserConfigStore | T1.1–T1.6 | ✅ |
| 1b Installer wizard | T2.1–T2.7 | ✅ |
| 1c Server config + select-provider | T3.1–T3.7 | ✅ |
| 2a Router + lazy registry | T4.1–T4.7 | ✅ |
| 2b Tool convergence | T5.1–T5.7 | ✅ (T5.7 aliases added then **removed** in 2c per task note) |
| 2c NullProvider + SW prep | T6.1–T6.6 | ✅ |

---

## Spec Coverage

| Spec | Status | Notes |
| ---- | ------ | ----- |
| `user-config-store` | ✅ PASS | Sync `Load()`/`Save()`, atomic `.bak`, cross-platform path tests |
| `installer-wizard` | ✅ PASS | Spectre wizard tests; platform-filtered providers |
| `server-config-loader` | ⚠️ PARTIAL | Primary/fallback/`Cad:Provider` implemented; **corrupt JSON** throws from `Program` (no degraded fallback); **startup config-source logging** not verified in code |
| `select-provider-skill` | ✅ PASS | `SelectProviderTool` + tests; agent skill file is harness-level |
| `lazy-provider-registry` | ✅ PASS | Lazy registry, Linux platform errors |
| `provider-router` | ✅ PASS | Push/pop, AsyncLocal, resolve, disabled |
| `mcp-tool-registration` | ✅ PASS | Unified `AtomicTools`/`SkillTools`/`WebCadTools`; no `inventor_connect` aliases; `OnshapeAtomicTools` not in MCP host |
| `cad-provider-protocol` | ⚠️ PARTIAL | Mechanical errors via router + `is IMechanicalCadProvider`; **`NullMechanicalCadProvider` removed** (supersedes null-DI scenarios); **SolidWorks stub** throws on resolve, does not implement `IMechanicalCadProvider` yet |

---

## Test Results

### Focused (change-owned tests)

```bash
dotnet test tests/McpCad.Tests/McpCad.Tests.csproj \
  --filter "FullyQualifiedName~UserConfig|FullyQualifiedName~UserConfigStore|\
FullyQualifiedName~InstallerWizard|FullyQualifiedName~ConfigLoad|\
FullyQualifiedName~SelectProvider|FullyQualifiedName~ProviderRouter|\
FullyQualifiedName~RegisterAllProviders|FullyQualifiedName~AtomicToolsRouter|\
FullyQualifiedName~ProviderRegistration"
```

**Result: Passed: 72, Failed: 0**

### Full suite

```bash
dotnet test tests/McpCad.Tests/McpCad.Tests.csproj
```

**Result: Passed: 120, Failed: 1, Skipped: 7**

| Result | Count | Notes |
| -------- | ------- | ----- |
| ✅ Passed | 120 | +12 vs prior partial verify (2b/2c tests) |
| ❌ Failed | 1 | `ExportPollingTests.PollTranslationAsync_Transient500ThenDone_Succeeds` — **pre-existing**, unrelated |
| ⏭️ Skipped | 7 | Inventor COM / Windows integration (expected on Linux) |

**No regressions attributable to dynamic-provider-routing.**

---

## Proposal Success Criteria

| # | Criterion | Status | Evidence |
| --- | ----------- | ------ | ---------- |
| 1 | UserConfigStore cross-platform | ✅ | T1.x, 20+ config tests |
| 2 | Installer wizard → valid config | ✅ | T2.x, InstallerWizardTests |
| 3 | Server starts from config + fallback | ✅ | `Program.cs` `UserConfigStore.Load()` + `ResolveEffectiveActiveProvider` |
| 4 | `mcp_cad_select_provider` | ✅ | SelectProviderToolTests |
| 5 | Linux server discovery (no `.exe`) | ✅ | Installer wizard tests |
| 6 | Router per tool call, no restart | ✅ | `IProviderRouter`, unified MCP tools, AtomicToolsRouterTests |
| 7 | Chained PRs ≤400 LOC each | ✅ | Per design/tasks (not re-measured in this verify) |
| 8 | Existing tests pass | ⚠️ | 120 pass; **1 known fail** (export polling) |
| 9 | SolidWorks alongside Inventor without collision | ⚠️ PARTIAL | Lazy `solidworks` in registry; **stub only**, not full `SolidWorksProvider` |
| 10 | Strict TDD | ✅ | RED/GREEN tasks in `tasks.md`; focused tests green |

---

## Strict TDD Compliance

| Check | Status |
| ----- | ------ |
| `strict_tdd: true` in `openspec/config.yaml` | ✅ |
| Tests precede implementation per task IDs | ✅ (documented in tasks + apply-progress) |
| Assertion quality (behavioral, not smoke-only) | ✅ Router, config, convergence tests use real dispatch/assertions |

---

## Findings

### CRITICAL

None for this change scope (export polling failure is out of scope).

### WARNING

| ID | Finding | Recommendation |
| ---- | ------- | -------------- |
| W1 | **Corrupt `config.json`** causes `JsonException` at `UserConfigStore.Load()` in `Program.cs` with no catch — contradicts `server-config-loader` “degraded startup” scenario | Wrap load in try/catch; log warning; fall back to `appsettings` |
| W2 | **`server-config-loader` spec** still documents `LoadAsync()`, `NullMechanicalCadProvider` in Onshape DI, and Phase 1 single-provider DI | Update spec deltas to match sync API + `RegisterAllProviders` |
| W3 | **SolidWorks** success criterion #9 is **prep only** (`ProviderConnectionException` on resolve) | Track follow-up change or `feature/solidworks-provider` merge |
| W4 | **`apply-progress.md`** still mentions PR 2b/2c as “remaining” in older sections | Append PR 2b/2c completion + `RegisterCadProvider` removal note (partially done) |

### SUGGESTION

| ID | Finding |
| ---- | ------- |
| S1 | Add explicit startup log: config source + resolved `active_provider` (spec “Logging at Startup”) |
| S2 | Fix or quarantine `ExportPollingTests` so full suite is green before release |
| S3 | Update `docs/providers.md` tool table (still lists separate Onshape tool families) |

---

## Runtime Architecture (verified)

- **Single DI entry**: `ProviderBootstrap.RegisterAllProviders` only (`RegisterCadProvider` **removed**).
- **MCP tools**: `AtomicTools`, `SkillTools`, `WebCadTools`, `SelectProviderTool` always registered.
- **Mechanical on Onshape**: `RouterToolDispatch` + `is IMechanicalCadProvider` (no `NullMechanicalCadProvider` in DI).
- **Legacy config**: `Cad:Provider` → `inventor` / `solidworks` mapping in `ResolveEffectiveActiveProvider`.

---

## Blockers for `sdd-archive`

| Blocker | Severity | Owner action |
| ------- | -------- | ------------ |
| Accept W1–W3 as documented debt | Optional | Product/architect sign-off |
| Export polling fail | INFO | Pre-existing; not DPR blocker |
| Uncommitted work / branch chain | Process | Commits + PRs per `feature-branch-chain` when ready |

**Archive recommendation**: **Proceed** if team accepts W1–W3 and known export test failure. Otherwise fix W1 + refresh specs, then re-run verify.

---

## Commands Executed

```bash
dotnet test tests/McpCad.Tests/McpCad.Tests.csproj --filter "FullyQualifiedName~UserConfig|..."
# Passed: 72

dotnet test tests/McpCad.Tests/McpCad.Tests.csproj
# Passed: 120, Failed: 1, Skipped: 7
```

---

## next_recommended

`sdd-archive` (with warnings recorded) **or** small follow-up PR for W1 + spec sync, then archive.

---

*Verified: 2026-07-03 — orchestrator inline verify (post PR 2b/2c + RegisterCadProvider cleanup)*
