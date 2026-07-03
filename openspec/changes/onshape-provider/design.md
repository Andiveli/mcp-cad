# Design: onshape-provider

## Technical Approach

Sibling-interface strategy. `ICadWebProvider : ICadProvider` extends only the base 13 methods (no Inventor "active sketch" model). Onshape ships in a new `McpCad.Onshape` project with a thin HTTP layer (`OnshapeClient` + HMAC) and the same manager pattern as `McpCad.Inventor`. The server picks one provider at startup from `Provider:Active`. Tools duplicate per provider — each tool class consumes its own `ICadWebProvider` or `IMechanicalCadProvider`. Delivery is chained PRs (4 slices, each ≤400 lines).

## Architecture Decisions

### Decision: Sibling interface, not extension of IMechanicalCadProvider

**Choice**: `ICadWebProvider : ICadProvider` adds ~13 web-specific methods.
**Alternatives considered**: Extend `IMechanicalCadProvider` with web-only methods gated by capability detection.
**Rationale**: Onshape has no active-sketch buffer, no live parameter expression tree, no transient COM document handle. Forcing `SketchCreate` to return `Dictionary<string,object?>` keyed by an `IMechanicalCadProvider` contract smuggles in a fake buffer. Sibling keeps each surface honest and lets Fusion 360 / 3DExperience plug in later without ripple.

### Decision: Manager-per-domain pattern (mirrors Inventor)

**Choice**: 5 managers — `DocumentManager`, `PartStudioManager`, `AssemblyManager`, `ExportManager`, `ElementManager` — each takes `OnshapeClient` + `OnshapeDriver` by constructor injection.
**Alternatives considered**: Flat provider class with all 25 methods inline; FeatureManager splits per concern.
**Rationale**: Same shape as `McpCad.Inventor/Managers/*.cs` keeps cognitive load low for reviewers familiar with the codebase. Each manager holds 3–7 methods, under the 400-line PR budget.

### Decision: Unit conversion at provider boundary, host = cm

**Choice**: All public `ICadWebProvider` methods accept/return cm (matches host = Inventor cm). `OnshapeClient` serializes cm→m (`/100`) on write, parses m→cm (`*100`) on read. One conversion site uses `CultureInfo.InvariantCulture` + `"G9"`.
**Alternatives considered**: Internal m, configurable units, Onshape-default m.
**Rationale**: Host uniformity — Inventor and Onshape tools speak the same units. Single conversion site prevents drift. `G9` rounds floats identically both ways.

### Decision: HMAC-SHA256 inside `OnshapeClient` only

**Choice**: `OnshapeClient.SendAsync(...)` is the single chokepoint that adds `Authorization: OnshapeKey {AccessKey}:{Base64HmacSha256(canonical)}`, `On-Nonce` (GUID), `On-Date` (RFC 7231).
**Alternatives considered**: `DelegatingHandler` on `HttpClient`, Polly retry.
**Rationale**: Easy to unit-test signing in isolation; Polly still plugs in later as a second handler.

### Decision: Async export polling, sync tool surface

**Choice**: `ExportManager.ExportStepAsync` polls `GET /translations/{tid}` every 2s up to 120s. Tool layer calls `.GetAwaiter().GetResult()`. Spec deferred.
**Alternatives considered**: `Task<Dictionary<string,object?>>` tool surface.
**Rationale**: Matches existing sync envelope contract; LLM loops blocking 10–60s is acceptable for first cut; async tools listed in proposal out-of-scope.

### Decision: Per-provider DI branch keyed on `Provider:Active`

**Choice**: `Program.cs` reads `Provider:Active`; unknown value throws at startup. Onshape mode wires `IHttpClientFactory`, `OnshapeClient`, `OnshapeDriver`, `OnshapeProvider`, `OnshapeAtomicTools`, `OnshapeSkillTools` and registers `IMechanicalCadProvider -> NullMechanicalCadProvider`. Inventor mode untouched.
**Alternatives considered**: Decorator chain, `IServiceProvider.IsService<>`.
**Rationale**: Fail-fast spec (provider-routing); no fallback path; Inventor COM stays cold.

## Data Flow

```
Tool (OnshapeAtomicTools)
    └─→ IOnshapeProvider   ───┐
                               ├─→ PartStudioManager ──┐
                               ├─→ DocumentManager  ──┤
                               ├─→ ExportManager    ──┴─→ OnshapeClient
                               └─→ ElementManager         (HttpClient + HMAC)
                                                          └─→ api.onshape.com
```

Tool catches `CadProviderException` (covers `OnshapeApiException`, `OnshapeAuthException`, `OnshapeQuotaException`, `OnshapeMicroversionSkewException`) and returns `ToolHelpers.Error(...)`. Success path returns envelope `{ success: true, did, eid, ... }`.

## File Changes

| File | Action | Description |
|---|---|---|
| `src/McpCad.Core/ICadWebProvider.cs` | Create | ~13 methods: `Authenticate`, `Health`, `DocOpen/Copy/Move/Delete`, `WsList/Current`, `ElementList/Get/Create/Rename`, `ExportStep/Stl` (polled), `UnitConvert` (optional hook) |
| `src/McpCad.Core/Models/CadElementAddress.cs` | Create | Record `DocumentId`, `WorkspaceOrVersionId`, `MicroversionId?`, `ElementId` |
| `src/McpCad.Core/Exceptions/CadProviderException.cs` | Create | Abstract base |
| `src/McpCad.Core/Exceptions/OnshapeApiException.cs` | Create | HTTP status + reason |
| `src/McpCad.Core/Exceptions/OnshapeAuthException.cs` | Create | 401/403 |
| `src/McpCad.Core/Exceptions/OnshapeQuotaException.cs` | Create | 429 + `RetryAfter` |
| `src/McpCad.Core/Exceptions/OnshapeMicroversionSkewException.cs` | Create | 409 + `ServerMicroversion` |
| `src/McpCad.Onshape/McpCad.Onshape.csproj` | Create | `net8.0`, refs Core; deps: `Microsoft.Extensions.Http`, `Polly` |
| `src/McpCad.Onshape/OnshapeClient.cs` | Create | HMAC signing, `SendAsync`, 2s/120s export polling |
| `src/McpCad.Onshape/OnshapeDriver.cs` | Create | Session state: tokens, last microversion, default `ws/m` |
| `src/McpCad.Onshape/OnshapeProvider.cs` | Create | Implements `ICadWebProvider`; delegates to 5 managers |
| `src/McpCad.Onshape/Managers/{Document,PartStudio,Assembly,Export,Element}Manager.cs` | Create | One per concern |
| `src/McpCad.Tools/Onshape/OnshapeAtomicTools.cs` | Create | snake_case MCP tools consuming `ICadWebProvider` |
| `src/McpCad.Tools/Onshape/OnshapeSkillTools.cs` | Create | Composed skills |
| `src/McpCad.Server/Program.cs` | Modify | Read `Provider:Active`; branch DI; fail-fast on unknown |
| `src/McpCad.Server/appsettings.json` | Modify | Add `Provider:Active` + `Onshape:{BaseUrl,AccessKey,SecretKeyId}` (secrets via env) |
| `src/mcp-cad.sln` | Modify | Add `McpCad.Onshape` + `McpCad.Tools.Onshape` projects |
| `tests/McpCad.Tests/Onshape/OnshapeClientTests.cs` | Create | HMAC vector tests, no HTTP |
| `tests/McpCad.Tests/Onshape/OnshapeProviderConformanceTests.cs` | Create | WireMock fake `cad.onshape.com`; conformance + microversion skew |
| `tests/McpCad.Tests/Mocks/MockOnshapeProvider.cs` | Create | Same shape as `MockInventorProvider` |
| `docs/providers.md` | Create | Add Onshape; docs two providers side-by-side |

## Interfaces / Contracts

```csharp
public interface ICadWebProvider : ICadProvider
{
    Dictionary<string, object?> Authenticate(string accessKey, string secretKeyId, string secretKey);
    Dictionary<string, object?> WsList(string? docId = null);
    Dictionary<string, object?> WsCurrent(string docId);
    Dictionary<string, object?> ElementList(string docId, string wvm, string wvmid);
    Dictionary<string, object?> ElementCreate(CadElementAddress addr, string name, string type);
    Dictionary<string, object?> ElementRename(CadElementAddress addr, string name);
    Dictionary<string, object?> ExportStep(CadElementAddress addr, Dictionary<string,object?>? opts = null);
    Dictionary<string, object?> ExportStl(CadElementAddress addr, Dictionary<string,object?>? opts = null);
    // Plus ~5 more (DocCopy/Delete, VersionCreate, MicroversionGet, ParamList/Set)
}

public abstract class CadProviderException : Exception { protected CadProviderException(string m, Exception? i = null) : base(m, i) {} }
```

## Testing Strategy

| Layer | What to Test | Approach |
|---|---|---|
| Unit | HMAC canonical string, unit conversion (`G9` round-trip), `CadElementAddress` (de)serialization | xUnit, no I/O |
| Integration | `OnshapeProvider` against WireMock stand-in for `cad.onshape.com` | `WireMock.Net` server in fixture; exercises all managers |
| Conformance | `OnshapeProvider` satisfies `ICadWebProvider` shape | Reflection test, same as `MockInventorProvider` |
| E2E | Real Onshape dev sandbox: sketch → extrude → STEP | Manual + recorded golden JSON; gated on real-key env var |

## Migration / Rollout

No data migration. `Provider:Active = "inventor"` keeps prior behavior byte-identical (DI branch never fires). Rollback = flip config; no code revert.

## Open Questions

- [ ] Should `ICadWebProvider.ExportStep` use `CadElementAddress` instead of `string` `did/eid`? — leaning YES for type safety; spec says "providers MUST accept/return this"
- [ ] Polly retry policy: 3 retries on 429 with `Retry-After` jitter? — defer to v2