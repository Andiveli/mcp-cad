# Proposal: onshape-provider

## Intent

mcp-cad binds to one backend today (Inventor via COM). Onshape is cloud-native: REST + HMAC, `did/wvm/wvmid/eid` addressing, microversion, async exports. Faking Onshape as `IMechanicalCadProvider` smuggles a fake "active sketch" buffer, hides unit conversion, and blocks Fusion 360 / 3DExperience later.

Add **`ICadWebProvider`** as a **sibling** of `IMechanicalCadProvider`. Onshape is the first implementation. Tools split per provider; server picks one at startup.

## Scope

### In Scope
- `ICadWebProvider : ICadProvider` (~12 base + ~13 web methods).
- `McpCad.Onshape`: `OnshapeClient` (HMAC-SHA256), `OnshapeDriver`, `OnshapeProvider`, 5 managers.
- `OnshapeAtomicTools` + `OnshapeSkillTools`.
- `OnshapeApiException`, `OnshapeAuthException`, generic `CadProviderException` base.
- `Provider:Active` server switch + `Onshape` block in `appsettings.json`.
- Unit conversion (cm↔in) at provider boundary. WireMock fake for CI. HMAC API-key auth.

### Out of Scope
- Fusion 360 / 3DExperience. Async `Task<>` tools. Multi-provider server. Python `cad-provider-protocol` spec.

## Capabilities

### New Capabilities
- `web-cad-provider`: `ICadWebProvider` + `CadProviderException` hierarchy.
- `onshape-integration`: HMAC client, element addressing, microversion, async export polling, units.
- `provider-routing`: `Provider:Active` switch, DI branch, per-provider tool registration.

### Modified Capabilities
- None.

## Approach

Sibling-interface strategy. Tool duplication is bounded — one-line delegations to a shared `OnshapeDriver`. HMAC in `OnshapeClient`. Every write sends `rejectMicroversionSkew: true`. STEP export polls `GET /translations/{tid}`.

Delivery: **chained PRs** (400-line budget exceeded).
- PR-1 Core interface + exceptions
- PR-2 Onshape client + driver + HMAC
- PR-3 OnshapeProvider + managers
- PR-4 Tools + DI switch + appsettings + tests

## Affected Areas

| Area | Impact |
|---|---|
| `src/McpCad.Core/ICadWebProvider.cs` | New |
| `src/McpCad.Core/Exceptions/`, `src/McpCad.Onshape/`, `src/McpCad.Tools/Onshape/`, `docs/providers.md` | New |
| `src/McpCad.Server/Program.cs`, `appsettings.json`, `src/mcp-cad.sln` | Modified |

## Risks

| Risk | Lik | Mitigation |
|---|---|---|
| Annual API quota burn by LLM loops | High | Batch features, cache `featureId`, dry-run CI |
| Microversion skew race | High | `rejectMicroversionSkew: true` on every write |
| Floating-point drift cm↔in + 400-line PR budget | High | Single conversion site (`InvariantCulture`, `G9`); chained PRs (4 slices) |
| Sync STEP blocks MCP thread 10–60s | Med | Document latency; async follow-up |

(Keys never in appsettings.)

## Rollback Plan

PR-by-PR git revert. PR-1 additive, trivially reversible. PR-4 flips `Provider:Active` back to `"inventor"` to restore prior behavior without code revert.

## Dependencies

- `Microsoft.Extensions.Http`, `Polly`, `WireMock.Net` (Onshape only; Core stays dep-free)
- Onshape key + secret via User Secrets / env vars

## Success Criteria

- [ ] `OnshapeProvider` passes `ICadWebProvider` conformance tests against WireMock
- [ ] Sketch → extrude → STEP export works on real Onshape dev sandbox
- [ ] All 4 chained PRs under 400 changed lines each
- [ ] `McpCad.Core` retains zero NuGet deps; Inventor path (`Provider:Active = "inventor"`) green
